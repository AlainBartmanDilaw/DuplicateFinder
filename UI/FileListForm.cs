using DuplicateFinder.Data;
using DuplicateFinder.Models;

namespace DuplicateFinder.UI;

public sealed class FileListForm : Form
{
    private readonly DataGridView _grid;
    private readonly Label _lblCount;

    public FileListForm(FileRepository repo)
    {
        Text = "Tous les fichiers scannés";
        Size = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(22, 22, 30);
        ForeColor = Color.FromArgb(220, 220, 235);
        Font = new Font("Segoe UI", 9.5f);

        _lblCount = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Color.FromArgb(120, 180, 255),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(26, 26, 36),
            GridColor = Color.FromArgb(45, 45, 60),
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(26, 26, 36),
                ForeColor = Color.FromArgb(200, 200, 220),
                SelectionBackColor = Color.FromArgb(40, 70, 130),
                SelectionForeColor = Color.White,
                Font = new Font("Consolas", 8.5f)
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(35, 35, 52),
                ForeColor = Color.FromArgb(120, 180, 255),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            },
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 30
        };

        var files = repo.GetAllFiles();
        var groups = repo.GetDuplicateGroups()
            .SelectMany(g => g.Files.Select(f => (f.Id, g.Count)))
            .ToDictionary(x => x.Id, x => x.Count);

        _lblCount.Text = $"  {files.Count} fichier(s) recensé(s)";

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "Chemin complet", FillWeight = 55 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size", HeaderText = "Taille", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sha", HeaderText = "SHA256 (début)", FillWeight = 25 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dup", HeaderText = "Doublons", FillWeight = 8 });

        foreach (var f in files.OrderBy(x => x.FullPath))
        {
            int dup = groups.TryGetValue(f.Id, out var c) ? c : 1;
            var row = _grid.Rows[_grid.Rows.Add()];
            row.Cells["Path"].Value = f.FullPath;
            row.Cells["Size"].Value = FormatSize(f.FileSize);
            row.Cells["Sha"].Value = (f.Sha256 ?? "?")[..Math.Min(16, (f.Sha256 ?? "?").Length)] + "…";
            row.Cells["Dup"].Value = dup > 1 ? $"×{dup}" : "-";
            if (dup > 1) row.DefaultCellStyle.ForeColor = Color.FromArgb(255, 180, 80);
        }

        Controls.AddRange([_grid, _lblCount]);
    }

    private static string FormatSize(long b)
    {
        if (b < 1024) return $"{b} o";
        if (b < 1024 * 1024) return $"{b / 1024.0:F1} Ko";
        return $"{b / 1024.0 / 1024:F1} Mo";
    }
}
