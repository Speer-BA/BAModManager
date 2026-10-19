using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text.RegularExpressions;
using BAModManager.Core;
using static BAModManager.Core.Loc;

namespace BAModManager.Ui;

/// <summary>
/// "Create mod package…": turns a folder laid out like the game folder (Mods\, UserData\…)
/// into a .bamod file with mod.json, a preview picture and an optional README.
/// </summary>
internal sealed class PackageCreatorForm : Form
{
    private static readonly string[] KnownRoots = { "Mods", "Plugins", "UserData", "UserLibs" };
    private static readonly string[] RootExtras = { "mod.json", "preview.png", "preview.jpg", "preview.jpeg", "README.md", "readme.txt" };

    private readonly TextBox _folder = Input();
    private readonly Label _folderInfo = Hint("");
    private readonly TextBox _name = Input();
    private readonly TextBox _id = Input();
    private readonly TextBox _version = Input("1.0.0");
    private readonly TextBox _author = Input();
    private readonly TextBox _gameVersion = Input();
    private readonly TextBox _website = Input();
    private readonly TextBox _description = Input(multiline: true);
    private readonly TextBox _requires = Input(multiline: true);
    private readonly PictureBox _picture = new()
    {
        Size = new Size(168, 168),
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Theme.Panel,
        Margin = new Padding(0, 0, 0, 8),
        Cursor = Cursors.Hand,
    };
    private readonly CheckBox _saveJson = new()
    {
        Text = T("Also save mod.json and preview.png into the folder (fills this form next time)"),
        Checked = true,
        AutoSize = true,
        ForeColor = Theme.TextMuted,
        Margin = new Padding(0, 6, 0, 0),
    };

    private string? _picturePath;
    private bool _idEdited;
    private string _defaultFolder = "";

    /// <summary>The package that was written, if any.</summary>
    public string? CreatedFile { get; private set; }

    public PackageCreatorForm(string? author = null)
    {
        Text = T("Create mod package");
        Font = Theme.Ui(9.5f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(860, 800);
        MinimumSize = new Size(760, 680);
        ShowInTaskbar = false;
        MinimizeBox = false;
        Padding = new Padding(24, 18, 24, 16);
        if (author != null) _author.Text = author;

        BuildLayout();

        _name.TextChanged += (_, _) => { if (!_idEdited) _id.Text = SuggestId(); };
        _author.TextChanged += (_, _) => { if (!_idEdited) _id.Text = SuggestId(); };
        _id.KeyPress += (_, _) => _idEdited = true;
        _picture.Click += (_, _) => ChoosePicture();
        SetPicture(null);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }

    // ───────────────────────── layout ─────────────────────────

    private static TextBox Input(string text = "", bool multiline = false) => new()
    {
        Text = text,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Theme.Surface,
        ForeColor = Theme.Text,
        Font = Theme.Ui(10f),
        Multiline = multiline,
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
        AcceptsReturn = multiline,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 0, 0, 10),
    };

    private static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.TextMuted,
        Font = Theme.Ui(8.75f),
        Margin = new Padding(0, 0, 0, 10),
        MaximumSize = new Size(560, 0),
    };

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.TextMuted,
        Font = Theme.Semibold(8.75f),
        Margin = new Padding(0, 0, 0, 3),
    };

    private void BuildLayout()
    {
        var title = new Label
        {
            Text = T("Create mod package"),
            Dock = DockStyle.Top,
            Height = 34,
            Font = Theme.Heading(16f),
        };
        var subtitle = new Label
        {
            Text = T("Packs a folder into a .bamod file. Players double-click it (or drop it on the Mod Manager) to install, and see your picture, description and version."),
            Dock = DockStyle.Top,
            Height = 42,
            ForeColor = Theme.TextMuted,
        };

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Background, AutoScroll = true };
        Theme.DarkScrollbars(grid);
        Theme.DarkScrollbars(_description);
        Theme.DarkScrollbars(_requires);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, BackColor = Theme.Background, Margin = new Padding(0, 0, 16, 0) };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        int row = 0;
        void Full(Control c, float height = 0)
        {
            left.RowStyles.Add(height > 0 ? new RowStyle(SizeType.Absolute, height) : new RowStyle(SizeType.AutoSize));
            left.Controls.Add(c, 0, row);
            left.SetColumnSpan(c, 2);
            row++;
        }
        void Pair(string l1, Control c1, string l2, Control c2)
        {
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.Controls.Add(FieldLabel(l1), 0, row);
            left.Controls.Add(FieldLabel(l2), 1, row);
            row++;
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            c1.Margin = new Padding(0, 0, 8, 10);
            left.Controls.Add(c1, 0, row);
            left.Controls.Add(c2, 1, row);
            row++;
        }

        // Folder row
        var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, BackColor = Theme.Background, Margin = new Padding(0) };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _folder.Margin = new Padding(0, 2, 8, 6);
        var browse = Theme.MakeButton(T("Browse…"));
        browse.Margin = new Padding(0);
        browse.Click += (_, _) => BrowseFolder();
        folderRow.Controls.Add(_folder, 0, 0);
        folderRow.Controls.Add(browse, 1, 0);

        Full(FieldLabel(T("MOD FOLDER")));
        Full(folderRow);
        Full(_folderInfo);
        Pair(T("NAME") + " *", _name, T("VERSION") + " *", _version);
        Pair(T("AUTHOR"), _author, "ID *  " + T("(unique, e.g. yourname.modname)"), _id);
        Pair(T("GAME VERSION"), _gameVersion, T("WEBSITE"), _website);
        Full(FieldLabel(T("DESCRIPTION")));
        _description.Height = 90;
        Full(_description, 100);
        Full(FieldLabel(T("REQUIRES  (one per line:  id  version  [download link])")));
        _requires.Height = 70;
        _requires.Font = new Font("Consolas", 9.5f);
        Full(_requires, 80);
        Full(Hint(T("Example:  author.othermod >=1.0 https://github.com/…/releases     ·     Version: \"1.2\" means 1.2 or newer, \"*\" any.")));
        Full(_saveJson);

        // Picture column
        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Background };
        right.Controls.Add(FieldLabel(T("PICTURE (1:1)")));
        right.Controls.Add(_picture);
        var choose = Theme.MakeButton(T("Choose…"));
        choose.Click += (_, _) => ChoosePicture();
        var clear = Theme.MakeButton(T("Default"), ButtonKind.Ghost);
        clear.Click += (_, _) => SetPicture(null);
        var picButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Background, Margin = new Padding(0) };
        picButtons.Controls.AddRange(new Control[] { choose, clear });
        right.Controls.Add(picButtons);
        right.Controls.Add(Hint(T("Square PNG/JPG. Saved as 512×512.\nWithout one, players see the default picture.")));

        grid.Controls.Add(left, 0, 0);
        grid.Controls.Add(right, 1, 0);

        // Bottom buttons
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, BackColor = Theme.Background, Padding = new Padding(0, 12, 0, 0) };
        var create = Theme.MakeButton(T("Create .bamod"), ButtonKind.Primary);
        create.Margin = new Padding(0);
        var cancel = Theme.MakeButton(T("Cancel"));
        cancel.Click += (_, _) => Close();
        create.Click += (_, _) => Create();
        bottom.Controls.Add(create);
        bottom.Controls.Add(cancel);
        CancelButton = cancel;

        Controls.Add(grid);
        Controls.Add(bottom);
        Controls.Add(subtitle);
        Controls.Add(title);
    }

    // ───────────────────────── behaviour ─────────────────────────

    /// <summary>Opens the form with a folder already chosen.</summary>
    public void UseFolder(string folder)
    {
        _defaultFolder = folder;
        LoadFolder(folder);
    }

    private string SuggestId()
    {
        static string Slug(string s) => Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "");
        var name = Slug(_name.Text);
        var author = Slug(_author.Text);
        return name.Length == 0 ? "" : author.Length > 0 ? $"{author}.{name}" : name;
    }

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("Choose your mod folder - laid out like the game folder (Mods\\, UserData\\…)"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (Directory.Exists(_folder.Text)) dlg.InitialDirectory = _folder.Text;
        else if (Directory.Exists(_defaultFolder)) dlg.InitialDirectory = _defaultFolder;
        if (dlg.ShowDialog(this) == DialogResult.OK) LoadFolder(dlg.SelectedPath);
    }

    private void LoadFolder(string folder)
    {
        _folder.Text = folder;
        if (!Directory.Exists(folder))
        {
            _folderInfo.Text = "";
            return;
        }

        // Existing mod.json fills the form
        var json = Path.Combine(folder, ModInfo.FileName);
        if (File.Exists(json))
        {
            try
            {
                var info = ModInfo.Parse(File.ReadAllText(json));
                _name.Text = info.Name ?? "";
                _id.Text = info.Id ?? "";
                _idEdited = true;
                _version.Text = info.Version ?? "1.0.0";
                _author.Text = info.Author ?? _author.Text;
                _gameVersion.Text = info.GameVersion ?? "";
                _website.Text = info.Website ?? "";
                _description.Text = (info.Description ?? "").Replace("\n", Environment.NewLine);
                _requires.Text = string.Join(Environment.NewLine, info.Requires.Select(d =>
                    $"{d.Id} {d.Version}{(d.Url != null ? " " + d.Url : "")}".Trim()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, T("The folder's mod.json couldn't be read:\n{0}", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else if (_name.Text.Length == 0)
        {
            _name.Text = Path.GetFileName(folder);
        }

        var preview = new[] { "preview.png", "preview.jpg", "preview.jpeg" }.Select(n => Path.Combine(folder, n)).FirstOrDefault(File.Exists);
        if (preview != null) SetPicture(preview);

        var files = PackFiles(folder);
        int inRoots = files.Count(f => KnownRoots.Contains(PathUtil.FirstSegment(f.Rel), StringComparer.OrdinalIgnoreCase));
        var readme = File.Exists(Path.Combine(folder, "README.md")) ? " + README.md" : "";
        _folderInfo.ForeColor = inRoots > 0 ? Theme.TextMuted : Theme.Accent;
        _folderInfo.Text = inRoots > 0
            ? T("{0} file(s) will be packed: {1}", files.Count, ModService.Describe(files.Select(f => f.Rel))) + readme
            : files.Count > 0
                ? "⚠  " + T("No Mods\\ or UserData\\ folder - {0} loose file(s) will be placed by type (.dll → Mods, .bundle → models).", files.Count)
                : "⚠  " + T("This folder is empty.");
    }

    /// <summary>Files to pack (relative paths), without the extras the form writes itself.</summary>
    private static List<(string Rel, string Full)> PackFiles(string folder)
    {
        return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(f => (Rel: Path.GetRelativePath(folder, f).Replace('\\', '/'), Full: f))
            .Where(f => !(f.Rel.IndexOf('/') < 0 && RootExtras.Contains(f.Rel, StringComparer.OrdinalIgnoreCase)))
            .Where(f => !f.Rel.EndsWith(".bamod", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Rel.EndsWith(PathUtil.DisabledSuffix, StringComparison.OrdinalIgnoreCase))
            .Where(f => !PathUtil.FirstSegment(f.Rel).StartsWith('.'))   // .git, .vs …
            .ToList();
    }

    private void ChoosePicture()
    {
        using var dlg = new OpenFileDialog
        {
            Title = T("Choose the mod's picture"),
            Filter = T("Pictures") + " (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) SetPicture(dlg.FileName);
    }

    private void SetPicture(string? path)
    {
        var old = _picture.Image;
        try
        {
            _picture.Image = new Bitmap(Previews.Large(path));
            _picturePath = path;
        }
        catch
        {
            _picture.Image = new Bitmap(Previews.Large(null));
            _picturePath = null;
        }
        old?.Dispose();
    }

    private bool TryBuildInfo(out ModInfo info)
    {
        info = new ModInfo();
        string? problem = null;
        if (!Directory.Exists(_folder.Text)) problem = T("Choose the mod folder.");
        else if (string.IsNullOrWhiteSpace(_name.Text)) problem = T("Give the mod a name.");
        else if (!Regex.IsMatch(_id.Text.Trim(), "^[a-z0-9][a-z0-9._-]*$"))
            problem = T("The ID may only use lower-case letters, numbers, dots, dashes and underscores (e.g. yourname.modname).");
        else if (!Regex.IsMatch(_version.Text.Trim(), @"^\d+(\.\d+)*([-+][0-9A-Za-z.-]+)?$"))
            problem = T("Use a version like 1.0.0.");
        else if (_website.Text.Trim().Length > 0 && !Uri.TryCreate(_website.Text.Trim(), UriKind.Absolute, out _))
            problem = T("The website must be a full link (https://…).");

        if (problem != null)
        {
            MessageBox.Show(this, problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        var requires = new List<DependencyRef>();
        foreach (var raw in _requires.Lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            string? url = null;
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count > 1 && parts[^1].StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                url = parts[^1];
                parts.RemoveAt(parts.Count - 1);
            }
            var dep = ModInfo.ParseDepLine(string.Join(' ', parts));
            dep.Url = url;
            requires.Add(dep);
        }

        static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        info = new ModInfo
        {
            Id = _id.Text.Trim(),
            Name = _name.Text.Trim(),
            Version = _version.Text.Trim(),
            Author = NullIfEmpty(_author.Text),
            Description = NullIfEmpty(_description.Text.Replace("\r\n", "\n")),
            GameVersion = NullIfEmpty(_gameVersion.Text),
            Website = NullIfEmpty(_website.Text),
            Requires = requires,
        };
        return true;
    }

    private void Create()
    {
        if (!TryBuildInfo(out var info)) return;
        var folder = _folder.Text;
        var files = PackFiles(folder);
        if (files.Count == 0 && info.Requires.Count == 0)
        {
            MessageBox.Show(this, T("There are no files to pack in this folder."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var safeName = Regex.Replace(info.Name!, @"[^\w\-. ]+", "").Trim().Replace(' ', '_');
        using var save = new SaveFileDialog
        {
            Title = T("Save mod package"),
            Filter = T("Broken Arrow mod package") + " (*.bamod)|*.bamod",
            FileName = $"{safeName}-{info.Version}.bamod",
            InitialDirectory = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder)) ?? folder,
            OverwritePrompt = true,
        };
        if (save.ShowDialog(this) != DialogResult.OK) return;
        var output = save.FileName;

        // Don't pack the output into itself
        if (Path.GetFullPath(output).StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            files = files.Where(f => !string.Equals(Path.GetFullPath(f.Full), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)).ToList();

        UseWaitCursor = true;
        try
        {
            var json = info.ToJson();
            byte[]? picture = null;
            if (_picturePath != null)
            {
                using var src = Image.FromFile(_picturePath);
                using var square = Previews.Square(src, 512);
                using var ms = new MemoryStream();
                square.Save(ms, ImageFormat.Png);
                picture = ms.ToArray();
            }
            var readme = Path.Combine(folder, "README.md");

            var tmp = output + ".tmp";
            using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                using (var w = new StreamWriter(zip.CreateEntry(ModInfo.FileName).Open()))
                    w.Write(json);
                if (picture != null)
                    using (var s = zip.CreateEntry("preview.png").Open())
                        s.Write(picture);
                if (File.Exists(readme)) zip.CreateEntryFromFile(readme, "README.md");
                foreach (var (rel, full) in files)
                    zip.CreateEntryFromFile(full, rel, CompressionLevel.Optimal);
            }
            File.Move(tmp, output, overwrite: true);

            if (_saveJson.Checked)
            {
                File.WriteAllText(Path.Combine(folder, ModInfo.FileName), json);
                if (picture != null) File.WriteAllBytes(Path.Combine(folder, "preview.png"), picture);
            }

            CreatedFile = output;
            UseWaitCursor = false;
            var answer = MessageBox.Show(this,
                T("Created {0}  ({1} file(s), {2:0.0} MB).\n\nShow it in Explorer?", Path.GetFileName(output), files.Count, new FileInfo(output).Length / 1024.0 / 1024.0),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{output}\"") { UseShellExecute = true })?.Dispose();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            UseWaitCursor = false;
            try { File.Delete(output + ".tmp"); } catch { /* ignore */ }
            MessageBox.Show(this, T("Couldn't create the package:\n\n{0}", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
