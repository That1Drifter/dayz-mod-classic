using System;
using System.Drawing;
using System.Windows.Forms;

namespace DayZModClassic.Launcher.Ui;

// Stock WinForms ProgressBar ignores BackColor/ForeColor under visual styles,
// so dark theming needs an owner-drawn bar.
public sealed class FlatProgressBar : Control
{
    private double _fraction;

    public Color TrackColor { get; set; } = Color.FromArgb(0x12, 0x15, 0x16);
    public Color FillColor { get; set; } = Color.FromArgb(0xc8, 0x60, 0x2a);
    public Color BorderColor { get; set; } = Color.FromArgb(0x26, 0x2b, 0x2c);

    public FlatProgressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 10;
    }

    // 0.0 .. 1.0
    public double Fraction
    {
        get => _fraction;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(clamped - _fraction) < 0.0005) return;
            _fraction = clamped;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var track = new SolidBrush(TrackColor))
            g.FillRectangle(track, ClientRectangle);
        var fillWidth = (int)Math.Round((Width - 2) * _fraction);
        if (fillWidth > 0)
        {
            using var fill = new SolidBrush(FillColor);
            g.FillRectangle(fill, 1, 1, fillWidth, Height - 2);
        }
        using var border = new Pen(BorderColor);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }
}
