using System.ComponentModel;
using BAModManager.Core;
using BAModManager.Ui;
using static BAModManager.Core.Loc;

namespace BAModManager;

/// <summary>Builds and refreshes the four pages.</summary>
public sealed partial class MainForm
{
    // Mods page
    private DataGridView _grid = null!;
    private Label _modsSummary = null!;
    private TextBox _search = null!;
    private Label _emptyState = null!;
    private readonly DropOverlay _dropOverlay = new();
    private readonly ThinScrollBar _gridScroll = new() { Dock = DockStyle.Right };
    private Button _addBtn = null!;
    private Button _addFolderBtn = null!;
    private Button _refreshBtn = null!;
    private Button _createBtn = null!;
    private Label _assocStatus = null!;
    private ContextMenuStrip _rowMenu = null!;
    private ToolStripMenuItem _menuToggle = null!;

    // Details pane
    private PictureBox _dPicture = null!;
    private readonly Dictionary<string, string?> _previewPaths = new();
    private Control _dBody = null!;
    private Label _dEmpty = null!;
    private Label _dTitle = null!;
    private Label _dDescription = null!;
    private Label _dVersion = null!;
    private Label _dAuthor = null!;
    private LinkLabel _dWebsite = null!;
    private Label _dDepsHeader = null!;
    private FlowLayoutPanel _dDeps = null!;
    private Button _dReadme = null!;
    private TableLayoutPanel _dActions = null!;
    private readonly Dictionary<Control, Label> _infoKeys = new();
    private readonly ToolTip _dTips = new();
    private Label _dClose = null!;
    private Label _dSub = null!;
    private Label _dStatus = null!;
    private Label _dLocation = null!;
    private Label _dInstalled = null!;
    private Label _dSource = null!;
    private Label _dFilesHeader = null!;
    private FileListView _dFiles = null!;
    private Button _dToggle = null!;
    private Button _dShow = null!;
    private Button _dRemove = null!;

    // MelonLoader page
    private Label _mlGlyph = null!;
    private Label _mlTitle = null!;
    private Label _mlSub = null!;
    private Button _mlInstall = null!;
    private Button _mlZip = null!;
    private Button _mlUninstall = null!;
    private Button _mlLog = null!;
    private Button _mlFolder = null!;

    // Settings page
    private TextBox _gamePath = null!;
    private Label _gameStatus = null!;
    private Button _browse = null!;
    private Button _detect = null!;
    private readonly List<Button> _folderButtons = new();
    private TableLayoutPanel _rulesTable = null!;
    private readonly List<(TextBox Ext, TextBox Folder)> _ruleRows = new();
    private TextBox _advSteam = null!;
    private TextBox _advRepo = null!;
    private TextBox _advAsset = null!;
    private Label _mlInfoSource = null!;

    // Log page
    private TextBox _log = null!;

    private const int ColToggle = 0;
    private const int ColPreview = 1;
    private const int ColName = 2;

    // ───────────────────────── small builders ─────────────────────────

    private static Label MakeLabel(string text, float size = 9.5f, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Theme.Ui(size),
        ForeColor = color ?? Theme.Text,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 4),
    };

    private static Label MakeHeading(string text, float size) => new()
    {
        Text = text,
        AutoSize = true,
        Font = size >= 14f ? Theme.Heading(size) : Theme.Semibold(size),
        ForeColor = Theme.Text,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 4),
    };

    private static FlowLayoutPanel ButtonRow() => new()
    {
        AutoSize = true,
        WrapContents = true,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 12, 0, 0),
    };

    /// <summary>A scrolling page with a vertical stack of cards.</summary>
    private static (Panel page, TableLayoutPanel stack) MakeStackPage(string title, string subtitle)
    {
        var page = new Panel { BackColor = Theme.Background, AutoScroll = true, Padding = new Padding(32, 26, 32, 24) };
        Theme.DarkScrollbars(page);
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Theme.Background,
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.Controls.Add(stack);

        AddToStack(stack, MakeHeading(title, 20f));
        var sub = MakeLabel(subtitle, 10f, Theme.TextMuted);
        sub.Margin = new Padding(1, 0, 0, 20);
        AddToStack(stack, sub);
        return (page, stack);
    }

    private static void AddToStack(TableLayoutPanel stack, Control c)
    {
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(c, 0, stack.RowStyles.Count - 1);
    }

    private static (Panel card, FlowLayoutPanel body) MakeCard(string title, string? subtitle = null)
    {
        var card = new Panel
        {
            BackColor = Theme.Panel,
            Padding = new Padding(22, 18, 22, 20),
            Margin = new Padding(0, 0, 0, 14),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        var body = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            BackColor = Theme.Panel,
        };
        body.Controls.Add(MakeHeading(title, 12f));
        if (subtitle != null)
        {
            var s = MakeLabel(subtitle, 9.5f, Theme.TextMuted);
            s.MaximumSize = new Size(720, 0);
            body.Controls.Add(s);
        }
        card.Controls.Add(body);
        return (card, body);
    }

    /// <summary>Flat dark text box with an optional icon, wrapped in a padded panel.</summary>
    private static (Panel host, TextBox box) MakeInput(int width, string placeholder, string? glyph = null)
    {
        var host = new Panel
        {
            Size = new Size(width, 36),
            BackColor = Theme.Surface,
            Padding = new Padding(glyph != null ? 4 : 10, 9, 8, 4),
            Margin = new Padding(0, 0, 8, 0),
        };
        var box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.Ui(10f),
            PlaceholderText = placeholder,
        };
        host.Controls.Add(box);
        if (glyph != null)
        {
            host.Controls.Add(new Label
            {
                Text = glyph,
                Font = Theme.Icons(10f),
                ForeColor = Theme.TextMuted,
                Dock = DockStyle.Left,
                Width = 28,
                TextAlign = ContentAlignment.TopCenter,
                Padding = new Padding(0, 1, 0, 0),
            });
        }
        host.Click += (_, _) => box.Focus();
        return (host, box);
    }

    /// <summary>Square icon-only button (Segoe MDL2 glyph).</summary>
    private static Button IconButton(string glyph, Color back, Color hover, Color fore)
    {
        var b = new RoundButton
        {
            Text = glyph,
            Font = Theme.Icons(12f),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = fore,
            Dock = DockStyle.Fill,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = hover;
        b.FlatAppearance.MouseDownBackColor = hover;
        return b;
    }

    private RadioButton MakeFilter(string text, string key)
    {
        var rb = new RadioButton
        {
            Text = text,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            MinimumSize = new Size(0, 36),
            Padding = new Padding(10, 0, 10, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 4, 0),
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Background,
            Cursor = Cursors.Hand,
            Font = Theme.Ui(9.5f),
        };
        rb.FlatAppearance.BorderSize = 0;
        rb.FlatAppearance.CheckedBackColor = Theme.Surface;
        rb.FlatAppearance.MouseOverBackColor = Theme.SurfaceHover;
        rb.CheckedChanged += (_, _) =>
        {
            rb.ForeColor = rb.Checked ? Theme.Text : Theme.TextMuted;
            if (!rb.Checked) return;
            _filter = key;
            PopulateGrid();
        };
        return rb;
    }

    // ───────────────────────── Mods page ─────────────────────────

    private void BuildModsPage()
    {
        var page = new Panel { BackColor = Theme.Background };

        // Header: title + summary, actions on the right
        var header = new Panel { Dock = DockStyle.Top, Height = 78, Padding = new Padding(32, 24, 32, 6), BackColor = Theme.Background };
        var titles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Background };
        titles.Controls.Add(MakeHeading(T("Mods"), 20f));
        _modsSummary = MakeLabel("", 9.5f, Theme.TextMuted);
        _modsSummary.Margin = new Padding(1, 0, 0, 0);
        titles.Controls.Add(_modsSummary);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Background, Padding = new Padding(0, 8, 0, 0) };
        _addBtn = Theme.MakeButton("+  " + T("Add mods"), ButtonKind.Primary);
        _addFolderBtn = Theme.MakeButton(T("Add folder"));
        _createBtn = Theme.MakeButton(T("Create package…"), ButtonKind.Ghost);
        _refreshBtn = Theme.MakeButton(T("Refresh"), ButtonKind.Ghost);
        _refreshBtn.Margin = new Padding(0);
        actions.Controls.AddRange(new Control[] { _addBtn, _addFolderBtn, _createBtn, _refreshBtn });
        header.Controls.Add(titles);
        header.Controls.Add(actions);

        // Toolbar: search + filters
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(32, 6, 32, 10), BackColor = Theme.Background };
        var toolFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Theme.Background };
        var (searchHost, search) = MakeInput(300, T("Search mods…"), Theme.GlyphSearch);
        _search = search;
        searchHost.Margin = new Padding(0, 0, 16, 0);
        var fAll = MakeFilter(T("All"), "all");
        var fOn = MakeFilter(T("Enabled"), "on");
        var fOff = MakeFilter(T("Disabled"), "off");
        toolFlow.Controls.AddRange(new Control[] { searchHost, fAll, fOn, fOff });
        toolbar.Controls.Add(toolFlow);

        // Body: grid + details
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(32, 0, 32, 20), BackColor = Theme.Background };
        var details = BuildDetailsPane();
        var gap = new Panel { Dock = DockStyle.Right, Width = 16, BackColor = Theme.Background };
        var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Padding = new Padding(1) };

        _grid = BuildGrid();
        _emptyState = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Panel,
            Font = Theme.Ui(11f),
            Visible = false,
        };
        // The slim scrollbar has its own lane on the right, so the list's width never changes
        // (the stock scrollbar is off; showing/hiding it used to shift every column).
        _gridScroll.Width = LogicalToDeviceUnits(14);
        gridHost.Controls.Add(_grid);
        gridHost.Controls.Add(_emptyState);
        gridHost.Controls.Add(_dropOverlay);
        gridHost.Controls.Add(_gridScroll);
        _grid.MouseWheel += (_, e) =>
        {
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
            ScrollGridBy(-Math.Sign(e.Delta));
        };
        _grid.SelectionChanged += (_, _) => UpdateGridScroll();
        _gridScroll.ValueChanged += (_, _) =>
        {
            if (_grid.Rows.Count > 0 && _gridScroll.Value < _grid.Rows.Count)
                _grid.FirstDisplayedScrollingRowIndex = _gridScroll.Value;
        };
        _grid.Scroll += (_, _) => UpdateGridScroll();
        _grid.Resize += (_, _) => UpdateGridScroll();
        _grid.RowsRemoved += (_, _) => UpdateGridScroll();

        body.Controls.Add(gridHost);
        body.Controls.Add(gap);
        body.Controls.Add(details);

        page.Controls.Add(body);
        page.Controls.Add(toolbar);
        page.Controls.Add(header);
        AddPage("mods", page);

        // Events
        fAll.Checked = true;
        _search.TextChanged += (_, _) => PopulateGrid();
        _addBtn.Click += async (_, _) => await AddFiles();
        _addFolderBtn.Click += async (_, _) => await AddFolder();
        _refreshBtn.Click += (_, _) => RefreshAll();
        _createBtn.Click += (_, _) => CreatePackage();
        _emptyState.Click += async (_, _) => { if (GameValid && _entries.Count == 0) await AddFiles(); };
    }

    private DataGridView BuildGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Theme.Panel,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Theme.Border,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 38,
            StandardTab = true,
            ScrollBars = ScrollBars.None,
        };
        grid.RowTemplate.Height = LogicalToDeviceUnits(88);
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Panel,
            ForeColor = Theme.TextMuted,
            SelectionBackColor = Theme.Panel,
            SelectionForeColor = Theme.TextMuted,
            Font = Theme.Ui(8.75f),
            Padding = new Padding(10, 0, 0, 0),
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.SurfaceHover,
            SelectionForeColor = Theme.Text,
            Font = Theme.Ui(9.75f),
            Padding = new Padding(10, 0, 8, 0),
            WrapMode = DataGridViewTriState.False,
        };

        var toggle = new DataGridViewTextBoxColumn
        {
            HeaderText = "",
            Name = "On",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = LogicalToDeviceUnits(70),
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.False,
        };
        grid.Columns.Add(toggle);
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "",
            Name = "Preview",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = LogicalToDeviceUnits(92),
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.False,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = T("NAME"),
            Name = "Name",
            FillWeight = 36,
            DefaultCellStyle = { Font = Theme.Semibold(10.5f) },
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("TYPE"), Name = "Type", FillWeight = 20 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("LOCATION"), Name = "Location", FillWeight = 26 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("SOURCE"), Name = "Source", FillWeight = 11 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("STATUS"), Name = "Status", FillWeight = 12 });

        // Draw the on/off switch
        grid.CellPainting += (_, e) =>
        {
            if (e.ColumnIndex == ColPreview && e.RowIndex >= 0 && e.Graphics != null)
            {
                e.PaintBackground(e.CellBounds, true);
                if (grid.Rows[e.RowIndex].Tag is ModEntry pm)
                {
                    var img = Previews.Small(PreviewPathFor(pm));
                    int size = img.Width;
                    var r = new Rectangle(e.CellBounds.X + (e.CellBounds.Width - size) / 2,
                                          e.CellBounds.Y + (e.CellBounds.Height - size) / 2, size, size);
                    Previews.DrawRounded(e.Graphics, img, r, 6 * DeviceDpi / 96f);
                    if (!pm.Enabled)
                        using (var dim = new SolidBrush(Color.FromArgb(120, Theme.Panel)))
                            e.Graphics.FillRectangle(dim, r);
                }
                e.Handled = true;
                return;
            }
            if (e.ColumnIndex == ColName && e.RowIndex >= 0 && e.Graphics != null &&
                grid.Rows[e.RowIndex].Tag is ModEntry nm &&
                (SubLine(nm) is { Length: > 0 } || DescriptionLine(nm) is { Length: > 0 }))
            {
                var sub = SubLine(nm);
                var desc = DescriptionLine(nm);
                e.PaintBackground(e.CellBounds, true);
                bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
                var style = e.CellStyle!;
                var fore = selected ? style.SelectionForeColor : style.ForeColor;
                var pad = style.Padding;
                var area = new Rectangle(e.CellBounds.X + pad.Left, e.CellBounds.Y, e.CellBounds.Width - pad.Horizontal, e.CellBounds.Height);
                const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                var titleH = TextRenderer.MeasureText(e.Graphics, "Hg", style.Font, Size.Empty, flags).Height;
                using var subFont = Theme.Ui(9f);
                var subH = TextRenderer.MeasureText(e.Graphics, "Hg", subFont, Size.Empty, flags).Height;
                int lines = (sub.Length > 0 ? 1 : 0) + (desc.Length > 0 ? 1 : 0);
                int top = area.Y + (area.Height - titleH - lines * (subH + 2)) / 2;
                TextRenderer.DrawText(e.Graphics, nm.Name, style.Font, new Rectangle(area.X, top, area.Width, titleH), fore, flags);
                int y = top + titleH + 2;
                if (sub.Length > 0)
                {
                    TextRenderer.DrawText(e.Graphics, sub, subFont, new Rectangle(area.X, y, area.Width, subH), Theme.TextMuted, flags);
                    y += subH + 2;
                }
                if (desc.Length > 0)
                {
                    var descColor = selected ? Color.FromArgb(0xB4, 0xB8, 0xBE) : Color.FromArgb(0xA4, 0xA8, 0xAE);
                    TextRenderer.DrawText(e.Graphics, desc, subFont, new Rectangle(area.X, y, area.Width, subH), nm.Enabled ? descColor : Theme.TextMuted, flags);
                }
                e.Handled = true;
                return;
            }
            if (e.ColumnIndex != ColToggle || e.RowIndex < 0 || e.Graphics == null) return;
            e.PaintBackground(e.CellBounds, true);
            var on = grid.Rows[e.RowIndex].Tag is ModEntry m && m.Enabled;
            Theme.DrawToggle(e.Graphics, e.CellBounds, on, DeviceDpi / 96f);
            e.Handled = true;
        };

        grid.CellMouseClick += async (_, e) =>
        {
            if (e.RowIndex < 0 || e.Button != MouseButtons.Left || e.ColumnIndex != ColToggle) return;
            if (grid.Rows[e.RowIndex].Tag is ModEntry m) await ToggleEntries(new List<ModEntry> { m }, !m.Enabled);
        };
        grid.CellMouseEnter += (_, e) =>
        {
            grid.Cursor = e.ColumnIndex == ColToggle && e.RowIndex >= 0 ? Cursors.Hand : Cursors.Default;
        };
        grid.CellMouseDown += (_, e) =>
        {
            // Right-click selects the row under the mouse before the menu opens
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            if (!grid.Rows[e.RowIndex].Selected)
            {
                grid.ClearSelection();
                grid.Rows[e.RowIndex].Selected = true;
            }
        };
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex != ColToggle) ShowSelectedInFolder();
        };
        grid.SelectionChanged += (_, _) =>
        {
            UpdateDetails();
            UpdateButtons();
        };
        grid.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                await RemoveSelected();
            }
            else if (e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                await ToggleSelected();
            }
            else if (e.KeyCode == Keys.F5)
            {
                RefreshAll();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                ClearSelection();
            }
        };
        grid.Sorted += (_, _) => grid.ClearSelection();
        grid.MouseDown += (_, e) =>
        {
            // Clicking the empty area below the last mod deselects
            if (grid.HitTest(e.X, e.Y).Type == DataGridViewHitTestType.None) ClearSelection();
        };

        // Right-click menu
        _rowMenu = new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()),
            ShowImageMargin = false,
            Font = Theme.Ui(9.5f),
        };
        _menuToggle = new ToolStripMenuItem(T("Disable"), null, async (_, _) => await ToggleSelected()) { ForeColor = Theme.Text };
        var show = new ToolStripMenuItem(T("Show in folder"), null, (_, _) => ShowSelectedInFolder()) { ForeColor = Theme.Text };
        var readme = new ToolStripMenuItem(T("Read me"), null, (_, _) => ShowReadme()) { ForeColor = Theme.Text };
        var remove = new ToolStripMenuItem(T("Remove…"), null, async (_, _) => await RemoveSelected()) { ForeColor = Theme.Bad };
        _rowMenu.Items.AddRange(new ToolStripItem[] { _menuToggle, readme, show, new ToolStripSeparator(), remove });
        _rowMenu.Opening += (_, e) =>
        {
            var sel = SelectedEntries();
            if (sel.Count == 0 || _busy) { e.Cancel = true; return; }
            _menuToggle.Text = sel.All(x => x.Enabled) ? T("Disable") : T("Enable");
            readme.Visible = sel.Count == 1 && GameValid && ModService.ReadmePath(GameDir, sel[0]) != null;
        };
        grid.ContextMenuStrip = _rowMenu;

        return grid;
    }

    private Panel BuildDetailsPane()
    {
        var pane = new Panel { Dock = DockStyle.Right, Width = 340, BackColor = Theme.Panel, Padding = new Padding(20, 18, 8, 18) };

        _dEmpty = new Label
        {
            Dock = DockStyle.Fill,
            Text = T("Select a mod to see its details"),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Panel,
        };

        const int textWidth = 290;

        // Scrolling part: picture, title, description, info, requirements, files
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Panel, Padding = new Padding(0, 0, 12, 0) };
        Theme.DarkScrollbars(scroll);
        var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, BackColor = Theme.Panel };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Controls.Add(t);

        void Row(Control c)
        {
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(c, 0, t.RowStyles.Count - 1);
        }

        _dPicture = new PictureBox
        {
            Size = new Size(128, 128),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Theme.Panel,
            Margin = new Padding(0, 0, 0, 16),
            Anchor = AnchorStyles.Top,
        };

        _dTitle = MakeHeading("", 14f);
        _dTitle.MaximumSize = new Size(textWidth, 0);
        _dSub = MakeLabel("", 9.5f, Theme.TextMuted);
        _dSub.MaximumSize = new Size(textWidth, 0);
        _dStatus = MakeLabel("", 9.5f, Theme.Good);
        _dStatus.Font = Theme.Semibold(9.5f);
        _dStatus.MaximumSize = new Size(textWidth, 0);
        _dStatus.Margin = new Padding(0, 4, 0, 12);
        _dDescription = MakeLabel("", 9.5f, Theme.Text);
        _dDescription.MaximumSize = new Size(textWidth, 0);
        _dDescription.Margin = new Padding(0, 0, 0, 14);

        var info = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, BackColor = Theme.Panel, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Label InfoKey(string k) => new() { Text = k.ToUpperInvariant(), AutoSize = true, ForeColor = Theme.TextMuted, Font = Theme.Semibold(8.25f), Margin = new Padding(0, 2, 16, 6) };
        Label InfoValue() => new() { AutoSize = true, ForeColor = Theme.Text, MaximumSize = new Size(200, 0), Margin = new Padding(0, 0, 0, 6) };
        _dVersion = InfoValue();
        _dAuthor = InfoValue();
        _dLocation = InfoValue();
        _dInstalled = InfoValue();
        _dSource = InfoValue();
        _dWebsite = new LinkLabel
        {
            AutoSize = true,
            MaximumSize = new Size(200, 0),
            Margin = new Padding(0, 0, 0, 6),
            LinkColor = Theme.Accent,
            ActiveLinkColor = Theme.AccentHover,
            VisitedLinkColor = Theme.Accent,
            LinkBehavior = LinkBehavior.HoverUnderline,
        };
        _dWebsite.LinkClicked += (_, _) => { if (_dWebsite.Tag is string url) OpenUrl(url); };
        int r = 0;
        foreach (var (key, value) in new (string, Control)[]
                 {
                     (T("Version"), _dVersion), (T("Author"), _dAuthor), (T("Location"), _dLocation),
                     (T("Installed"), _dInstalled), (T("From"), _dSource), (T("Website"), _dWebsite),
                 })
        {
            var keyLabel = InfoKey(key);
            _infoKeys[value] = keyLabel;
            info.Controls.Add(keyLabel, 0, r);
            info.Controls.Add(value, 1, r);
            r++;
        }

        _dDepsHeader = MakeLabel(T("REQUIREMENTS"), 8.5f, Theme.TextMuted);
        _dDepsHeader.Font = Theme.Semibold(8.5f);
        _dDeps = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Theme.Panel,
            Margin = new Padding(0, 2, 0, 14),
        };

        _dFilesHeader = MakeLabel(T("FILES"), 8.5f, Theme.TextMuted);
        _dFilesHeader.Font = Theme.Semibold(8.5f);
        _dFiles = new FileListView
        {
            Width = textWidth,
            Height = 110,
            Margin = new Padding(0, 2, 0, 8),
        };

        Row(_dPicture);
        Row(_dTitle);
        Row(_dSub);
        Row(_dStatus);
        Row(_dDescription);
        Row(info);
        Row(_dDepsHeader);
        Row(_dDeps);
        Row(_dFilesHeader);
        Row(_dFiles);

        // Fixed part at the bottom: actions
        // Action bar at the bottom: [ Disable ............ ][📄][📁][🗑]
        int barH = LogicalToDeviceUnits(40);
        int iconW = LogicalToDeviceUnits(40);
        int gap = LogicalToDeviceUnits(8);

        var actions = new Panel { Dock = DockStyle.Bottom, Height = barH + LogicalToDeviceUnits(17), BackColor = Theme.Panel, Padding = new Padding(0, 0, 12, 0) };
        var separator = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };

        _dActions = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = barH,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Theme.Panel,
            Margin = new Padding(0),
        };
        _dActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _dActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, iconW + gap));
        _dActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, iconW + gap));
        _dActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, iconW + gap));
        _dActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _dToggle = Theme.MakeButton(T("Disable"), ButtonKind.Primary);
        _dToggle.Font = Theme.Semibold(10.5f);
        _dReadme = IconButton(Theme.GlyphReadme, Theme.Surface, Theme.SurfaceHover, Theme.Text);
        _dShow = IconButton(Theme.GlyphFolder, Theme.Surface, Theme.SurfaceHover, Theme.Text);
        _dRemove = IconButton(Theme.GlyphDelete, Color.FromArgb(0x33, 0x26, 0x27), Color.FromArgb(0x44, 0x2A, 0x2B), Theme.Bad);
        _dToggle.AutoSize = false;
        _dToggle.MinimumSize = Size.Empty;
        _dToggle.Dock = DockStyle.Fill;
        _dToggle.Margin = new Padding(0);
        foreach (var b in new[] { _dReadme, _dShow, _dRemove }) b.Margin = new Padding(gap, 0, 0, 0);

        _dTips.SetToolTip(_dReadme, T("Read me"));
        _dTips.SetToolTip(_dShow, T("Show in folder"));
        _dTips.SetToolTip(_dRemove, T("Remove"));

        _dActions.Controls.Add(_dToggle, 0, 0);
        _dActions.Controls.Add(_dReadme, 1, 0);
        _dActions.Controls.Add(_dShow, 2, 0);
        _dActions.Controls.Add(_dRemove, 3, 0);

        actions.Controls.Add(_dActions);
        actions.Controls.Add(separator);

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel };
        body.Controls.Add(scroll);
        body.Controls.Add(actions);
        _dBody = body;

        pane.Controls.Add(body);
        pane.Controls.Add(_dEmpty);

        _dClose = new Label
        {
            Text = "✕",
            Size = new Size(28, 28),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Panel,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pane.Width - 28 - 6, 6),
            Visible = false,
        };
        _dClose.MouseEnter += (_, _) => _dClose.ForeColor = Theme.Text;
        _dClose.MouseLeave += (_, _) => _dClose.ForeColor = Theme.TextMuted;
        _dClose.Click += (_, _) => ClearSelection();
        new ToolTip().SetToolTip(_dClose, T("Deselect (Esc)"));
        pane.Controls.Add(_dClose);
        _dClose.BringToFront();

        _dToggle.Click += async (_, _) => await ToggleSelected();
        _dReadme.Click += (_, _) => ShowReadme();
        _dShow.Click += (_, _) => ShowSelectedInFolder();
        _dRemove.Click += async (_, _) => await RemoveSelected();
        return pane;
    }

    private static string Key(ModEntry e) => ModService.Key(e);

    /// <summary>Requirement problems per mod (filled by PopulateGrid).</summary>
    private readonly Dictionary<string, List<DepIssue>> _issues = new();

    private List<DepIssue> IssuesOf(ModEntry e) =>
        _issues.TryGetValue(Key(e), out var list) ? list : new List<DepIssue>();

    private (string text, Color color) StatusOf(ModEntry e)
    {
        if (e.MissingFiles > 0) return (T("{0} file(s) missing", e.MissingFiles), Theme.Bad);
        if (!e.Enabled) return (T("Disabled"), Theme.TextMuted);
        var issues = IssuesOf(e);
        var first = issues.FirstOrDefault(i => i.Problem != DepProblem.Incompatible);
        if (first != null)
            return (first.Problem == DepProblem.Outdated ? T("Needs newer {0}", first.OtherName) : T("Needs {0}", first.OtherName), Theme.Accent);
        var conflict = issues.FirstOrDefault();
        if (conflict != null) return (T("Conflicts with {0}", conflict.OtherName), Theme.Bad);
        return (T("Enabled"), Theme.Good);
    }

    /// <summary>Line under the mod name: "v1.2.0 · Author".</summary>
    private static string SubLine(ModEntry e)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(e.Version)) parts.Add("v" + e.Version!.TrimStart('v', 'V'));
        if (!string.IsNullOrWhiteSpace(e.Author)) parts.Add(e.Author!);
        return string.Join("  ·  ", parts);
    }

    /// <summary>First line of the description, for the list.</summary>
    private static string DescriptionLine(ModEntry e)
    {
        var d = e.Description;
        if (string.IsNullOrWhiteSpace(d)) return "";
        var first = d.Replace("\r", "").Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "";
        return first.Trim();
    }

    private void PopulateGrid()
    {
        if (_grid is null) return;

        _issues.Clear();
        foreach (var e in _entries) _issues[Key(e)] = Dependencies.Check(e, _entries);

        var keep = SelectedEntries().Select(Key).ToHashSet();
        var q = _search.Text.Trim();
        var shown = _entries
            .Where(e => _filter switch { "on" => e.Enabled, "off" => !e.Enabled, _ => true })
            .Where(e => q.Length == 0
                        || e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || e.TypeText.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || e.Location.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || (e.FileName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (e.Author?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (e.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        _previewPaths.Clear();
        int keepFirst = _grid.FirstDisplayedScrollingRowIndex;
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var e in shown)
        {
            var (statusText, statusColor) = StatusOf(e);
            int i = _grid.Rows.Add("", "", e.Name, e.TypeText, e.Location, e.IsTracked ? T("Manager") : T("Manual"), statusText);
            var row = _grid.Rows[i];
            row.Tag = e;
            if (!e.Enabled)
            {
                row.DefaultCellStyle.ForeColor = Theme.TextMuted;
                row.DefaultCellStyle.SelectionForeColor = Theme.TextMuted;
            }
            var status = row.Cells["Status"];
            status.Style.ForeColor = statusColor;
            status.Style.SelectionForeColor = statusColor;
            var issues = IssuesOf(e);
            if (issues.Count > 0) status.ToolTipText = string.Join("\n", issues.Select(x => x.Describe()));
            if (!e.IsTracked) row.Cells["Source"].Style.ForeColor = Theme.TextMuted;
        }

        if (_grid.SortedColumn != null)
            _grid.Sort(_grid.SortedColumn,
                _grid.SortOrder == SortOrder.Descending ? ListSortDirection.Descending : ListSortDirection.Ascending);

        _grid.ClearSelection();
        foreach (DataGridViewRow row in _grid.Rows)
            if (row.Tag is ModEntry m && keep.Contains(Key(m))) row.Selected = true;
        if (keepFirst > 0 && _grid.Rows.Count > 0)
            _grid.FirstDisplayedScrollingRowIndex = Math.Min(keepFirst, _grid.Rows.Count - 1);
        _grid.ResumeLayout();

        int on = _entries.Count(e => e.Enabled);
        int problems = _entries.Count(e => e.Enabled && IssuesOf(e).Count > 0);
        _modsSummary.Text = !GameValid
            ? T("Set the game folder in Settings to get started")
            : _entries.Count == 0
                ? T("Nothing installed yet")
                : T("{0} installed  ·  {1} enabled  ·  {2} disabled", _entries.Count, on, _entries.Count - on) +
                  (problems > 0 ? "  ·  ⚠ " + T("{0} with missing requirements", problems) : "");

        if (!GameValid)
            _emptyState.Text = T("Set the game folder in Settings first.");
        else if (_entries.Count == 0)
            _emptyState.Text = T("No mods yet\n\nDrag .bamod packages, .dll mods or .bundle models here,\nor click to choose files.");
        else
            _emptyState.Text = T("No mods match your search.");
        _emptyState.Cursor = GameValid && _entries.Count == 0 ? Cursors.Hand : Cursors.Default;
        _emptyState.Visible = shown.Count == 0;
        if (_emptyState.Visible) _emptyState.BringToFront();

        UpdateGridScroll();
        UpdateDetails();
    }

    /// <summary>Shows an info row (label + value) only when there's something to show.</summary>
    private void ShowInfo(Control value, bool show)
    {
        value.Visible = show;
        if (_infoKeys.TryGetValue(value, out var key)) key.Visible = show;
    }

    private void ShowInfo(Label value, string? text)
    {
        bool has = !string.IsNullOrWhiteSpace(text) && text.Trim() != "-";
        value.Text = has ? text!.Trim() : "";
        ShowInfo((Control)value, has);
    }

    private void ClearSelection()
    {
        if (_grid is null) return;
        _grid.ClearSelection();
        UpdateDetails();
        UpdateButtons();
    }

    /// <summary>Shows Read me next to Show in folder only when the mod has one.</summary>
    private void SetReadmeVisible(bool visible)
    {
        _dReadme.Visible = visible;
        _dActions.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, visible ? LogicalToDeviceUnits(48) : 0);
    }

    /// <summary>Keeps the slim scrollbar in step with the list.</summary>
    private void UpdateGridScroll()
    {
        if (_grid is null) return;
        int total = _grid.Rows.Count;
        int visible = Math.Max(1, _grid.DisplayedRowCount(false));
        int first = Math.Max(0, _grid.FirstDisplayedScrollingRowIndex);
        _gridScroll.SetRange(total, visible, first);
    }

    /// <summary>Mouse wheel: one row per notch (rows are tall).</summary>
    private void ScrollGridBy(int rows)
    {
        if (_grid.Rows.Count == 0) return;
        int visible = Math.Max(1, _grid.DisplayedRowCount(false));
        int first = Math.Max(0, _grid.FirstDisplayedScrollingRowIndex);
        int target = Math.Clamp(first + rows, 0, Math.Max(0, _grid.Rows.Count - visible));
        if (target != first) _grid.FirstDisplayedScrollingRowIndex = target;
        UpdateGridScroll();
    }

    private List<ModEntry> SelectedEntries() =>
        _grid is null
            ? new List<ModEntry>()
            : _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<ModEntry>().ToList();

    private void AddDepLine(string text, Color color)
    {
        _dDeps.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(290, 0),
            ForeColor = color,
            Margin = new Padding(0, 0, 0, 4),
        });
    }

    private void UpdateDetails()
    {
        if (_dBody is null) return;
        var sel = SelectedEntries();
        _dBody.Visible = sel.Count > 0;
        _dEmpty.Visible = sel.Count == 0;
        _dClose.Visible = sel.Count > 0;
        if (sel.Count == 0) return;

        _dBody.SuspendLayout();
        bool allOn = sel.All(e => e.Enabled);
        _dToggle.Text = allOn ? T("Disable") : T("Enable");
        _dTips.SetToolTip(_dRemove, sel.Count == 1 ? T("Remove") : T("Remove {0} mods", sel.Count));

        var oldPicture = _dPicture.Image;
        _dPicture.Image = new Bitmap(Previews.Large(sel.Count == 1 ? PreviewPathFor(sel[0]) : null));
        oldPicture?.Dispose();

        foreach (Control c in _dDeps.Controls.Cast<Control>().ToList()) c.Dispose();
        _dDeps.Controls.Clear();

        if (sel.Count > 1)
        {
            _dTitle.Text = T("{0} mods selected", sel.Count);
            _dSub.Text = string.Join(", ", sel.Take(4).Select(e => e.Name)) + (sel.Count > 4 ? ", …" : "");
            _dStatus.Text = T("{0} enabled, {1} disabled", sel.Count(e => e.Enabled), sel.Count(e => !e.Enabled));
            _dStatus.ForeColor = Theme.TextMuted;
            _dDescription.Visible = false;
            foreach (var l in new[] { _dVersion, _dAuthor, _dLocation, _dInstalled, _dSource }) ShowInfo(l, null);
            ShowInfo(_dWebsite, false);
            _dWebsite.Tag = null;
            _dDepsHeader.Visible = _dDeps.Visible = false;
            _dFiles.SetItems(Array.Empty<string>());
            _dFilesHeader.Text = T("FILES");
            SetReadmeVisible(false);
            _dBody.ResumeLayout();
            return;
        }

        var e = sel[0];
        var mi = e.Info;
        _dTitle.Text = e.Name;
        _dSub.Text = (e.FileName != null ? e.FileName + "  ·  " : e.TypeText + "  ·  ") +
                     (e.IsTracked ? T("installed with Mod Manager") : T("added by hand"));
        var (statusText, statusColor) = StatusOf(e);
        _dStatus.Text = (e.Enabled && e.MissingFiles == 0 && statusColor == Theme.Good ? "●  " : e.Enabled ? "⚠  " : "○  ") + statusText;
        _dStatus.ForeColor = statusColor;

        bool hasDescription = !string.IsNullOrWhiteSpace(e.Description);
        _dDescription.Text = hasDescription ? e.Description!.Replace("\r\n", "\n") : T("No description provided.");
        _dDescription.ForeColor = hasDescription ? Theme.Text : Theme.TextMuted;
        _dDescription.Visible = true;
        ShowInfo(_dVersion, e.Version);
        ShowInfo(_dAuthor, e.Author);
        ShowInfo(_dLocation, e.Location);
        ShowInfo(_dInstalled, e.Package?.InstalledAt.ToString("g"));
        ShowInfo(_dSource, e.Package != null && e.Package.Source.Length > 0 ? Path.GetFileName(e.Package.Source) : null);
        if (!string.IsNullOrWhiteSpace(e.Website))
        {
            _dWebsite.Text = e.Website!.Replace("https://", "").Replace("http://", "").TrimEnd('/');
            _dWebsite.Tag = e.Website;
            _dWebsite.LinkArea = new LinkArea(0, _dWebsite.Text.Length);
            ShowInfo(_dWebsite, true);
        }
        else
        {
            _dWebsite.Text = "";
            _dWebsite.Tag = null;
            _dWebsite.LinkArea = new LinkArea(0, 0);
            ShowInfo(_dWebsite, false);
        }

        // Requirements, optional, conflicts, required-by
        var issues = IssuesOf(e);
        bool any = false;
        if (mi != null)
        {
            foreach (var req in mi.Requires)
            {
                any = true;
                var issue = issues.FirstOrDefault(x => x.Ref == req);
                var found = Dependencies.FindProvider(_entries, req.Id);
                var name = found?.Name ?? req.Id;
                var rule = ModVersion.Describe(req.Version);
                var label = rule.Length > 0 ? $"{name} ({rule})" : name;
                if (issue == null) AddDepLine($"✔  {label}", Theme.Good);
                else AddDepLine($"✖  {issue.Describe()}", issue.Problem == DepProblem.Disabled ? Theme.Accent : Theme.Bad);
            }
            foreach (var opt in mi.Optional)
            {
                any = true;
                var found = Dependencies.FindProvider(_entries, opt.Id);
                AddDepLine((found != null ? "✔" : "○") + "  " + T("{0} (optional)", found?.Name ?? opt.Id), found != null ? Theme.Good : Theme.TextMuted);
            }
            foreach (var bad in mi.Incompatible)
            {
                any = true;
                var issue = issues.FirstOrDefault(x => x.Ref == bad);
                var found = Dependencies.FindProvider(_entries, bad.Id);
                AddDepLine(issue != null ? "✖  " + T("Conflicts with {0}", issue.OtherName) : "-  " + T("Not compatible with {0}", found?.Name ?? bad.Id),
                    issue != null ? Theme.Bad : Theme.TextMuted);
            }
        }
        var requiredBy = Dependencies.RequiredBy(e, _entries);
        if (requiredBy.Count > 0)
        {
            any = true;
            AddDepLine(T("Needed by: ") + string.Join(", ", requiredBy.Select(x => x.Name)), Theme.TextMuted);
        }
        _dDepsHeader.Visible = _dDeps.Visible = any;

        var files = e.Package != null ? e.Package.Files : new List<string> { e.LooseRel ?? "" };
        _dFilesHeader.Text = T("FILES") + $" ({files.Count})  ·  " + ModService.Describe(files);
        _dFiles.SetItems(files);

        SetReadmeVisible(GameValid && ModService.ReadmePath(GameDir, e) != null);
        _dBody.ResumeLayout();
    }

    /// <summary>Preview file for a list entry (cached per refresh), or null for the default picture.</summary>
    private string? PreviewPathFor(ModEntry e)
    {
        var key = Key(e);
        if (_previewPaths.TryGetValue(key, out var cached)) return cached;
        var path = GameValid ? _mods.FindPreview(GameDir, _manifest, e) : null;
        _previewPaths[key] = path;
        return path;
    }

    // ───────────────────────── MelonLoader page ─────────────────────────

    private void BuildLoaderPage()
    {
        var (page, stack) = MakeStackPage("MelonLoader", T("The mod loader. Mods only load when MelonLoader is installed."));

        // Status card
        var status = new Panel
        {
            BackColor = Theme.Panel,
            Padding = new Padding(22, 20, 22, 20),
            Margin = new Padding(0, 0, 0, 14),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        var row = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top, BackColor = Theme.Panel };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _mlGlyph = new Label { AutoSize = true, Font = Theme.Icons(30f), ForeColor = Theme.TextMuted, Margin = new Padding(0, 4, 18, 0) };
        var right = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Theme.Panel, Dock = DockStyle.Fill };
        _mlTitle = MakeHeading("", 15f);
        _mlSub = MakeLabel("", 9.5f, Theme.TextMuted);
        _mlSub.MaximumSize = new Size(640, 0);
        var buttons = ButtonRow();
        _mlInstall = Theme.MakeButton(T("Install MelonLoader"), ButtonKind.Primary);
        _mlZip = Theme.MakeButton(T("Install from zip…"));
        _mlUninstall = Theme.MakeButton(T("Uninstall"), ButtonKind.Danger);
        buttons.Controls.AddRange(new Control[] { _mlInstall, _mlZip, _mlUninstall });
        right.Controls.AddRange(new Control[] { _mlTitle, _mlSub, buttons });
        row.Controls.Add(_mlGlyph, 0, 0);
        row.Controls.Add(right, 1, 0);
        status.Controls.Add(row);
        AddToStack(stack, status);

        // Anti-cheat warning
        AddToStack(stack, BuildAntiCheatWarning());

        // Troubleshooting card
        var (tools, toolsBody) = MakeCard(T("Troubleshooting"),
            T("MelonLoader writes everything it does to Latest.log - the first place to look when a mod doesn't load."));
        var toolButtons = ButtonRow();
        _mlLog = Theme.MakeButton(T("Open Latest.log"));
        _mlFolder = Theme.MakeButton(T("Open MelonLoader folder"));
        toolButtons.Controls.AddRange(new Control[] { _mlLog, _mlFolder });
        toolsBody.Controls.Add(toolButtons);
        AddToStack(stack, tools);

        // Info card
        var (info, infoBody) = MakeCard(T("Good to know"));
        foreach (var line in new[]
                 {
                     "•  " + T("While MelonLoader is installed, PLAY starts the game with Anti-Cheat disabled."),
            "•  " + T("The first launch after installing takes a few minutes while MelonLoader prepares the game."),
                     "•  " + T("Updating MelonLoader only replaces MelonLoader itself - your Mods, UserData (mod settings and models), Plugins and UserLibs folders aren't touched."),
                     "•  " + T("Uninstalling moves files to the Recycle Bin. You choose whether your mods are removed too."),
                 })
        {
            var l = MakeLabel(line, 9.5f, Theme.TextMuted);
            l.MaximumSize = new Size(720, 0);
            infoBody.Controls.Add(l);
        }
        _mlInfoSource = MakeLabel("", 9.5f, Theme.TextMuted);
        _mlInfoSource.MaximumSize = new Size(720, 0);
        infoBody.Controls.Add(_mlInfoSource);
        UpdateLoaderSourceText();
        AddToStack(stack, info);

        AddPage("loader", page);

        _mlInstall.Click += async (_, _) => await InstallLatestMelonLoader();
        _mlZip.Click += async (_, _) => await InstallMelonLoaderFromZip();
        _mlUninstall.Click += async (_, _) => await UninstallMelonLoader();
        _mlLog.Click += (_, _) => OpenLoaderLog();
        _mlFolder.Click += (_, _) => OpenPath(Path.Combine(GameDir, "MelonLoader"));
    }

    /// <summary>Amber warning card: don't use MelonLoader in online matches with anti-cheat.</summary>
    private Control BuildAntiCheatWarning()
    {
        var warnBack = Color.FromArgb(0x33, 0x2B, 0x1D);
        var card = new Panel
        {
            BackColor = warnBack,
            Padding = new Padding(0, 0, 22, 0),
            Margin = new Padding(0, 0, 0, 14),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        var stripe = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = Theme.Accent };

        var row = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top, BackColor = warnBack, Padding = new Padding(18, 16, 0, 16) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var icon = new Label
        {
            Text = Theme.GlyphWarning,
            Font = Theme.Icons(20f),
            ForeColor = Theme.Accent,
            AutoSize = true,
            Margin = new Padding(0, 2, 16, 0),
        };
        var texts = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = warnBack, Margin = new Padding(0) };
        var heading = MakeHeading(T("DO NOT play Broken Arrow with Anti-Cheat turned on while MelonLoader is installed"), 11.5f);
        heading.ForeColor = Theme.Accent;
        heading.MaximumSize = new Size(700, 0);
        var body = MakeLabel(
            T("Anti-Cheat can detect MelonLoader and mods, and that can get your account banned. With MelonLoader installed, only play with the Anti-Cheat disabled option (Right click on Broken Arrow → Properties → General → Selected Launch Option → Play Anti-Cheat Disabled).") + "\n\n" +
            T("For normal online matches (quick match, ranked, skirmish), uninstall MelonLoader first; untick \"Also delete all mods\" if you want to keep your mods; select \"Play Broken Arrow\" in Steam Launch Option."),
            9.5f, Color.FromArgb(0xD8, 0xD2, 0xC4));
        body.MaximumSize = new Size(700, 0);
        body.Margin = new Padding(0, 4, 0, 0);
        texts.Controls.Add(heading);
        texts.Controls.Add(body);
        row.Controls.Add(icon, 0, 0);
        row.Controls.Add(texts, 1, 0);

        card.Controls.Add(row);
        card.Controls.Add(stripe);
        return card;
    }

    private void RefreshLoaderPage(bool valid)
    {
        if (!valid)
        {
            _mlGlyph.Text = Theme.GlyphSettings;
            _mlGlyph.ForeColor = Theme.TextMuted;
            _mlTitle.Text = T("Game folder not set");
            _mlSub.Text = T("Set the game folder in Settings first.");
            _mlInstall.Text = T("Install MelonLoader");
            return;
        }

        var dir = GameDir;
        if (_ml.IsInstalled(dir))
        {
            var version = _ml.GetInstalledVersion(dir) ?? _manifest.MelonLoader?.Version;
            bool ok = _ml.HasProxyDll(dir);
            _mlGlyph.Text = ok ? Theme.GlyphCheck : Theme.GlyphWarning;
            _mlGlyph.ForeColor = ok ? Theme.Good : Theme.Bad;
            _mlTitle.Text = (ok ? T("MelonLoader {0} is installed", version ?? "") : T("MelonLoader {0} is incomplete", version ?? "")).Replace("  ", " ");
            _mlSub.Text = ok
                ? (_manifest.MelonLoader != null
                    ? T("Installed {0:g}. Press Update to get the newest release.", _manifest.MelonLoader.InstalledAt)
                    : T("Installed. Press Update to get the newest release."))
                : T("version.dll is missing from the game folder, so MelonLoader won't start. Reinstall it.");
            _mlInstall.Text = ok ? T("Update to latest") : T("Reinstall");
        }
        else
        {
            _mlGlyph.Text = Theme.GlyphDownload;
            _mlGlyph.ForeColor = Theme.Accent;
            _mlTitle.Text = T("MelonLoader is not installed");
            _mlSub.Text = T("Install it with one click - the latest release is downloaded from GitHub.");
            _mlInstall.Text = T("Install MelonLoader");
        }
    }

    // ───────────────────────── Settings page ─────────────────────────

    private void BuildSettingsPage()
    {
        var (page, stack) = MakeStackPage(T("Settings"), T("Language, game folder, where files go, and the Mod Manager itself."));

        // Language
        var (lang, langBody) = MakeCard(T("Language"), T("The Mod Manager restarts to switch the language."));
        var langRow = ButtonRow();
        foreach (var (code, name) in Languages)
        {
            var b = Theme.MakeButton(name, code == Current ? ButtonKind.Primary : ButtonKind.Secondary);
            if (code == "zh") b.Font = new Font("Microsoft YaHei UI", b.Font.Size, FontStyle.Bold);
            b.Click += (_, _) => SwitchLanguage(code);
            langRow.Controls.Add(b);
        }
        langBody.Controls.Add(langRow);
        AddToStack(stack, lang);

        // Game folder
        var (game, gameBody) = MakeCard(T("Game folder"), T("The folder that contains the game's .exe. Usually found automatically from Steam."));
        var pathRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Panel, Margin = new Padding(0, 10, 0, 6) };
        var (pathHost, pathBox) = MakeInput(560, @"…\steamapps\common\broken_arrow");
        _gamePath = pathBox;
        _browse = Theme.MakeButton(T("Browse…"));
        _detect = Theme.MakeButton(T("Auto-detect"));
        pathRow.Controls.AddRange(new Control[] { pathHost, _browse, _detect });
        _gameStatus = MakeLabel("", 9.5f, Theme.TextMuted);
        gameBody.Controls.Add(pathRow);
        gameBody.Controls.Add(_gameStatus);
        AddToStack(stack, game);

        // Quick folders
        var (folders, foldersBody) = MakeCard(T("Open folders"));
        var folderRow = ButtonRow();
        foreach (var (label, rel) in new[] { (T("Game folder"), ""), ("Mods", "Mods"), ("UserData", "UserData"), ("Plugins", "Plugins") })
        {
            var b = Theme.MakeButton(label);
            b.Click += (_, _) => OpenPath(rel.Length == 0 ? GameDir : Path.Combine(GameDir, rel));
            _folderButtons.Add(b);
            folderRow.Controls.Add(b);
        }
        foldersBody.Controls.Add(folderRow);
        AddToStack(stack, folders);

        // .bamod files
        var (assoc, assocBody) = MakeCard(T(".bamod mod packages"),
            T("Double-clicking a .bamod file opens the Mod Manager and installs it. This is set up automatically when the Mod Manager starts."));
        _assocStatus = MakeLabel("", 9.5f, Theme.TextMuted);
        var assocRow = ButtonRow();
        var assocOn = Theme.MakeButton(T("Open .bamod files with this Mod Manager"));
        var assocOff = Theme.MakeButton(T("Stop opening .bamod files"), ButtonKind.Ghost);
        assocOn.Click += (_, _) =>
        {
            Log(FileAssociation.Register() ? T(".bamod files now open with this Mod Manager.") : T("Couldn't register .bamod files."));
            RefreshSettingsPage(GameValid);
        };
        assocOff.Click += (_, _) =>
        {
            FileAssociation.Unregister();
            Log(T(".bamod files are no longer opened by the Mod Manager."));
            RefreshSettingsPage(GameValid);
        };
        assocRow.Controls.AddRange(new Control[] { assocOn, assocOff });
        assocBody.Controls.Add(_assocStatus);
        assocBody.Controls.Add(assocRow);
        AddToStack(stack, assoc);

        // For modders
        var (modders, moddersBody) = MakeCard(T("For modders"),
            T("Pack your mod into a .bamod: one file with your DLLs, models, picture, description and requirements."));
        var modderRow = ButtonRow();
        var create = Theme.MakeButton(T("Create mod package…"), ButtonKind.Primary);
        create.Click += (_, _) => CreatePackage();
        modderRow.Controls.Add(create);
        moddersBody.Controls.Add(modderRow);
        AddToStack(stack, modders);

        // File rules (editable)
        var (rules, rulesBody) = MakeCard(T("Where files go"),
            T("Loose files (a single .dll, .bundle…) are placed by their extension. Mod packages (.bamod / .zip) that contain Mods\\ or UserData\\ folders keep their own layout and don't need rules. Files of these types that you copied into the game folder by hand can also be switched on and off."));
        _rulesTable = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 3,
            BackColor = Theme.Panel,
            Margin = new Padding(0, 12, 0, 4),
        };
        _rulesTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _rulesTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _rulesTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rulesBody.Controls.Add(_rulesTable);

        var rulesButtons = ButtonRow();
        var addRule = Theme.MakeButton("+  " + T("Add rule"));
        var saveRules = Theme.MakeButton(T("Save"), ButtonKind.Primary);
        var resetRules = Theme.MakeButton(T("Reset to default"), ButtonKind.Ghost);
        addRule.Click += (_, _) => AddRuleRow("", "");
        saveRules.Click += (_, _) => SaveRules();
        resetRules.Click += (_, _) => LoadRuleRows(ManagerConfig.DefaultRules());
        rulesButtons.Controls.AddRange(new Control[] { addRule, saveRules, resetRules });
        rulesBody.Controls.Add(rulesButtons);
        AddToStack(stack, rules);
        LoadRuleRows(_cfg.ExtensionRules);

        // Advanced
        var (advanced, advancedBody) = MakeCard(T("Advanced"),
            T("Only change these if the game's folder name or MelonLoader's download location changes."));
        var advGrid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, BackColor = Theme.Panel, Margin = new Padding(0, 12, 0, 0) };
        advGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        advGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Label AdvKey(string k) => new()
        {
            Text = k,
            AutoSize = true,
            ForeColor = Theme.TextMuted,
            Font = Theme.Semibold(8.75f),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 18, 8),
        };
        var (steamHost, steamBox) = MakeInput(360, ManagerConfig.DefaultSteamFolderName);
        var (repoHost, repoBox) = MakeInput(360, ManagerConfig.DefaultMelonLoaderRepo);
        var (assetHost, assetBox) = MakeInput(360, ManagerConfig.DefaultMelonLoaderAsset);
        _advSteam = steamBox;
        _advRepo = repoBox;
        _advAsset = assetBox;
        foreach (var h in new[] { steamHost, repoHost, assetHost }) h.Margin = new Padding(0, 0, 0, 8);
        advGrid.Controls.Add(AdvKey(T("STEAM FOLDER NAME")), 0, 0); advGrid.Controls.Add(steamHost, 1, 0);
        advGrid.Controls.Add(AdvKey(T("MELONLOADER GITHUB REPO")), 0, 1); advGrid.Controls.Add(repoHost, 1, 1);
        advGrid.Controls.Add(AdvKey(T("MELONLOADER DOWNLOAD FILE")), 0, 2); advGrid.Controls.Add(assetHost, 1, 2);
        advancedBody.Controls.Add(advGrid);
        advancedBody.Controls.Add(MakeLabel(T("Steam folder = the folder under steamapps\\common used by Auto-detect."), 9f, Theme.TextMuted));

        var advButtons = ButtonRow();
        var saveAdv = Theme.MakeButton(T("Save"), ButtonKind.Primary);
        var resetAdv = Theme.MakeButton(T("Reset to default"), ButtonKind.Ghost);
        saveAdv.Click += (_, _) => SaveAdvanced();
        resetAdv.Click += (_, _) =>
        {
            _advSteam.Text = ManagerConfig.DefaultSteamFolderName;
            _advRepo.Text = ManagerConfig.DefaultMelonLoaderRepo;
            _advAsset.Text = ManagerConfig.DefaultMelonLoaderAsset;
        };
        advButtons.Controls.AddRange(new Control[] { saveAdv, resetAdv });
        advancedBody.Controls.Add(advButtons);
        AddToStack(stack, advanced);
        _advSteam.Text = _cfg.SteamFolderName;
        _advRepo.Text = _cfg.MelonLoaderRepo;
        _advAsset.Text = _cfg.MelonLoaderAsset;

        // About
        var version = typeof(MainForm).Assembly.GetName().Version;
        var (about, aboutBody) = MakeCard(T("About"));
        aboutBody.Controls.Add(MakeLabel($"{_cfg.GameName} Mod Manager  v{version?.ToString(3) ?? "1.0.0"}  ·  " + T("by {0}", "Speer"), 9.5f, Theme.TextMuted));
        aboutBody.Controls.Add(MakeLabel(T("Install record: {0}", T("<game folder>") + $"\\{Manifest.FolderRel.Replace('/', '\\')}\\manifest.json"), 9f, Theme.TextMuted));
        AddToStack(stack, about);

        // Uninstall the Mod Manager itself
        var (removeCard, removeBody) = MakeCard(T("Uninstall Mod Manager"),
            T("Removes the Mod Manager from this PC: the program, its settings and its records. You'll be asked what to do with MelonLoader and your mods."));
        var removeRow = ButtonRow();
        var removeBtn = Theme.MakeButton(T("Uninstall Mod Manager…"), ButtonKind.Danger);
        removeBtn.Click += async (_, _) => await UninstallManager();
        removeRow.Controls.Add(removeBtn);
        removeBody.Controls.Add(removeRow);
        AddToStack(stack, removeCard);

        AddPage("settings", page);

        _gamePath.TextChanged += (_, _) => RefreshAll();
        _browse.Click += (_, _) => BrowseGame();
        _detect.Click += (_, _) => DetectGame(showFailure: true);
    }

    private void RefreshSettingsPage(bool valid)
    {
        bool registered = FileAssociation.IsRegisteredHere();
        _assocStatus.Text = registered ? "✔  " + T(".bamod files open with this Mod Manager") : "✖  " + T(".bamod files don't open with this Mod Manager");
        _assocStatus.ForeColor = registered ? Theme.Good : Theme.TextMuted;

        var dir = GameDir;
        if (string.IsNullOrWhiteSpace(dir))
        {
            _gameStatus.Text = T("No folder chosen yet.");
            _gameStatus.ForeColor = Theme.TextMuted;
        }
        else if (!valid)
        {
            _gameStatus.Text = "✖  " + T("This doesn't look like the game folder (no *_Data folder inside).");
            _gameStatus.ForeColor = Theme.Bad;
        }
        else
        {
            var exe = GameLocator.FindExe(dir);
            var appId = GameLocator.FindSteamAppId(dir);
            _gameStatus.Text = "✔  " + T("Found {0}", exe != null ? Path.GetFileName(exe) : _cfg.GameName) +
                               (appId != null ? "  ·  " + T("Steam app {0}", appId) : "");
            _gameStatus.ForeColor = Theme.Good;
        }
    }

    // ───────────────────────── settings editors ─────────────────────────

    private void LoadRuleRows(IReadOnlyDictionary<string, string> rules)
    {
        _rulesTable.SuspendLayout();
        foreach (Control c in _rulesTable.Controls.Cast<Control>().ToList()) c.Dispose();
        _rulesTable.Controls.Clear();
        _rulesTable.RowStyles.Clear();
        _ruleRows.Clear();

        Label Head(string t) => new() { Text = t, AutoSize = true, ForeColor = Theme.TextMuted, Font = Theme.Semibold(8.25f), Margin = new Padding(0, 0, 8, 6) };
        _rulesTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rulesTable.Controls.Add(Head(T("EXTENSION")), 0, 0);
        _rulesTable.Controls.Add(Head(T("GOES TO (inside the game folder)")), 1, 0);

        foreach (var (ext, folder) in rules.OrderBy(r => r.Key)) AddRuleRow(ext, folder);
        _rulesTable.ResumeLayout();
    }

    private void AddRuleRow(string ext, string folder)
    {
        var (extHost, extBox) = MakeInput(120, ".bundle");
        var (folderHost, folderBox) = MakeInput(420, "UserData/MyMod");
        extHost.Margin = new Padding(0, 0, 8, 8);
        folderHost.Margin = new Padding(0, 0, 8, 8);
        extBox.Text = ext;
        folderBox.Text = folder.Replace('/', '\\');

        var remove = new Label
        {
            Text = "✕",
            Size = new Size(32, 36),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.TextMuted,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 8),
        };
        remove.MouseEnter += (_, _) => remove.ForeColor = Theme.Bad;
        remove.MouseLeave += (_, _) => remove.ForeColor = Theme.TextMuted;

        int row = _rulesTable.RowStyles.Count;
        _rulesTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rulesTable.Controls.Add(extHost, 0, row);
        _rulesTable.Controls.Add(folderHost, 1, row);
        _rulesTable.Controls.Add(remove, 2, row);
        var entry = (extBox, folderBox);
        _ruleRows.Add(entry);

        remove.Click += (_, _) =>
        {
            _ruleRows.Remove(entry);
            extHost.Visible = folderHost.Visible = remove.Visible = false;
        };
        if (ext.Length == 0) extBox.Focus();
    }

    private void SaveRules()
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (extBox, folderBox) in _ruleRows)
        {
            var ext = extBox.Text.Trim();
            var folder = folderBox.Text.Trim().Replace('\\', '/').Trim('/');
            if (ext.Length == 0 && folder.Length == 0) continue;

            if (!ext.StartsWith('.')) ext = "." + ext;
            if (ext.Length < 2 || ext.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || ext.Count(c => c == '.') != 1 ||
                ext.Equals(PathUtil.DisabledSuffix, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, T("\"{0}\" isn't a valid file extension (example: .bundle).", extBox.Text), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                extBox.Focus();
                return;
            }
            if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) || ext.Equals(".bamod", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, T("{0} files are mod packages and are always unpacked - they can't have a rule.", ext), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                extBox.Focus();
                return;
            }
            try
            {
                if (Path.IsPathRooted(folder) || (folder.Length > 0 && PathUtil.NormalizeRel(folder) != folder))
                    throw new InvalidDataException();
            }
            catch
            {
                MessageBox.Show(this, T("\"{0}\" must be a folder inside the game folder, like Mods or UserData\\MyMod.", folderBox.Text),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                folderBox.Focus();
                return;
            }
            if (rules.ContainsKey(ext))
            {
                MessageBox.Show(this, T("There are two rules for {0}. Keep one.", ext), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                extBox.Focus();
                return;
            }
            rules[ext] = folder;
        }

        _cfg.ExtensionRules = ManagerConfig.NormalizeRules(rules);
        if (_cfg.Save()) Log(T("Saved file rules."));
        else Log(T("Couldn't save the settings file."));
        LoadRuleRows(_cfg.ExtensionRules);
        RefreshAll();
    }

    private void SaveAdvanced()
    {
        var steam = _advSteam.Text.Trim();
        var repo = _advRepo.Text.Trim().Trim('/');
        var asset = _advAsset.Text.Trim();
        if (steam.Length == 0 || steam.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(this, T("Enter the game's folder name under steamapps\\common (e.g. broken_arrow)."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(repo, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
        {
            MessageBox.Show(this, T("The GitHub repo must look like owner/name (e.g. LavaGang/MelonLoader)."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (asset.Length == 0 || !asset.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, T("The download file must be a .zip (e.g. MelonLoader.x64.zip)."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _cfg.SteamFolderName = steam;
        _cfg.MelonLoaderRepo = repo;
        _cfg.MelonLoaderAsset = asset;
        Log(_cfg.Save() ? T("Saved advanced settings.") : T("Couldn't save the settings file."));
        UpdateLoaderSourceText();
    }

    private void UpdateLoaderSourceText()
    {
        if (_mlInfoSource != null)
            _mlInfoSource.Text = "•  " + T("Downloads come from {0} ({1}). You can change this in Settings → Advanced.", "github.com/" + _cfg.MelonLoaderRepo, _cfg.MelonLoaderAsset);
    }

    // ───────────────────────── Log page ─────────────────────────

    private void BuildLogPage()
    {
        var page = new Panel { BackColor = Theme.Background, Padding = new Padding(32, 24, 32, 20) };

        var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Theme.Background };
        var titles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Background };
        titles.Controls.Add(MakeHeading(T("Activity log"), 20f));
        titles.Controls.Add(MakeLabel(T("Everything the mod manager did in this session."), 10f, Theme.TextMuted));
        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Background, Padding = new Padding(0, 6, 0, 0) };
        var copy = Theme.MakeButton(T("Copy"));
        var clear = Theme.MakeButton(T("Clear"), ButtonKind.Ghost);
        clear.Margin = new Padding(0);
        actions.Controls.AddRange(new Control[] { copy, clear });
        header.Controls.Add(titles);
        header.Controls.Add(actions);

        var logHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Padding = new Padding(14, 12, 6, 12) };
        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            Font = new Font("Consolas", 9.5f),
        };
        Theme.DarkScrollbars(_log);
        logHost.Controls.Add(_log);

        page.Controls.Add(logHost);
        page.Controls.Add(header);
        AddPage("log", page);

        copy.Click += (_, _) =>
        {
            if (_log.TextLength > 0) Clipboard.SetText(_log.Text);
        };
        clear.Click += (_, _) => _log.Clear();
    }
}
