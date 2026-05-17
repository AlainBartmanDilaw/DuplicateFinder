using DuplicateFinder.Data;
using DuplicateFinder.Models;
using DuplicateFinder.Services;

namespace DuplicateFinder.UI;

public sealed class MainForm : Form
{
    // ── Base de données ───────────────────────────────────────────────────────
    private readonly FileRepository _repo;

    // ── Données doublons ──────────────────────────────────────────────────────
    private List<DuplicateGroup> _groups = [];
    private int _groupIndex;       // groupe courant
    private int _pairIndex;        // paire courante dans le groupe

    // ── UI ────────────────────────────────────────────────────────────────────
    private readonly Panel _topBar;
    private readonly Label _lblTitle;
    private readonly Label _lblStats;

    // Barre de navigation
    private readonly Label _lblGroup;
    private readonly Button _btnPrevGroup;
    private readonly Button _btnNextGroup;
    private readonly Label _lblPair;
    private readonly Button _btnPrevPair;
    private readonly Button _btnNextPair;

    // Zone principale
    private readonly Panel _pnlMain;
    private ImageComparePanel? _comparePanel;

    // Barre du bas
    private readonly StatusStrip _status;
    private readonly ToolStripStatusLabel _lblStatusLeft;
    private readonly ToolStripStatusLabel _lblStatusRight;

    // Toolbar
    private readonly ToolStrip _toolbar;

    public MainForm()
    {
        // ── DB ────────────────────────────────────────────────────────────────
        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DuplicateFinder", "store.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _repo = new FileRepository(dbPath);

        // ── Fenêtre ───────────────────────────────────────────────────────────
        Text = "Duplicate Finder";
        Size = new Size(1280, 820);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 18, 26);
        ForeColor = Color.FromArgb(220, 220, 235);
        Font = new Font("Segoe UI", 10f);

        // ── Toolbar ───────────────────────────────────────────────────────────
        _toolbar = new ToolStrip
        {
            BackColor = Color.FromArgb(28, 28, 40),
            ForeColor = Color.FromArgb(200, 200, 220),
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            Padding = new Padding(6, 2, 0, 2),
            ImageScalingSize = new Size(20, 20)
        };

        AddToolBtn("🔍  Scanner", "Choisir des répertoires et lancer un scan", BtnScan_Click);
        _toolbar.Items.Add(new ToolStripSeparator());
        AddToolBtn("📋  Fichiers", "Afficher tous les fichiers recensés", BtnFiles_Click);
        AddToolBtn("⚠  Doublons", "Recharger la liste des doublons", BtnRefreshDups_Click);
        _toolbar.Items.Add(new ToolStripSeparator());
        AddToolBtn("🗑  Vider la base", "Supprimer toutes les données", BtnClearDb_Click);

        // ── Top bar (titre + stats) ────────────────────────────────────────────
        _topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = Color.FromArgb(24, 24, 36)
        };

        _lblTitle = new Label
        {
            Text = "DUPLICATE FINDER",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(80, 160, 255),
            AutoSize = true,
            Location = new Point(14, 10)
        };

        _lblStats = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(140, 140, 180),
            AutoSize = true,
            Location = new Point(14, 36)
        };

        _topBar.Controls.AddRange([_lblTitle, _lblStats]);

        // ── Navigation groupes ─────────────────────────────────────────────────
        var navPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = Color.FromArgb(22, 22, 34),
            Padding = new Padding(8, 4, 8, 4)
        };

        _btnPrevGroup = NavBtn("◀ Groupe préc.");
        _btnPrevGroup.Click += (_, _) => MoveGroup(-1);

        _lblGroup = NavLabel();

        _btnNextGroup = NavBtn("Groupe suiv. ▶");
        _btnNextGroup.Click += (_, _) => MoveGroup(+1);

        var sep = new Label { Text = "│", Width = 16, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(60, 60, 80), AutoSize = false };

        _btnPrevPair = NavBtn("◀ Paire préc.");
        _btnPrevPair.Click += (_, _) => MovePair(-1);

        _lblPair = NavLabel();

        _btnNextPair = NavBtn("Paire suiv. ▶");
        _btnNextPair.Click += (_, _) => MovePair(+1);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false
        };
        flow.Controls.AddRange([_btnPrevGroup, _lblGroup, _btnNextGroup, sep, _btnPrevPair, _lblPair, _btnNextPair]);
        navPanel.Controls.Add(flow);

        // ── Zone principale ────────────────────────────────────────────────────
        _pnlMain = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 18, 26),
            AutoScroll = true
        };

        // ── Status bar ────────────────────────────────────────────────────────
        _status = new StatusStrip { BackColor = Color.FromArgb(20, 20, 32) };
        _lblStatusLeft = new ToolStripStatusLabel
        {
            ForeColor = Color.FromArgb(120, 180, 255),
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _lblStatusRight = new ToolStripStatusLabel
        {
            ForeColor = Color.FromArgb(100, 100, 140),
            TextAlign = ContentAlignment.MiddleRight
        };
        _status.Items.AddRange([_lblStatusLeft, _lblStatusRight]);

        Controls.AddRange([_pnlMain, navPanel, _topBar, _toolbar, _status]);

        FormClosed += (_, _) => _repo.Dispose();

        RefreshDuplicates();
    }

    // ── Toolbar actions ───────────────────────────────────────────────────────

    private async void BtnScan_Click(object? sender, EventArgs e)
    {
        using var cfg = new ScanForm();
        if (cfg.ShowDialog(this) != DialogResult.OK) return;

        var scanner = new DirectoryScanner(_repo);
        using var progress = new ProgressForm();

        var progressReporter = new Progress<ScanProgress>(p => progress.UpdateProgress(p));

        progress.FormClosed += async (_, _) => { };
        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var dir in cfg.SelectedDirectories)
                    await scanner.ScanAsync(dir, cfg.Options, progressReporter, progress.CancellationToken);
            }
            catch (OperationCanceledException) { }
            finally { progress.MarkDone(); }
        });

        progress.ShowDialog(this);
        RefreshDuplicates();
        SetStatus($"Scan terminé — base : {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DuplicateFinder", "store.db")}");
    }

    private void BtnFiles_Click(object? sender, EventArgs e)
    {
        using var f = new FileListForm(_repo);
        f.ShowDialog(this);
    }

    private void BtnRefreshDups_Click(object? sender, EventArgs e) => RefreshDuplicates();

    private void BtnClearDb_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("Supprimer toutes les données de la base ?", "Confirmer",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            _repo.ClearAll();
            RefreshDuplicates();
            SetStatus("Base vidée.");
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void MoveGroup(int delta)
    {
        if (_groups.Count == 0) return;
        _groupIndex = Math.Clamp(_groupIndex + delta, 0, _groups.Count - 1);
        _pairIndex = 0;
        ShowCurrentPair();
    }

    private void MovePair(int delta)
    {
        if (_groups.Count == 0) return;
        var g = _groups[_groupIndex];
        int maxPairs = g.Count - 1;   // référent vs chaque autre
        _pairIndex = Math.Clamp(_pairIndex + delta, 0, maxPairs - 1);
        ShowCurrentPair();
    }

    // ── Affichage ─────────────────────────────────────────────────────────────

    private void RefreshDuplicates()
    {
        _groups = _repo.GetDuplicateGroups();
        _groupIndex = 0;
        _pairIndex = 0;

        var allFiles = _repo.GetAllFiles();
        _lblStats.Text = $"{allFiles.Count} fichiers  |  {_groups.Count} groupe(s) de doublons  |  " +
                         $"{_groups.Sum(g => g.Count - 1)} fichier(s) en double";

        ShowCurrentPair();
    }

    private void ShowCurrentPair()
    {
        _pnlMain.SuspendLayout();
        _pnlMain.Controls.Clear();

        if (_groups.Count == 0)
        {
            var lbl = new Label
            {
                Text = "Aucun doublon détecté.\nLancez un scan pour commencer.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(100, 100, 140),
                Font = new Font("Segoe UI", 14f),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _pnlMain.Controls.Add(lbl);
            UpdateNavLabels();
            _pnlMain.ResumeLayout();
            return;
        }

        var group = _groups[_groupIndex];
        var reference = group.Reference;
        int maxPairIdx = group.Count - 2;
        _pairIndex = Math.Clamp(_pairIndex, 0, maxPairIdx);

        // Le doublon à comparer est Files[_pairIndex + 1]
        var duplicate = group.Files[_pairIndex + 1];

        if (_comparePanel != null)
        {
            _comparePanel.FileDeleted -= OnFileDeleted;
            _comparePanel.Dispose();
        }

        _comparePanel = new ImageComparePanel(_repo)
        {
            Dock = DockStyle.Fill
        };
        _comparePanel.FileDeleted += OnFileDeleted;
        _comparePanel.SetPair(reference, duplicate, group.Sha256, group.Count);

        _pnlMain.Controls.Add(_comparePanel);
        UpdateNavLabels();
        _pnlMain.ResumeLayout();
    }

    private void OnFileDeleted(object? sender, EventArgs e) => RefreshDuplicates();

    private void UpdateNavLabels()
    {
        if (_groups.Count == 0)
        {
            _lblGroup.Text = "Aucun groupe";
            _lblPair.Text = string.Empty;
            _btnPrevGroup.Enabled = _btnNextGroup.Enabled = false;
            _btnPrevPair.Enabled = _btnNextPair.Enabled = false;
            return;
        }

        var g = _groups[_groupIndex];
        _lblGroup.Text = $" Groupe {_groupIndex + 1} / {_groups.Count} ({g.Count} fichiers) ";
        _lblPair.Text = $" Paire {_pairIndex + 1} / {g.Count - 1} ";

        _btnPrevGroup.Enabled = _groupIndex > 0;
        _btnNextGroup.Enabled = _groupIndex < _groups.Count - 1;
        _btnPrevPair.Enabled = _pairIndex > 0;
        _btnNextPair.Enabled = _pairIndex < g.Count - 2;
    }

    private void SetStatus(string msg)
    {
        _lblStatusLeft.Text = msg;
        _lblStatusRight.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddToolBtn(string text, string tooltip, EventHandler handler)
    {
        var btn = new ToolStripButton(text)
        {
            ToolTipText = tooltip,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(200, 200, 225),
            Padding = new Padding(6, 0, 6, 0)
        };
        btn.Click += handler;
        _toolbar.Items.Add(btn);
    }

    private static Button NavBtn(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Height = 30,
        BackColor = Color.FromArgb(35, 50, 80),
        ForeColor = Color.FromArgb(160, 200, 255),
        FlatStyle = FlatStyle.Flat,
        Cursor = Cursors.Hand,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        Margin = new Padding(2, 4, 2, 4)
    };

    private static Label NavLabel() => new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 9f),
        ForeColor = Color.FromArgb(180, 180, 215),
        TextAlign = ContentAlignment.MiddleCenter,
        Margin = new Padding(4, 8, 4, 0)
    };
}
