using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DayZModClassic.Launcher.Ui;

// Palette mirrors the CSS variables on dayzmodclassic.com (website/index.html).
public static class Theme
{
    public static readonly Color Bg        = ColorTranslator.FromHtml("#0c0e0f");
    public static readonly Color Bg2       = ColorTranslator.FromHtml("#121516");
    public static readonly Color Panel     = ColorTranslator.FromHtml("#15181a");
    public static readonly Color Line      = ColorTranslator.FromHtml("#262b2c");
    public static readonly Color Ink       = ColorTranslator.FromHtml("#bdbab0");
    public static readonly Color InkBright = ColorTranslator.FromHtml("#e6e3d8");
    public static readonly Color Mute      = ColorTranslator.FromHtml("#6a6f6e");
    public static readonly Color Rust      = ColorTranslator.FromHtml("#c8602a");
    public static readonly Color RustDim   = ColorTranslator.FromHtml("#8a4520");
    public static readonly Color Signal    = ColorTranslator.FromHtml("#9aae5c");
    public static readonly Color Flare     = ColorTranslator.FromHtml("#df3322");

    public static void Apply(Form form)
    {
        form.BackColor = Bg;
        form.ForeColor = Ink;
        ApplyRecursive(form);
        EnableDarkTitleBar(form);
    }

    private static void ApplyRecursive(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case Button b:
                    StyleButton(b);
                    break;
                case TextBox tb:
                    tb.BackColor = Bg2;
                    tb.ForeColor = InkBright;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ListView lv:
                    lv.BackColor = Bg2;
                    lv.ForeColor = Ink;
                    lv.BorderStyle = BorderStyle.FixedSingle;
                    StyleListViewHeader(lv);
                    break;
                case LinkLabel ll:
                    ll.LinkColor = Rust;
                    ll.ActiveLinkColor = InkBright;
                    ll.VisitedLinkColor = Rust;
                    ll.BackColor = Color.Transparent;
                    break;
                case Label l:
                    l.BackColor = Color.Transparent;
                    break;
                case GroupBox g:
                    g.ForeColor = Mute;
                    break;
            }
            if (c.HasChildren) ApplyRecursive(c);
        }
    }

    public static void StyleButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Line;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = Panel;
        b.FlatAppearance.MouseDownBackColor = Line;
        b.BackColor = Bg2;
        b.ForeColor = Ink;
        b.UseVisualStyleBackColor = false;
    }

    public static void StylePrimary(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = RustDim;
        b.FlatAppearance.MouseDownBackColor = RustDim;
        b.BackColor = Rust;
        b.ForeColor = Color.FromArgb(0x0c, 0x0e, 0x0f);
        b.UseVisualStyleBackColor = false;
    }

    // ListView column headers ignore Back/ForeColor unless owner-drawn.
    private static void StyleListViewHeader(ListView lv)
    {
        lv.OwnerDraw = true;
        lv.DrawColumnHeader += (_, e) =>
        {
            using (var bg = new SolidBrush(Panel)) e.Graphics.FillRectangle(bg, e.Bounds);
            using (var pen = new Pen(Line)) e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", lv.Font,
                new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
                Mute, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        };
        lv.DrawItem += (_, e) => e.DrawDefault = true;
        lv.DrawSubItem += (_, e) => e.DrawDefault = true;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void EnableDarkTitleBar(Form form)
    {
        try
        {
            int on = 1;
            // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Win10 1809+: 19; 20 from 1903)
            _ = DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int));
        }
        catch { /* older Windows: keep light title bar */ }
    }
}
