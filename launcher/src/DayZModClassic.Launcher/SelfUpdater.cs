using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DayZModClassic.Launcher;

// Rename-swap self-update. The running exe at P downloads a verified copy S
// into LOCALAPPDATA, starts "S --apply-update P --waitpid <pid>" and exits.
// S waits for the pid, renames P to P.old, copies itself to P, and restarts P.
// Renaming a just-exited exe is allowed on Windows; the .old of the previous
// version is cleaned on the next normal start. Files written by HttpClient
// carry no Zone.Identifier, so the swapped exe does not retrigger SmartScreen.
public static class SelfUpdater
{
    public static string UpdateDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DayZModClassic", "update");

    public static string StagedExePath => Path.Combine(UpdateDir, "DayZModClassic-new.exe");

    // Call first thing in Main. Returns true when this process was started
    // only to apply an update and should exit without showing UI.
    public static bool HandleStartupArgs(string[] args)
    {
        var idx = Array.IndexOf(args, "--apply-update");
        if (idx < 0 || idx + 1 >= args.Length) return false;

        var targetExe = args[idx + 1];
        int waitPid = 0;
        var pidIdx = Array.IndexOf(args, "--waitpid");
        if (pidIdx >= 0 && pidIdx + 1 < args.Length) int.TryParse(args[pidIdx + 1], out waitPid);

        ApplyUpdate(targetExe, waitPid);
        return true;
    }

    private static void ApplyUpdate(string targetExe, int waitPid)
    {
        Logger.Info($"apply-update target=\"{Logger.Scrub(targetExe)}\" waitpid={waitPid}");
        try
        {
            if (waitPid > 0)
            {
                try
                {
                    using var p = Process.GetProcessById(waitPid);
                    p.WaitForExit(15000);
                }
                catch { /* already gone */ }
            }

            var backup = targetExe + ".old";
            Exception? last = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (attempt > 0) Thread.Sleep(500);
                try
                {
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(targetExe, backup);
                    File.Copy(Environment.ProcessPath!, targetExe);
                    Process.Start(new ProcessStartInfo(targetExe, "--updated") { UseShellExecute = true });
                    Logger.Info("apply-update done");
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    // Roll the rename back if the copy is what failed.
                    try { if (!File.Exists(targetExe) && File.Exists(backup)) File.Move(backup, targetExe); } catch { }
                }
            }

            Logger.Exception("apply-update failed", last!);
            MessageBox.Show(
                "The launcher update could not be applied automatically.\n\n" +
                "Please download the latest version from https://dayzmodclassic.com/downloads",
                "DayZ Mod Classic", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            Logger.Exception("apply-update fatal", ex);
        }
    }

    public static bool ExeDirWritable()
    {
        try
        {
            var dir = Path.GetDirectoryName(Environment.ProcessPath!)!;
            var probe = Path.Combine(dir, ".dzc-writetest");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    public static async Task<string> DownloadAsync(VersionInfo v, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(UpdateDir);
        var staged = StagedExePath;

        using var resp = await ManifestService.Http.GetAsync(v.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? 0;

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long done = 0;
        await using (var body = await resp.Content.ReadAsStreamAsync(ct))
        await using (var outStream = new FileStream(staged, FileMode.Create, FileAccess.Write))
        {
            var buffer = new byte[1024 * 1024];
            int read;
            while ((read = await body.ReadAsync(buffer, ct)) > 0)
            {
                await outStream.WriteAsync(buffer.AsMemory(0, read), ct);
                hasher.AppendData(buffer, 0, read);
                done += read;
                progress.Report(new InstallProgress("download", "launcher update", 1, 1, done, total, 0));
            }
        }

        var sha = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        if (!string.IsNullOrEmpty(v.Sha256) &&
            !string.Equals(sha, v.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(staged); } catch { }
            throw new InvalidOperationException("Launcher update download was corrupt (hash mismatch). Try again.");
        }
        return staged;
    }

    public static void BeginUpdate(string stagedExePath)
    {
        var current = Environment.ProcessPath!;
        Logger.Info($"self-update starting from=\"{Logger.Scrub(current)}\"");
        Process.Start(new ProcessStartInfo(stagedExePath,
            $"--apply-update \"{current}\" --waitpid {Environment.ProcessId}")
        {
            UseShellExecute = true
        });
        Application.Exit();
    }

    public static void CleanupOldBinaries()
    {
        try
        {
            var old = Environment.ProcessPath + ".old";
            if (File.Exists(old)) File.Delete(old);
        }
        catch { /* previous process may still be exiting; next start gets it */ }
        try
        {
            if (Directory.Exists(UpdateDir)) Directory.Delete(UpdateDir, recursive: true);
        }
        catch { }
    }
}
