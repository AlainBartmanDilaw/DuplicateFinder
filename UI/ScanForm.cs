using DuplicateFinder.Services;

namespace DuplicateFinder.UI;

public sealed class ScanForm : Form
{
    private readonly ListBox _lstDirs;
    private readonly Button _btnAdd;
    private readonly Button _btnRemove;
    private readonly TextBox _txtExtensions;
    private readonly CheckBox _chkRecursive;
    private readonly Button _btnScan;
    private readonly Button _btnCancel;
    private readonly Label _lblHint;

    public List<string> SelectedDirectories { get; private set; } = [];
    public ScanOptions Options { get; private set; } = new();

    public ScanForm()
    {
        Text = "Configurer le scan";
        Size = new Size(560, 420);
        MinimumSize = new Size(480, 380);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Color.FromArgb(22, 22, 30);
        ForeColor = Color.FromArgb(220, 220, 235);
        Font = new Font("Segoe UI", 10f);

        // ── Répertoires ──────────────────────────────────────────────
        var grpDirs = new GroupBox
        {
            Text = "Répertoires à scanner",
            Dock = DockStyle.Top,
            Height = 200,
            Padding = new Padding(8),
            ForeColor = Color.FromArgb(120, 180, 255),
            FlatStyle = FlatStyle.Flat
        };

        _lstDirs = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 42),
            ForeColor = Color.FromArgb(200, 200, 220),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9f)
        };

        var pnlDirBtns = new Panel { Dock = DockStyle.Right, Width = 100 };

        _btnAdd = CreateButton("+ Ajouter", Color.FromArgb(40, 100, 180));
        _btnAdd.Dock = DockStyle.Top;
        _btnAdd.Height = 36;
        _btnAdd.Click += BtnAdd_Click;

        _btnRemove = CreateButton("− Retirer", Color.FromArgb(80, 30, 30));
        _btnRemove.Dock = DockStyle.Top;
        _btnRemove.Height = 36;
        _btnRemove.Click += BtnRemove_Click;

        pnlDirBtns.Controls.AddRange([_btnRemove, _btnAdd]);
        grpDirs.Controls.AddRange([_lstDirs, pnlDirBtns]);

        // ── Options ──────────────────────────────────────────────────
        var grpOpts = new GroupBox
        {
            Text = "Options",
            Dock = DockStyle.Top,
            Height = 100,
            Padding = new Padding(8),
            ForeColor = Color.FromArgb(120, 180, 255),
        };

        var lblExt = new Label { Text = "Extensions (ex: .jpg .png .gif) — vide = tout :", AutoSize = true, Location = new Point(10, 22) };
        _txtExtensions = new TextBox
        {
            Location = new Point(10, 44),
            Width = 340,
            BackColor = Color.FromArgb(30, 30, 42),
            ForeColor = Color.FromArgb(200, 200, 220),
            BorderStyle = BorderStyle.FixedSingle
        };
        _chkRecursive = new CheckBox
        {
            Text = "Inclure les sous-répertoires",
            Location = new Point(10, 72),
            Checked = true,
            ForeColor = Color.FromArgb(200, 200, 220),
            AutoSize = true
        };

        grpOpts.Controls.AddRange([lblExt, _txtExtensions, _chkRecursive]);

        // ── Hint ──────────────────────────────────────────────────────
        _lblHint = new Label
        {
            Text = "ℹ️  Les SHA256 seront calculés lors du scan — peut prendre du temps sur de gros volumes.",
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = Color.FromArgb(140, 140, 170),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            Padding = new Padding(4, 8, 0, 0)
        };

        // ── Boutons OK/Annuler ─────────────────────────────────────────
        var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 48 };
        _btnScan = CreateButton("▶  Lancer le scan", Color.FromArgb(30, 110, 60));
        _btnScan.Size = new Size(160, 36);
        _btnScan.Location = new Point(8, 6);
        _btnScan.Click += BtnScan_Click;

        _btnCancel = CreateButton("Annuler", Color.FromArgb(60, 60, 75));
        _btnCancel.Size = new Size(100, 36);
        _btnCancel.Location = new Point(176, 6);
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        pnlBottom.Controls.AddRange([_btnScan, _btnCancel]);

        Controls.AddRange([pnlBottom, _lblHint, grpOpts, grpDirs]);
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Choisir un répertoire" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            if (!_lstDirs.Items.Contains(dlg.SelectedPath))
                _lstDirs.Items.Add(dlg.SelectedPath);
        }
    }

    private void BtnRemove_Click(object? sender, EventArgs e)
    {
        if (_lstDirs.SelectedItem != null)
            _lstDirs.Items.Remove(_lstDirs.SelectedItem);
    }

    private void BtnScan_Click(object? sender, EventArgs e)
    {
        if (_lstDirs.Items.Count == 0)
        {
            MessageBox.Show("Veuillez ajouter au moins un répertoire.", "Attention",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SelectedDirectories = _lstDirs.Items.Cast<string>().ToList();

        var exts = _txtExtensions.Text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.StartsWith('.') ? e.ToLower() : "." + e.ToLower())
            .ToArray();

        Options = new ScanOptions
        {
            Extensions = exts,
            Recursive = _chkRecursive.Checked
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button CreateButton(string text, Color bg)
    {
        return new Button
        {
            Text = text,
            BackColor = bg,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
    }
}
