using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DayZModClassic.Launcher.Ui;

// Banner strip across the top of the main window: darkened crop of the
// website hero image with the title drawn over it.
public sealed class HeaderPanel : Panel
{
    private readonly Image? _banner;
    private readonly string _version;

    public HeaderPanel(string version)
    {
        _version = version;
        Height = 64;
        Dock = DockStyle.Top;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        _banner = LoadBanner();
    }

    private static Image? LoadBanner()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("DayZModClassic.Launcher.Resources.banner.png");
            return stream == null ? null : Image.FromStream(stream);
        }
        catch { return null; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (_banner != null)
        {
            // Cover-fit: scale to fill, crop overflow.
            var scale = Math.Max((float)Width / _banner.Width, (float)Height / _banner.Height);
            var w = _banner.Width * scale;
            var h = _banner.Height * scale;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_banner, (Width - w) / 2f, (Height - h) / 2f, w, h);
        }
        else
        {
            using var bg = new SolidBrush(Theme.Bg2);
            g.FillRectangle(bg, ClientRectangle);
        }

        using var titleFont = new Font("Segoe UI", 15, FontStyle.Bold);
        using var verFont = new Font("Segoe UI", 8.5f);
        const string title = "DAYZ MOD CLASSIC";
        var titleSize = g.MeasureString(title, titleFont);
        g.DrawString(title, titleFont, Brushes.Black, 13, (Height - titleSize.Height) / 2f + 1);
        using (var ink = new SolidBrush(Theme.InkBright))
            g.DrawString(title, titleFont, ink, 12, (Height - titleSize.Height) / 2f);
        using (var mute = new SolidBrush(Theme.Ink))
            g.DrawString($"v{_version}", verFont, mute, 14 + titleSize.Width, (Height - titleSize.Height) / 2f + 7);

        using var accent = new Pen(Theme.Rust, 2);
        g.DrawLine(accent, 0, Height - 1, Width, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _banner?.Dispose();
        base.Dispose(disposing);
    }
}
