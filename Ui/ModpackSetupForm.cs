using BAModManager.Core;
using static BAModManager.Core.Loc;

namespace BAModManager.Ui;

/// <summary>For modders: link a fresh Mod Launcher to their modpack's GitHub repo (saved as launcher.json).</summary>
internal sealed class ModpackSetupForm : Form
{
    private readonly TextBox _link;
    private readonly TextBox _testing;

    public string Link => _link.Text.Trim();

    /// <summary>Optional second repo players can switch to ("Testing").</summary>
    public string? TestingLink => string.IsNullOrWhiteSpace(_testing.Text) ? null : _testing.Text.Trim();

    public ModpackSetupForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = T("Set up modpack");
        Font = Theme.Ui(10f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(500, 390);
        Padding = new Padding(24, 20, 24, 18);

        var title = new Label { Text = T("Set up modpack"), Dock = DockStyle.Top, Height = 36, Font = Theme.Heading(15f) };
        var info = new Label
        {
            Text = T("For modders: paste your modpack's GitHub link. Its latest release needs a .bamod file attached. The link is saved in launcher.json next to the launcher - share both files with your players. Players can't change it."),
            Dock = DockStyle.Top,
            Height = 88,
            ForeColor = Theme.TextMuted,
        };
        var label = new Label { Text = T("GITHUB LINK"), Dock = DockStyle.Top, Height = 22, Font = Theme.Semibold(8.75f), ForeColor = Theme.TextMuted };
        var host = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.Surface, Padding = new Padding(10, 8, 10, 6) };
        _link = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            PlaceholderText = "https://github.com/you/yourmodpack",
        };
        host.Controls.Add(_link);

        var testingLabel = new Label { Text = T("TESTING VERSION - GITHUB LINK (optional)"), Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 12, 0, 0), Font = Theme.Semibold(8.75f), ForeColor = Theme.TextMuted };
        var testingHost = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.Surface, Padding = new Padding(10, 8, 10, 6) };
        _testing = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            PlaceholderText = "https://github.com/you/yourmodpack-testing",
        };
        testingHost.Controls.Add(_testing);
        var testingInfo = new Label { Text = T("Players can switch between Stable and Testing on the launcher."), Dock = DockStyle.Top, Height = 24, ForeColor = Theme.TextMuted };

        var save = Theme.MakeButton(T("Save"), ButtonKind.Primary);
        var cancel = Theme.MakeButton(T("Cancel"), ButtonKind.Ghost);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Background };
        buttons.Controls.AddRange(new Control[] { cancel, save });

        Controls.Add(testingInfo);
        Controls.Add(testingHost);
        Controls.Add(testingLabel);
        Controls.Add(host);
        Controls.Add(label);
        Controls.Add(info);
        Controls.Add(title);
        Controls.Add(buttons);

        save.Click += (_, _) =>
        {
            if (GitHubRepo.Parse(Link) == null || (TestingLink != null && GitHubRepo.Parse(TestingLink) == null))
            {
                MessageBox.Show(this, T("That isn't a GitHub link. It should look like github.com/owner/name."), Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        AcceptButton = save;
        CancelButton = cancel;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }
}
