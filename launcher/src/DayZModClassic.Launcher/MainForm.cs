using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DayZModClassic.Launcher;

public class MainForm : Form
{
    private const string AppVersion = AppInfo.Version;

    private enum PlayState { Checking, Install, Update, Play, Busy, Blocked }

    private LauncherConfig _config = new();
    private List<ServerEntry> _servers = new();
    private CancellationTokenSource _pingCts = new();
    private CancellationTokenSource? _installCts;
    private PlayState _playState = PlayState.Checking;
    private VersionInfo? _versionInfo;
    private ModManifest? _manifest;
    private UpdateCheckResult? _updateCheck;
    private HashCache _hashCache = HashCache.Load();

    // UI
    private ListView _lvServers = null!;
    private Button _btnRefresh = null!;
    private Button _btnAddServer = null!;
    private Label _lblSteam = null!;
    private Label _lblA2oa = null!;
    private Label _lblMod = null!;
    private Label _lblBe = null!;
    private Label _lblVersion = null!;
    private TextBox _tbName = null!;
    private Button _btnPlay = null!;
    private Button _btnRpt = null!;
    private Button _btnModFolder = null!;
    private Button _btnHelp = null!;
    private ContextMenuStrip _helpMenu = null!;
    private System.Windows.Forms.Timer _healthTimer = null!;
    private Label _lblStatus = null!;
    private ContextMenuStrip _ctxMenu = null!;
    private Ui.FlatProgressBar _progress = null!;
    private LinkLabel _lnkPlayAnyway = null!;
    private ToolStripMenuItem _miUpdateLauncher = null!;

    public MainForm()
    {
        Text = $"DayZ Mod Classic {AppVersion}";
        Size = new Size(760, 570);
        MinimumSize = new Size(660, 510);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(8)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));

        // LEFT: server list panel
        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 0, 6, 0),
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        _lvServers = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            GridLines = false,
            HideSelection = false,
        };
        _lvServers.Columns.Add("Name", 220);
        _lvServers.Columns.Add("Players", 70);
        _lvServers.Columns.Add("Ping", 60);
        _lvServers.Columns.Add("Version", 60);
        _lvServers.DoubleClick += (_, __) => DoPlay();

        _ctxMenu = new ContextMenuStrip();
        var miSetDefault = new ToolStripMenuItem("Set as default");
        miSetDefault.Click += (_, __) => SetSelectedAsDefault();
        var miRemove = new ToolStripMenuItem("Remove (custom only)");
        miRemove.Click += (_, __) => RemoveSelectedCustom();
        _ctxMenu.Items.Add(miSetDefault);
        _ctxMenu.Items.Add(miRemove);
        _lvServers.ContextMenuStrip = _ctxMenu;

        var listButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
        };
        _btnRefresh = new Button { Text = "Refresh", Width = 90, Height = 28 };
        _btnRefresh.Click += async (_, __) => await ReloadServersAsync();
        _btnAddServer = new Button { Text = "Add custom server", Width = 150, Height = 28 };
        _btnAddServer.Click += (_, __) => ShowAddServerDialog();
        listButtons.Controls.Add(_btnRefresh);
        listButtons.Controls.Add(_btnAddServer);

        leftPanel.Controls.Add(_lvServers, 0, 0);
        leftPanel.Controls.Add(listButtons, 0, 1);

        // RIGHT: health panel (Panel + caption; GroupBox borders fight dark themes)
        var rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Ui.Theme.Panel,
            Padding = new Padding(1),
        };
        var healthCaption = new Label
        {
            Text = "STATUS",
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            ForeColor = Ui.Theme.Mute,
        };
        var healthLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(10, 6, 10, 6),
            AutoSize = false,
        };
        _lblSteam   = MakeStatusLabel();
        _lblA2oa    = MakeStatusLabel();
        _lblMod     = MakeStatusLabel();
        _lblBe      = MakeStatusLabel();
        _lblVersion = MakeStatusLabel();
        _lblVersion.ForeColor = SystemColors.ControlText;
        _lblVersion.Text = $"Launcher version: {AppVersion}";
        healthLayout.Controls.Add(_lblSteam);
        healthLayout.Controls.Add(_lblA2oa);
        healthLayout.Controls.Add(_lblMod);
        healthLayout.Controls.Add(_lblBe);
        healthLayout.Controls.Add(_lblVersion);
        rightPanel.Controls.Add(healthLayout);
        rightPanel.Controls.Add(healthCaption);

        // BOTTOM: action bar (spans full width)
        var actionBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 3,
            Padding = new Padding(0, 8, 0, 0),
        };
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        actionBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        actionBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        actionBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        var lblPlayer = new Label
        {
            Text = "Player name:",
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
        };
        _tbName = new TextBox { Dock = DockStyle.Fill, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        _tbName.TextChanged += (_, __) =>
        {
            _config.PlayerName = _tbName.Text;
        };

        _btnPlay = new Button
        {
            Text = "PLAY",
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
        };
        Ui.Theme.StylePrimary(_btnPlay);
        _btnPlay.Click += (_, __) => DoPlay();

        _btnRpt = new Button { Text = "Open RPT log", Dock = DockStyle.Fill };
        _btnRpt.Click += (_, __) => OpenRpt();
        _btnModFolder = new Button { Text = "Open mod folder", Dock = DockStyle.Fill };
        _btnModFolder.Click += (_, __) => OpenModFolder();

        _helpMenu = new ContextMenuStrip();
        var miReport = new ToolStripMenuItem("Save diagnostic report...");
        miReport.Click += (_, __) => SaveDiagReport();
        _miUpdateLauncher = new ToolStripMenuItem("Update launcher...") { Visible = false };
        _miUpdateLauncher.Click += async (_, __) => { if (_versionInfo != null) await RunSelfUpdateAsync(_versionInfo); };
        var miAbout  = new ToolStripMenuItem("About...");
        miAbout.Click += (_, __) => ShowAbout();
        _helpMenu.Items.Add(miReport);
        _helpMenu.Items.Add(_miUpdateLauncher);
        _helpMenu.Items.Add(miAbout);

        _btnHelp = new Button { Text = "Help ▾", Dock = DockStyle.Fill };
        _btnHelp.Click += (_, __) => _helpMenu.Show(_btnHelp, new Point(0, _btnHelp.Height));

        actionBar.Controls.Add(lblPlayer, 0, 0);
        actionBar.Controls.Add(_tbName, 1, 0);
        actionBar.Controls.Add(_btnPlay, 2, 0);
        actionBar.Controls.Add(_btnRpt, 3, 0);
        actionBar.Controls.Add(_btnModFolder, 4, 0);
        actionBar.Controls.Add(_btnHelp, 5, 0);

        _progress = new Ui.FlatProgressBar
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            Visible = false,
        };
        actionBar.Controls.Add(_progress, 0, 1);
        actionBar.SetColumnSpan(_progress, 6);

        _lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = SystemColors.GrayText,
            Text = "",
        };
        actionBar.Controls.Add(_lblStatus, 0, 2);
        actionBar.SetColumnSpan(_lblStatus, 5);

        _lnkPlayAnyway = new LinkLabel
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Text = "Play anyway",
            Visible = false,
        };
        _lnkPlayAnyway.LinkClicked += (_, __) => PlayAnyway();
        actionBar.Controls.Add(_lnkPlayAnyway, 5, 2);

        // wire root
        root.Controls.Add(leftPanel, 0, 0);
        root.Controls.Add(rightPanel, 1, 0);

        var bottomHost = new Panel { Dock = DockStyle.Fill };
        bottomHost.Controls.Add(actionBar);
        actionBar.Dock = DockStyle.Fill;
        root.Controls.Add(bottomHost, 0, 1);
        root.SetColumnSpan(bottomHost, 2);

        Controls.Add(root);
        Controls.Add(new Ui.HeaderPanel(AppVersion));

        Ui.Theme.Apply(this);
        Ui.Theme.StylePrimary(_btnPlay); // Theme.Apply restyles all buttons; re-assert primary
        _lblStatus.ForeColor = Ui.Theme.Mute;
        _lblVersion.ForeColor = Ui.Theme.Ink;
    }

    private static Label MakeStatusLabel() => new()
    {
        AutoSize = false,
        Height = 22,
        Dock = DockStyle.Top,
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "...",
    };

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        _config = ConfigStore.Load();

        // Detect if missing
        if (string.IsNullOrEmpty(_config.SteamPath) || string.IsNullOrEmpty(_config.A2oaPath))
        {
            var det = SteamDetector.Detect();
            if (string.IsNullOrEmpty(_config.SteamPath) && !string.IsNullOrEmpty(det.SteamPath))
                _config.SteamPath = det.SteamPath!;
            if (string.IsNullOrEmpty(_config.A2oaPath) && !string.IsNullOrEmpty(det.A2oaPath))
                _config.A2oaPath = det.A2oaPath!;
            if (string.IsNullOrEmpty(_config.A2BasePath) && !string.IsNullOrEmpty(det.A2BasePath))
                _config.A2BasePath = det.A2BasePath!;
            ConfigStore.Save(_config);
            Logger.Info($"detect run steam=\"{Logger.Scrub(_config.SteamPath ?? "")}\" a2oa=\"{Logger.Scrub(_config.A2oaPath ?? "")}\" a2base=\"{Logger.Scrub(_config.A2BasePath ?? "")}\"");
        }

        _tbName.Text = _config.PlayerName;

        var h = GameLauncher.ComputeHealth(_config);
        Logger.Info($"health startup steam={h.SteamRunning} a2oa={h.A2oaInstalled} mod={h.ModInstalled} be={h.BattlEyeFixPresent}");

        UpdateHealth();
        _healthTimer = new System.Windows.Forms.Timer { Interval = 10_000 };
        _healthTimer.Tick += (_, __) => UpdateHealth();
        _healthTimer.Start();

        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--updated") >= 0)
            _lblStatus.Text = $"Launcher updated to {AppVersion}.";

        OfferDesktopShortcutOnce();

        SetPlayState(PlayState.Checking);
        var updateCheckTask = RunStartupUpdateCheckAsync();
        await ReloadServersAsync();
        await updateCheckTask;
    }

    // ---- Install / update pipeline ----

    private async Task RunStartupUpdateCheckAsync()
    {
        _versionInfo = await ManifestService.FetchVersionAsync(_config.VersionUrl);

        // Launcher self-update gate runs before the mod check.
        if (_versionInfo != null && !await HandleLauncherUpdateGateAsync(_versionInfo))
            return; // forced update in progress; PLAY stays blocked

        var manifestUrl = !string.IsNullOrEmpty(_config.ManifestUrl)
            ? _config.ManifestUrl
            : _versionInfo?.ManifestUrl ?? "";
        _manifest = string.IsNullOrEmpty(manifestUrl)
            ? null
            : await ManifestService.FetchManifestAsync(manifestUrl);

        if (_manifest == null)
        {
            var h = GameLauncher.ComputeHealth(_config);
            if (h.ModInstalled)
            {
                SetPlayState(PlayState.Play);
                _lblStatus.Text = "Could not check for mod updates (offline?). Playing with installed files.";
            }
            else
            {
                SetPlayState(PlayState.Blocked);
                _lblStatus.Text = "Internet connection required for first install.";
            }
            return;
        }

        _lblStatus.Text = "Verifying mod files...";
        var manifest = _manifest;
        _updateCheck = await Task.Run(() => ModInstaller.Check(_config, manifest, _hashCache));
        Logger.Info($"mod check state={_updateCheck.State} files={_updateCheck.ToDownload.Count} bytes={_updateCheck.DownloadBytes}");

        switch (_updateCheck.State)
        {
            case InstallState.NotInstalled:
                SetPlayState(PlayState.Install);
                _lblStatus.Text = $"Mod not installed. Click INSTALL to download {FormatMb(_updateCheck.DownloadBytes)}.";
                break;
            case InstallState.UpdateAvailable:
                SetPlayState(PlayState.Update);
                _lblStatus.Text = $"Update available (mod {_updateCheck.ModVersion}): {_updateCheck.ToDownload.Count} file(s), {FormatMb(_updateCheck.DownloadBytes)}.";
                break;
            default:
                SetPlayState(PlayState.Play);
                _lblStatus.Text = $"Mod {_updateCheck.ModVersion} is up to date.";
                break;
        }
    }

    // Returns false when a forced update takes over (UI stays blocked).
    private async Task<bool> HandleLauncherUpdateGateAsync(VersionInfo v)
    {
        bool forced = IsNewer(v.MinRequired, AppVersion);
        bool optional = !forced && IsNewer(v.Latest, AppVersion);
        if (!forced && !optional) return true;

        if (!SelfUpdater.ExeDirWritable())
        {
            _lblStatus.Text = $"Launcher {v.Latest} is available; download it from dayzmodclassic.com/downloads.";
            if (forced)
            {
                SetPlayState(PlayState.Blocked);
                MessageBox.Show(this,
                    $"Launcher {v.Latest} is required, but this launcher cannot update itself " +
                    "from its current folder.\n\nDownload the new version from " +
                    "https://dayzmodclassic.com/downloads and replace this file.",
                    "DayZ Mod Classic", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                try { Process.Start(new ProcessStartInfo("https://dayzmodclassic.com/downloads") { UseShellExecute = true }); }
                catch { }
                return false;
            }
            return true;
        }

        if (forced)
        {
            SetPlayState(PlayState.Blocked);
            var pick = MessageBox.Show(this,
                $"Launcher update {v.Latest} is required to continue.\n\nUpdate now?",
                "DayZ Mod Classic", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (pick != DialogResult.OK)
            {
                Close();
                return false;
            }
            await RunSelfUpdateAsync(v);
            return false;
        }

        // Optional: surface it, keep going.
        _lblStatus.Text = $"Launcher {v.Latest} is available. Use Help > Update launcher.";
        _miUpdateLauncher.Visible = true;
        return true;
    }

    private async Task RunSelfUpdateAsync(VersionInfo v)
    {
        _btnPlay.Enabled = false;
        _progress.Visible = true;
        _progress.Fraction = 0;
        var progress = new Progress<InstallProgress>(p =>
        {
            _progress.Fraction = p.BytesTotal > 0 ? (double)p.BytesDone / p.BytesTotal : 0;
            _lblStatus.Text = $"Downloading launcher {v.Latest}...  {FormatMb(p.BytesDone)}{(p.BytesTotal > 0 ? " / " + FormatMb(p.BytesTotal) : "")}";
        });
        try
        {
            var staged = await SelfUpdater.DownloadAsync(v, progress, CancellationToken.None);
            _lblStatus.Text = "Restarting to apply the update...";
            SelfUpdater.BeginUpdate(staged);
        }
        catch (Exception ex)
        {
            Logger.Exception("self-update download", ex);
            _progress.Visible = false;
            MessageBox.Show(this, $"Launcher update failed: {ex.Message}", "DayZ Mod Classic",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool IsNewer(string candidate, string current)
    {
        return Version.TryParse(candidate, out var c) && Version.TryParse(current, out var cur) && c > cur;
    }

    private void SetPlayState(PlayState state)
    {
        _playState = state;
        _lnkPlayAnyway.Visible = state == PlayState.Update;
        switch (state)
        {
            case PlayState.Checking:
                _btnPlay.Text = "...";
                _btnPlay.Enabled = false;
                break;
            case PlayState.Install:
                _btnPlay.Text = "INSTALL";
                _btnPlay.Enabled = true;
                break;
            case PlayState.Update:
                _btnPlay.Text = "UPDATE";
                _btnPlay.Enabled = true;
                break;
            case PlayState.Play:
                _btnPlay.Text = "PLAY";
                _btnPlay.Enabled = true;
                break;
            case PlayState.Busy:
                _btnPlay.Text = "CANCEL";
                _btnPlay.Enabled = true;
                break;
            case PlayState.Blocked:
                _btnPlay.Text = "PLAY";
                _btnPlay.Enabled = false;
                break;
        }
    }

    private async Task RunInstallAsync()
    {
        if (_manifest == null || _updateCheck == null) return;
        if (string.IsNullOrEmpty(_config.A2oaPath) || !Directory.Exists(_config.A2oaPath))
        {
            var pick = MessageBox.Show(this,
                "Arma 2: Operation Arrowhead was not found.\n\n" +
                "DayZ Mod Classic needs Arma 2 and Arma 2: Operation Arrowhead installed via Steam " +
                "(run each once so they register).\n\nOpen the Steam install page now?",
                "DayZ Mod Classic", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (pick == DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo("steam://install/33930") { UseShellExecute = true }); }
                catch (Exception ex) { Logger.Exception("steam install link", ex); }
            }
            return;
        }

        var wasState = _playState;
        SetPlayState(PlayState.Busy);
        _btnRefresh.Enabled = false;
        _progress.Visible = true;
        _progress.Fraction = 0;
        _installCts = new CancellationTokenSource();

        var progress = new Progress<InstallProgress>(p =>
        {
            _progress.Fraction = p.BytesTotal > 0 ? (double)p.BytesDone / p.BytesTotal : 0;
            _lblStatus.Text = p.Phase == "commit"
                ? $"Installing {p.CurrentFile} ({p.FileIndex}/{p.FileCount})..."
                : $"{p.CurrentFile}  ({p.FileIndex}/{p.FileCount})  {p.BytesPerSec / (1024 * 1024):F1} MB/s  {FormatMb(p.BytesDone)} / {FormatMb(p.BytesTotal)}";
        });

        try
        {
            await ModInstaller.InstallAsync(_config, _manifest, _updateCheck.ToDownload, _hashCache, progress, _installCts.Token);
            _updateCheck = _updateCheck with { State = InstallState.UpToDate, ToDownload = Array.Empty<ManifestFile>(), DownloadBytes = 0 };
            SetPlayState(PlayState.Play);
            UpdateHealth();
            // BeginInvoke queues behind any still-pending progress posts so the
            // final status text is not overwritten by a stale report.
            var doneText = $"Mod {_manifest.ModVersion} installed.";
            BeginInvoke(new Action(() => _lblStatus.Text = doneText));
        }
        catch (OperationCanceledException)
        {
            SetPlayState(wasState);
            _lblStatus.Text = "Download cancelled. Finished files are kept; click again to resume.";
        }
        catch (ModInstaller.InstallException ex)
        {
            Logger.Error($"install failed: {ex.Message}");
            SetPlayState(wasState);
            _lblStatus.Text = "Install failed.";
            MessageBox.Show(this, ex.Message, "DayZ Mod Classic", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            Logger.Exception("install failed", ex);
            SetPlayState(wasState);
            _lblStatus.Text = "Install failed.";
            MessageBox.Show(this, $"Unexpected error:\n{ex.Message}", "DayZ Mod Classic", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _installCts.Dispose();
            _installCts = null;
            _progress.Visible = false;
            _btnRefresh.Enabled = true;
        }
    }

    private void PlayAnyway()
    {
        if (!_config.PlayAnywayWarned)
        {
            var pick = MessageBox.Show(this,
                "Your mod files are out of date. If the server requires the new files, " +
                "you may be kicked at the lobby (signature check).\n\nPlay anyway?",
                "DayZ Mod Classic", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (pick != DialogResult.Yes) return;
            _config.PlayAnywayWarned = true;
            ConfigStore.Save(_config);
        }
        LaunchSelected();
    }

    private static string FormatMb(long bytes) => $"{bytes / (1024.0 * 1024.0):F1} MB";

    private void OfferDesktopShortcutOnce()
    {
        if (_config.ShortcutOffered) return;
        _config.ShortcutOffered = true;
        ConfigStore.Save(_config);

        try
        {
            var exe = Environment.ProcessPath;
            if (exe == null || ShortcutService.DesktopShortcutExists()) return;
            // Already living on the desktop: a shortcut would be redundant.
            if (exe.Contains(@"\Desktop\", StringComparison.OrdinalIgnoreCase)) return;

            var pick = MessageBox.Show(this,
                "Create a desktop shortcut for DayZ Mod Classic?",
                "DayZ Mod Classic", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (pick == DialogResult.Yes)
                ShortcutService.CreateDesktopShortcut(exe);
        }
        catch (Exception ex)
        {
            Logger.Exception("shortcut offer", ex);
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        try { _pingCts.Cancel(); } catch { }
        try { _installCts?.Cancel(); } catch { }
        _config.PlayerName = _tbName.Text;
        ConfigStore.Save(_config);
        _hashCache.Save();
    }

    // ---- Health ----
    private void UpdateHealth()
    {
        var h = GameLauncher.ComputeHealth(_config);
        SetStatus(_lblSteam, "Steam running", h.SteamRunning, h.SteamRunning ? "" : "(not detected)");
        SetStatus(_lblA2oa,  "Arma 2 OA installed", h.A2oaInstalled, h.A2oaPath ?? "(not found)");
        SetStatus(_lblMod,   "DayZ Mod Classic installed", h.ModInstalled, h.ModInstalled ? "" : $"(missing {GameLauncher.ModFolder}\\AddOns\\dayz.pbo)");
        SetStatus(_lblBe,    "BattlEye fix present", h.BattlEyeFixPresent, h.BattlEyeFixPresent ? "" : "(stock BE; needs Dwarden fix)");
    }

    private static void SetStatus(Label l, string label, bool ok, string detail)
    {
        l.ForeColor = ok ? Ui.Theme.Signal : Ui.Theme.Flare;
        var mark = ok ? "OK" : "X ";
        l.Text = $"[{mark}] {label}{(string.IsNullOrEmpty(detail) ? "" : ": " + detail)}";
    }

    // ---- Servers ----
    private async Task ReloadServersAsync()
    {
        _btnRefresh.Enabled = false;
        _lblStatus.Text = "Fetching server list...";

        try { _pingCts.Cancel(); } catch { }
        _pingCts = new CancellationTokenSource();

        var fetched = await ServerListService.FetchAsync(_config.ServersUrl, _pingCts.Token);
        _servers = ServerListService.Merge(fetched, _config.CustomServers);
        RebindList();

        _lblStatus.Text = $"Loaded {_servers.Count} server(s). Pinging...";
        _btnRefresh.Enabled = true;

        // Restore default selection
        TrySelectByName(_config.LastServer);

        // Fire-and-forget pings, updating UI from worker thread via Invoke.
        _ = Task.Run(async () =>
        {
            var token = _pingCts.Token;
            var tasks = _servers.Select(async s =>
            {
                var r = await ServerQuery.QueryAsync(s.Host, s.Port, 1000, token);
                s.PlayerCount = r.PlayerCount;
                s.MaxPlayers = r.MaxPlayers;
                s.PingMs = r.PingMs;
                s.QueryStatus = r.Status;
                if (!IsDisposed)
                {
                    try
                    {
                        Invoke(new Action(() => UpdateRowForServer(s)));
                    }
                    catch { /* form closed */ }
                }
            });
            try { await Task.WhenAll(tasks); } catch { }
            if (!IsDisposed)
            {
                try { Invoke(new Action(() => _lblStatus.Text = $"Loaded {_servers.Count} server(s).")); } catch { }
            }
        });
    }

    private void RebindList()
    {
        _lvServers.BeginUpdate();
        _lvServers.Items.Clear();
        foreach (var s in _servers)
        {
            var item = new ListViewItem(s.Name);
            item.SubItems.Add("...");
            item.SubItems.Add("...");
            item.SubItems.Add(s.Version);
            item.Tag = s;
            if (s.Custom) item.ForeColor = Ui.Theme.Rust;
            _lvServers.Items.Add(item);
        }
        _lvServers.EndUpdate();
    }

    private void UpdateRowForServer(ServerEntry s)
    {
        foreach (ListViewItem item in _lvServers.Items)
        {
            if (ReferenceEquals(item.Tag, s))
            {
                if (s.QueryStatus == "ok" && s.PlayerCount.HasValue)
                {
                    item.SubItems[1].Text = $"{s.PlayerCount}/{s.MaxPlayers}";
                    item.SubItems[2].Text = s.PingMs.HasValue ? $"{s.PingMs} ms" : "-";
                }
                else
                {
                    item.SubItems[1].Text = "-";
                    item.SubItems[2].Text = s.QueryStatus ?? "timeout";
                }
                return;
            }
        }
    }

    private void TrySelectByName(string name)
    {
        foreach (ListViewItem item in _lvServers.Items)
        {
            if (item.Tag is ServerEntry s && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
                return;
            }
        }
        if (_lvServers.Items.Count > 0)
        {
            _lvServers.Items[0].Selected = true;
            _lvServers.Items[0].Focused = true;
        }
    }

    private ServerEntry? GetSelectedServer()
    {
        if (_lvServers.SelectedItems.Count == 0) return null;
        return _lvServers.SelectedItems[0].Tag as ServerEntry;
    }

    private void SetSelectedAsDefault()
    {
        var s = GetSelectedServer();
        if (s == null) return;
        _config.LastServer = s.Name;
        ConfigStore.Save(_config);
        _lblStatus.Text = $"Default server: {s.Name}";
    }

    private void RemoveSelectedCustom()
    {
        var s = GetSelectedServer();
        if (s == null) return;
        if (!s.Custom)
        {
            MessageBox.Show(this, "Only custom servers can be removed.", "DayZ Mod Classic",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _config.CustomServers.RemoveAll(c =>
            string.Equals(c.Name, s.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.Host, s.Host, StringComparison.OrdinalIgnoreCase) &&
            c.Port == s.Port);
        ConfigStore.Save(_config);

        _servers.Remove(s);
        RebindList();
    }

    private void ShowAddServerDialog()
    {
        using var dlg = new AddServerDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var entry = new ServerEntry
        {
            Name = dlg.ServerName,
            Host = dlg.Host,
            Port = dlg.Port,
            Custom = true,
            Version = "?",
        };
        _config.CustomServers.Add(entry);
        ConfigStore.Save(_config);
        _servers.Add(entry);
        RebindList();
        TrySelectByName(entry.Name);
        _ = Task.Run(async () =>
        {
            var r = await ServerQuery.QueryAsync(entry.Host, entry.Port);
            entry.PlayerCount = r.PlayerCount;
            entry.MaxPlayers = r.MaxPlayers;
            entry.PingMs = r.PingMs;
            entry.QueryStatus = r.Status;
            if (!IsDisposed)
            {
                try { Invoke(new Action(() => UpdateRowForServer(entry))); } catch { }
            }
        });
    }

    // ---- Buttons ----
    private async void DoPlay()
    {
        switch (_playState)
        {
            case PlayState.Busy:
                try { _installCts?.Cancel(); } catch { }
                return;
            case PlayState.Install:
            case PlayState.Update:
                await RunInstallAsync();
                return;
            case PlayState.Play:
                LaunchSelected();
                return;
            default:
                return;
        }
    }

    private async void LaunchSelected()
    {
        var s = GetSelectedServer();
        if (s == null)
        {
            MessageBox.Show(this, "Select a server first.", "DayZ Mod Classic",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var name = _tbName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show(this, "Enter a player name first.", "DayZ Mod Classic",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            _tbName.Focus();
            return;
        }
        _config.PlayerName = name;
        _config.LastServer = s.Name;
        ConfigStore.Save(_config);

        _btnPlay.Enabled = false;
        _lblStatus.Text = $"Launching {s.Name}...";
        Logger.Info($"launch attempt server=\"{s.Name}\" addr={s.Host}:{s.Port}");
        try
        {
            await GameLauncher.LaunchAsync(_config, s, name);
            _lblStatus.Text = $"Launched. Connecting to {s.Host}:{s.Port}.";
            Logger.Info($"launch ok server=\"{s.Name}\"");
            WindowState = FormWindowState.Minimized;
        }
        catch (GameLauncher.LaunchException ex)
        {
            Logger.Error($"launch aborted: {ex.Message}");
            MessageBox.Show(this, ex.Message, "DayZ Mod Classic",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblStatus.Text = "Launch aborted.";
        }
        catch (Exception ex)
        {
            Logger.Exception("launch failed", ex);
            MessageBox.Show(this, $"Unexpected error:\n{ex}", "DayZ Mod Classic",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblStatus.Text = "Launch failed.";
        }
        finally
        {
            _btnPlay.Enabled = true;
        }
    }

    private void OpenRpt()
    {
        var rpt = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArmA 2 OA", "arma2oa.RPT");
        if (!File.Exists(rpt))
        {
            MessageBox.Show(this, $"RPT not found at:\n{rpt}", "DayZ Mod Classic",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{rpt}\"") { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "DayZ Mod Classic",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenModFolder()
    {
        if (string.IsNullOrEmpty(_config.A2oaPath) || !Directory.Exists(_config.A2oaPath))
        {
            MessageBox.Show(this, "Arma 2 OA path is not set.", "DayZ Mod Classic",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var mod = Path.Combine(_config.A2oaPath, GameLauncher.ModFolder);
        if (!Directory.Exists(mod))
        {
            // open the OA root anyway
            mod = _config.A2oaPath;
        }
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{mod}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "DayZ Mod Classic",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowAbout()
    {
        using var dlg = new AboutDialog(AppVersion);
        dlg.ShowDialog(this);
    }

    private void SaveDiagReport()
    {
        try
        {
            var h = GameLauncher.ComputeHealth(_config);
            Logger.Info($"diag report requested health={h}");
            var zipPath = DiagReport.Build(_config, h, AppVersion);
            Logger.Info($"diag report saved path=\"{zipPath}\"");

            var msg =
                "Saved diagnostic report to:" + Environment.NewLine + zipPath + Environment.NewLine + Environment.NewLine +
                "Contents: launcher logs, Arma RPT, scrubbed config, install diag (if present)," + Environment.NewLine +
                "and system info. Plain text inside the zip; review before sending." + Environment.NewLine + Environment.NewLine +
                "Open Explorer at that file now?";
            var pick = MessageBox.Show(this, msg, "DayZ Mod Classic",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (pick == DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zipPath}\"")
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { Logger.Exception("explorer open", ex); }
            }
        }
        catch (Exception ex)
        {
            Logger.Exception("SaveDiagReport", ex);
            MessageBox.Show(this, "Could not save diagnostic report: " + ex.Message,
                "DayZ Mod Classic", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
