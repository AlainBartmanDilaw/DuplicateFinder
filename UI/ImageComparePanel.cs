using DuplicateFinder.Models;
using DuplicateFinder.Data;
using DuplicateFinder.Services;

namespace DuplicateFinder.UI;

/// <summary>
/// Panneau affichant 2 images côte-à-côte avec infos et bouton corbeille.
/// </summary>
public sealed class ImageComparePanel : Panel
{
    public event EventHandler? FileDeleted;

    private readonly FileRepository _repo;
    private FileEntry? _leftFile;
    private FileEntry? _rightFile;

    private readonly ImagePanel _left;
    private readonly ImagePanel _right;
    private readonly Label _lblSha;
    private readonly Label _lblDupCount;

    public ImageComparePanel(FileRepository repo)
    {
        _repo = repo;
        BackColor = Color.FromArgb(28, 28, 38);
        Padding = new Padding(0, 0, 0, 12);

        _lblSha = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = Color.FromArgb(90, 130, 200),
            Font = new Font("Consolas", 8f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
        };

        _lblDupCount = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            ForeColor = Color.FromArgb(220, 120, 60),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
        };

        var pnlImages = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(6)
        };
        pnlImages.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pnlImages.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _left = new ImagePanel("RÉFÉRENT");
        _right = new ImagePanel("DOUBLON");

        _left.TrashClicked += (_, _) => DeleteFile(isLeft: true);
        _right.TrashClicked += (_, _) => DeleteFile(isLeft: false);

        pnlImages.Controls.Add(_left, 0, 0);
        pnlImages.Controls.Add(_right, 1, 0);

        var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(50, 50, 68) };

        Controls.AddRange([pnlImages, _lblDupCount, _lblSha, sep]);
    }

    public void SetPair(FileEntry reference, FileEntry duplicate, string sha256, int totalInGroup)
    {
        _leftFile = reference;
        _rightFile = duplicate;

        _lblSha.Text = $"SHA256 : {sha256}";
        _lblDupCount.Text = totalInGroup > 2
            ? $"⚠  {totalInGroup} fichiers identiques dans ce groupe"
            : string.Empty;

        _left.LoadFile(reference);
        _right.LoadFile(duplicate);
    }

    private void DeleteFile(bool isLeft)
    {
        var target = isLeft ? _leftFile : _rightFile;
        if (target == null) return;

        var confirm = MessageBox.Show(
            $"Envoyer dans la corbeille ?\n\n{target.FullPath}",
            "Confirmer",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        bool ok = RecycleBinService.SendToRecycleBin(target, _repo);
        if (ok)
            FileDeleted?.Invoke(this, EventArgs.Empty);
        else
            MessageBox.Show("Impossible de supprimer le fichier.", "Erreur",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

// ── Sous-panneau pour une image ──────────────────────────────────────────────

public sealed class ImagePanel : Panel
{
    public event EventHandler? TrashClicked;

    private readonly PictureBox _pic;
    private readonly Label _lblPath;
    private readonly Label _lblSize;
    private readonly Label _lblRole;
    private readonly Button _btnTrash;
    private static readonly string[] _imageExts = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".ico"];

    public ImagePanel(string role)
    {
        Margin = new Padding(4);
        BackColor = Color.FromArgb(32, 32, 46);
        Dock = DockStyle.Fill;

        _lblRole = new Label
        {
            Text = role,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = role == "RÉFÉRENT"
                ? Color.FromArgb(80, 200, 120)
                : Color.FromArgb(220, 100, 80)
        };

        _pic = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(20, 20, 30)
        };
        _pic.Click += (_, _) => OpenFile();

        var pnlInfo = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Color.FromArgb(25, 25, 36) };

        _lblPath = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = Color.FromArgb(180, 180, 205),
            Font = new Font("Consolas", 7.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0),
            AutoEllipsis = true
        };

        _lblSize = new Label
        {
            Dock = DockStyle.Top,
            Height = 18,
            ForeColor = Color.FromArgb(130, 130, 160),
            Font = new Font("Segoe UI", 8f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };

        _btnTrash = new Button
        {
            Text = "🗑  Corbeille",
            Dock = DockStyle.Bottom,
            Height = 30,
            BackColor = Color.FromArgb(90, 25, 25),
            ForeColor = Color.FromArgb(255, 160, 140),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _btnTrash.FlatAppearance.BorderColor = Color.FromArgb(130, 40, 40);
        _btnTrash.Click += (_, _) => TrashClicked?.Invoke(this, EventArgs.Empty);

        pnlInfo.Controls.AddRange([_btnTrash, _lblSize, _lblPath]);
        Controls.AddRange([pnlInfo, _pic, _lblRole]);
    }

    public void LoadFile(FileEntry file)
    {
        _lblPath.Text = file.FullPath;
        _lblPath.ToolTipText(file.FullPath);
        _lblSize.Text = $"Taille : {FormatSize(file.FileSize)}";

        // Libérer ancienne image
        if (_pic.Image != null)
        {
            _pic.Image.Dispose();
            _pic.Image = null;
        }

        var ext = Path.GetExtension(file.FullPath).ToLowerInvariant();
        if (_imageExts.Contains(ext) && File.Exists(file.FullPath))
        {
            try
            {
                // Charger sans verrouiller le fichier
                using var fs = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var bmp = new Bitmap(fs);
                _pic.Image = bmp;
            }
            catch
            {
                _pic.Image = null;
            }
        }
        else
        {
            _pic.Image = null;
        }
    }

    private void OpenFile()
    {
        var path = _lblPath.Text;
        if (File.Exists(path))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { }
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} o";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} Ko";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024:F1} Mo";
        return $"{bytes / 1024.0 / 1024 / 1024:F2} Go";
    }
}

// Extension helper
public static class LabelExt
{
    private static readonly System.ComponentModel.ComponentResourceManager _rm = new(typeof(Label));

    public static void ToolTipText(this Label lbl, string text)
    {
        // Simple: on utilise le Tag
        lbl.Tag = text;
    }
}
