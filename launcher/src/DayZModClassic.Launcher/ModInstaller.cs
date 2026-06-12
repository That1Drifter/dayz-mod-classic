using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DayZModClassic.Launcher;

public static class ModInstaller
{
    public const string StagingDirName = ".dzc-staging";
    private const long FreeSpaceSlackBytes = 64L * 1024 * 1024;
    private const int DownloadBufferBytes = 8 * 1024 * 1024;

    public class InstallException : Exception
    {
        public InstallException(string message) : base(message) { }
        public InstallException(string message, Exception inner) : base(message, inner) { }
    }

    // Compares manifest against the local install. Hash work happens here, so
    // call off the UI thread; warm-cache runs are stat calls only.
    public static UpdateCheckResult Check(LauncherConfig cfg, ModManifest manifest, HashCache cache)
    {
        var root = cfg.A2oaPath;
        var toDownload = new List<ManifestFile>();
        bool anyPresent = false;

        foreach (var f in manifest.Files)
        {
            var target = TargetPath(root, f.Path);
            if (!File.Exists(target))
            {
                toDownload.Add(f);
                continue;
            }
            anyPresent = true;
            try
            {
                if (new FileInfo(target).Length != f.Size ||
                    !string.Equals(cache.GetSha256(target, f.Path), f.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    toDownload.Add(f);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"check: could not read {f.Path}: {ex.Message}");
                toDownload.Add(f);
            }
        }
        cache.Save();

        var modPbo = TargetPath(root, GameLauncher.ModPboRelative.Replace('\\', '/'));
        var state = !File.Exists(modPbo) && !anyPresent ? InstallState.NotInstalled
                  : toDownload.Count > 0 ? InstallState.UpdateAvailable
                  : InstallState.UpToDate;

        return new UpdateCheckResult(state, manifest.ModVersion, toDownload, toDownload.Sum(f => f.Size));
    }

    // Two-phase install: download + verify everything into <a2oa>\.dzc-staging,
    // then commit with atomic same-volume moves. Targets are never touched
    // until every byte is verified, so a failed download can't corrupt an
    // existing install, and the PBO/bisign skew window is the commit's
    // sub-second move loop.
    public static async Task InstallAsync(
        LauncherConfig cfg,
        ModManifest manifest,
        IReadOnlyList<ManifestFile> toDownload,
        HashCache cache,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        var root = cfg.A2oaPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            throw new InstallException("Arma 2: Operation Arrowhead folder not found. Install Arma 2 and Arma 2 OA from Steam first.");

        ProbeWritable(root);

        var staging = Path.Combine(root, StagingDirName);
        Directory.CreateDirectory(staging);

        long totalBytes = toDownload.Sum(f => f.Size);
        EnsureFreeSpace(root, staging, toDownload, totalBytes);

        // --- Acquire phase ---
        long doneBytes = 0;
        var speed = new SpeedMeter();
        for (int i = 0; i < toDownload.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var f = toDownload[i];
            var staged = Path.Combine(staging, f.Sha256 + ".blob");

            if (TryReuseStaged(staged, f))
            {
                doneBytes += f.Size;
                progress.Report(new InstallProgress("download", FileName(f), i + 1, toDownload.Count, doneBytes, totalBytes, speed.Current));
                continue;
            }

            var url = ResolveUrl(manifest.BaseUrl, f.Url);
            bool ok = false;
            for (int attempt = 1; attempt <= 2 && !ok; attempt++)
            {
                try
                {
                    await DownloadOneAsync(url, staged, f, i, toDownload.Count, doneBytes, totalBytes, speed, progress, ct);
                    ok = true;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    TryDelete(staged + ".part");
                    throw;
                }
                catch (Exception ex) when (attempt == 1)
                {
                    Logger.Warn($"download attempt 1 failed file={f.Path}: {ex.Message}; retrying");
                    TryDelete(staged + ".part");
                }
                catch (Exception ex)
                {
                    TryDelete(staged + ".part");
                    throw new InstallException($"Download failed for {FileName(f)}: {ex.Message}", ex);
                }
            }
            doneBytes += f.Size;
        }

        // --- Commit phase (not cancellable; sub-second) ---
        RefuseIfGameRunning();
        for (int i = 0; i < toDownload.Count; i++)
        {
            var f = toDownload[i];
            var staged = Path.Combine(staging, f.Sha256 + ".blob");
            var target = TargetPath(root, f.Path);
            progress.Report(new InstallProgress("commit", FileName(f), i + 1, toDownload.Count, totalBytes, totalBytes, 0));

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            cache.Invalidate(f.Path);
            MoveWithRetry(staged, target, f.Path);

            var fi = new FileInfo(target);
            cache.Record(f.Path, fi.Length, fi.LastWriteTimeUtc, f.Sha256);
        }
        cache.Save();

        TryDeleteDirIfEmpty(staging);
        Logger.Info($"install committed files={toDownload.Count} bytes={totalBytes} modVersion={manifest.ModVersion}");
    }

    private static async Task DownloadOneAsync(
        Uri url, string staged, ManifestFile f,
        int index, int count, long doneBytesBase, long totalBytes,
        SpeedMeter speed, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var part = staged + ".part";
        using var resp = await ManifestService.Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long fileBytes = 0;
        await using (var body = await resp.Content.ReadAsStreamAsync(ct))
        await using (var outStream = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, DownloadBufferBytes))
        {
            var buffer = new byte[DownloadBufferBytes];
            int read;
            while ((read = await body.ReadAsync(buffer, ct)) > 0)
            {
                await outStream.WriteAsync(buffer.AsMemory(0, read), ct);
                hasher.AppendData(buffer, 0, read);
                fileBytes += read;
                speed.Add(read);
                progress.Report(new InstallProgress("download", FileName(f), index + 1, count,
                    doneBytesBase + fileBytes, totalBytes, speed.Current));
            }
        }

        var sha = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        if (!string.Equals(sha, f.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(part);
            throw new InstallException($"Hash mismatch for {FileName(f)} (expected {f.Sha256[..12]}…, got {sha[..12]}…). The download may be corrupt; try again.");
        }
        File.Move(part, staged, overwrite: true);
    }

    private static bool TryReuseStaged(string staged, ManifestFile f)
    {
        try
        {
            // A verified blob from an earlier cancelled/crashed run; also accept
            // a complete .part whose content checks out.
            foreach (var candidate in new[] { staged, staged + ".part" })
            {
                if (!File.Exists(candidate)) continue;
                if (new FileInfo(candidate).Length != f.Size) { TryDelete(candidate); continue; }
                if (string.Equals(HashCache.HashFile(candidate), f.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    if (candidate != staged) File.Move(candidate, staged, overwrite: true);
                    Logger.Info($"staging reuse {f.Path}");
                    return true;
                }
                TryDelete(candidate);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"staging reuse check failed {f.Path}: {ex.Message}");
        }
        return false;
    }

    private static void MoveWithRetry(string staged, string target, string relPath)
    {
        var delays = new[] { 0, 500, 1500 };
        for (int attempt = 0; attempt < delays.Length; attempt++)
        {
            if (delays[attempt] > 0) Thread.Sleep(delays[attempt]);
            try
            {
                if (File.Exists(target))
                {
                    var attrs = File.GetAttributes(target);
                    if ((attrs & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(target, attrs & ~FileAttributes.ReadOnly);
                }
                File.Move(staged, target, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < delays.Length - 1 && (ex is IOException || ex is UnauthorizedAccessException))
            {
                Logger.Warn($"commit retry {attempt + 1} for {relPath}: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new InstallException(
                    $"Could not replace {relPath}: {ex.Message}\n\n" +
                    "Close Arma 2 if it is running, or add an antivirus exclusion for the Arma 2 OA folder, then click UPDATE again. " +
                    "The downloaded file is kept, so the retry will be instant.", ex);
            }
        }
    }

    private static void RefuseIfGameRunning()
    {
        bool running;
        try
        {
            running = Process.GetProcesses().Any(p =>
            {
                try { return p.ProcessName.StartsWith("arma2oa", StringComparison.OrdinalIgnoreCase) ||
                             p.ProcessName.StartsWith("ArmA2OA", StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            });
        }
        catch { running = false; }
        if (running)
            throw new InstallException("Arma 2 OA is running. Close the game, then click UPDATE again (downloads are kept).");
    }

    private static void ProbeWritable(string root)
    {
        var probe = Path.Combine(root, ".dzc-writetest");
        try
        {
            File.WriteAllText(probe, "x");
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            throw new InstallException(
                $"No write access to the Arma 2 OA folder:\n{root}\n\n" +
                "If the game is installed under Program Files, run the launcher as administrator once, " +
                "or move the Steam library out of Program Files.", ex);
        }
    }

    private static void EnsureFreeSpace(string root, string staging, IReadOnlyList<ManifestFile> toDownload, long totalBytes)
    {
        try
        {
            long alreadyStaged = toDownload
                .Select(f => Path.Combine(staging, f.Sha256 + ".blob"))
                .Where(File.Exists)
                .Sum(p => new FileInfo(p).Length);
            long needed = totalBytes - alreadyStaged + FreeSpaceSlackBytes;
            var free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root))!).AvailableFreeSpace;
            if (free < needed)
                throw new InstallException(
                    $"Not enough disk space on {Path.GetPathRoot(root)}: need about {needed / (1024 * 1024)} MB, only {free / (1024 * 1024)} MB free.");
        }
        catch (InstallException) { throw; }
        catch (Exception ex)
        {
            Logger.Warn($"free-space check skipped: {ex.Message}");
        }
    }

    private static string TargetPath(string root, string relPath) =>
        Path.Combine(root, relPath.Replace('/', '\\'));

    private static Uri ResolveUrl(string baseUrl, string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs)) return abs;
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        return new Uri(new Uri(baseUrl), url);
    }

    private static string FileName(ManifestFile f) => f.Path[(f.Path.LastIndexOf('/') + 1)..];

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirIfEmpty(string dir)
    {
        try
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch { }
    }

    // Bytes/sec over a ~1s sliding window.
    private sealed class SpeedMeter
    {
        private readonly Queue<(long ticks, int bytes)> _window = new();
        private long _windowBytes;

        public void Add(int bytes)
        {
            var now = Environment.TickCount64;
            _window.Enqueue((now, bytes));
            _windowBytes += bytes;
            while (_window.Count > 0 && now - _window.Peek().ticks > 1000)
                _windowBytes -= _window.Dequeue().bytes;
        }

        public double Current
        {
            get
            {
                if (_window.Count == 0) return 0;
                var span = Environment.TickCount64 - _window.Peek().ticks;
                return span <= 0 ? 0 : _windowBytes * 1000.0 / span;
            }
        }
    }
}
