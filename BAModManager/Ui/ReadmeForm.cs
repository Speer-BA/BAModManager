using static BAModManager.Core.Loc;
namespace BAModManager.Ui;

/// <summary>Simple dark window that shows a mod's README.</summary>
internal sealed class ReadmeForm : Form
{
    public ReadmeForm(string modName, string text)
    {
        Text = $"{modName} - {T("Read me")}";
        Font = Theme.Ui(10f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 620);
        MinimumSize = new Size(420, 320);
        ShowInTaskbar = false;
        MinimizeBox = false;
        Padding = new Padding(18, 16, 18, 14);

        var title = new Label
        {
            Text = modName,
            Dock = DockStyle.Top,
            Height = 40,
            Font = Theme.Heading(15f),
            ForeColor = Theme.Text,
        };

        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Padding = new Padding(14, 12, 6, 12) };
        var box = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            Font = Theme.Ui(10f),
            Text = text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine),
        };
        Theme.DarkScrollbars(box);
        host.Controls.Add(box);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 12, 0, 0), BackColor = Theme.Background };
        var close = Theme.MakeButton(T("Close"), ButtonKind.Primary);
        close.Margin = new Padding(0);
        close.Click += (_, _) => Close();
        bottom.Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;

        Controls.Add(host);
        Controls.Add(bottom);
        Controls.Add(title);
        Shown += (_, _) => { box.SelectionStart = 0; box.SelectionLength = 0; close.Focus(); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }
}
