using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DayZModClassic.Launcher;

public class AboutDialog : Form
{
    private const string GitHubUrl = "https://github.com/dayzmodclassic"; // placeholder

    public AboutDialog(string version)
    {
        Text = "About DayZ Mod Classic";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 240);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 5,
        };

        var lblTitle = new Label
        {
            Text = "DayZ Mod Classic",
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 30,
        };
        var lblVersion = new Label
        {
            Text = $"Launcher version {version}",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 22,
        };
        var lblBlurb = new Label
        {
            Text = "A revival of the original DayZ Mod for Arma 2: Operation Arrowhead.\n" +
                   "Runs the classic 1.6-era zombies-and-sandbox experience on modern Windows.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 60,
        };

        var lnkRepo = new LinkLabel
        {
            Text = GitHubUrl,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 24,
        };
        lnkRepo.Links.Add(0, GitHubUrl.Length, GitHubUrl);
        lnkRepo.LinkClicked += (_, e) =>
        {
            try
            {
                if (e.Link?.LinkData is string url)
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        };

        var lblCredits = new Label
        {
            Text = "Credits: Dean \"Rocket\" Hall (original DayZ Mod), Bohemia Interactive,\n" +
                   "the DayZ Mod Team, and the Arma 2 community.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 50,
        };

        layout.Controls.Add(lblTitle);
        layout.Controls.Add(lblVersion);
        layout.Controls.Add(lblBlurb);
        layout.Controls.Add(lnkRepo);
        layout.Controls.Add(lblCredits);

        Controls.Add(layout);
    }
}
