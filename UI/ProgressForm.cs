namespace DuplicateFinder.UI;

public sealed class ProgressForm : Form
{
    private readonly ProgressBar _bar;
    private readonly Label _lblFile;
    private readonly Label _lblCount;
    private readonly Button _btnCancel;
    private CancellationTokenSource? _cts;

    public CancellationToken CancellationToken => _cts!.Token;

    public ProgressForm()
    {
        Text = "Scan en cours…";
        Size = new Size(600, 160);
        MinimumSize = Size;
        MaximumSize = Size;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        BackColor = Color.FromArgb(22, 22, 30);
        ForeColor = Color.FromArgb(220, 220, 235);
        Font = new Font("Segoe UI", 10f);

        _lblCount = new Label
        {
            Text = "Initialisation…",
            Location = new Point(12, 12),
            AutoSize = false,
            Width = 560,
            Height = 20,
            ForeColor = Color.FromArgb(120, 180, 255)
        };

        _bar = new ProgressBar
        {
            Location = new Point(12, 38),
            Size = new Size(560, 22),
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100
        };

        _lblFile = new Label
        {
            Text = string.Empty,
            Location = new Point(12, 68),
            AutoSize = false,
            Width = 560,
            Height = 18,
            ForeColor = Color.FromArgb(160, 160, 185),
            Font = new Font("Consolas", 8.5f)
        };

        _btnCancel = new Button
        {
            Text = "Annuler",
            Location = new Point(464, 94),
            Size = new Size(108, 30),
            BackColor = Color.FromArgb(100, 30, 30),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnCancel.Click += (_, _) =>
        {
            _cts?.Cancel();
            _btnCancel.Enabled = false;
            _btnCancel.Text = "Annulation…";
        };

        Controls.AddRange([_lblCount, _bar, _lblFile, _btnCancel]);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _cts = new CancellationTokenSource();
    }

    public void UpdateProgress(Services.ScanProgress p)
    {
        if (IsDisposed) return;
        Invoke(() =>
        {
            _bar.Value = Math.Clamp(p.Percent, 0, 100);
            _lblCount.Text = $"Fichiers traités : {p.Done} / {p.Total}  ({p.Percent}%)";
            var shortPath = p.CurrentFile.Length > 80
                ? "…" + p.CurrentFile[^78..]
                : p.CurrentFile;
            _lblFile.Text = shortPath;
        });
    }

    public void MarkDone()
    {
        if (IsDisposed) return;
        Invoke(() =>
        {
            _bar.Value = 100;
            DialogResult = DialogResult.OK;
            Close();
        });
    }
}
