using System;
using System.Drawing;
using System.Windows.Forms;

namespace DayZModClassic.Launcher;

public class AddServerDialog : Form
{
    private readonly TextBox _tbName = new() { Width = 220 };
    private readonly TextBox _tbHost = new() { Width = 220 };
    private readonly NumericUpDown _nudPort = new() { Minimum = 1, Maximum = 65535, Value = 2302, Width = 90 };

    public string ServerName => _tbName.Text.Trim();
    public string Host => _tbHost.Text.Trim();
    public int Port => (int)_nudPort.Value;

    public AddServerDialog()
    {
        Text = "Add custom server";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 170);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 4,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(_tbName, 1, 0);
        layout.Controls.Add(new Label { Text = "Host:", TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Dock = DockStyle.Fill }, 0, 1);
        layout.Controls.Add(_tbHost, 1, 1);
        layout.Controls.Add(new Label { Text = "Port:", TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Dock = DockStyle.Fill }, 0, 2);
        layout.Controls.Add(_nudPort, 1, 2);

        var btns = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var btnOk = new Button { Text = "Add", DialogResult = DialogResult.None, Width = 80 };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        btnOk.Click += (_, __) =>
        {
            if (string.IsNullOrWhiteSpace(ServerName) || string.IsNullOrWhiteSpace(Host))
            {
                MessageBox.Show(this, "Name and host are required.", "Add server",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        btns.Controls.Add(btnOk);
        btns.Controls.Add(btnCancel);
        layout.Controls.Add(btns, 0, 3);
        layout.SetColumnSpan(btns, 2);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(layout);
    }
}
