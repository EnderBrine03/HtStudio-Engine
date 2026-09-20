namespace HtStudio;

/// <summary>
/// Bağımsız HtStudio Launcher arayüzü (Rockstar Games Launcher tarzı hub).
/// HtStudio ile üretilmiş uygulamaları listeler, doğrular, başlatır.
/// </summary>
public class LauncherForm : Form
{
    readonly ListView _list = new();
    readonly Label _status = new();
    readonly Panel _side = new();
    readonly Label _detailTitle = new();
    readonly Label _detailBody = new();
    readonly Button _btnPlay = new();
    List<LibraryEntry> _apps = new();

    public LauncherForm()
    {
        Text = "HtStudio Launcher";
        Width = 1000;
        Height = 640;
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 18, 24);
        ForeColor = Color.FromArgb(230, 230, 235);
        Font = new Font("Segoe UI", 10f);

        // Top bar
        var top = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(28, 28, 36)
        };
        var title = new Label
        {
            Text = "HtStudio",
            Font = new Font("Segoe UI Semibold", 16f),
            ForeColor = Color.FromArgb(88, 166, 255),
            AutoSize = true,
            Location = new Point(16, 14)
        };
        var subtitle = new Label
        {
            Text = "Launcher",
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.FromArgb(160, 160, 170),
            AutoSize = true,
            Location = new Point(120, 18)
        };
        top.Controls.Add(title);
        top.Controls.Add(subtitle);

        var btnAdd = MakeButton("Uygulama ekle", 0);
        btnAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAdd.Location = new Point(Width - 280, 12);
        btnAdd.Click += (_, _) => AddApp();
        top.Controls.Add(btnAdd);

        var btnRefresh = MakeButton("Yenile", 0);
        btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRefresh.Location = new Point(Width - 150, 12);
        btnRefresh.Click += (_, _) => Reload();
        top.Controls.Add(btnRefresh);

        // Resize buttons with form
        Resize += (_, _) =>
        {
            btnAdd.Location = new Point(ClientSize.Width - 280, 12);
            btnRefresh.Location = new Point(ClientSize.Width - 150, 12);
        };

        // List
        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.BackColor = Color.FromArgb(22, 22, 30);
        _list.ForeColor = Color.FromArgb(230, 230, 235);
        _list.BorderStyle = BorderStyle.None;
        _list.Columns.Add("Uygulama", 280);
        _list.Columns.Add("Paket", 220);
        _list.Columns.Add("Yol", 400);
        _list.SelectedIndexChanged += (_, _) => UpdateDetail();
        _list.DoubleClick += (_, _) => PlaySelected();

        // Side panel
        _side.Dock = DockStyle.Right;
        _side.Width = 300;
        _side.BackColor = Color.FromArgb(24, 24, 32);
        _side.Padding = new Padding(16);

        _detailTitle.AutoSize = false;
        _detailTitle.Dock = DockStyle.Top;
        _detailTitle.Height = 40;
        _detailTitle.Font = new Font("Segoe UI Semibold", 14f);
        _detailTitle.ForeColor = Color.White;
        _detailTitle.Text = "Bir uygulama seç";

        _detailBody.AutoSize = false;
        _detailBody.Dock = DockStyle.Fill;
        _detailBody.ForeColor = Color.FromArgb(180, 180, 190);
        _detailBody.Text = "HtStudio ile üretilmiş uygulamaları ekle ve buradan başlat.";

        _btnPlay.Text = "▶  Oynat";
        _btnPlay.Dock = DockStyle.Bottom;
        _btnPlay.Height = 44;
        _btnPlay.FlatStyle = FlatStyle.Flat;
        _btnPlay.BackColor = Color.FromArgb(35, 134, 54);
        _btnPlay.ForeColor = Color.White;
        _btnPlay.FlatAppearance.BorderSize = 0;
        _btnPlay.Enabled = false;
        _btnPlay.Click += (_, _) => PlaySelected();

        var btnRemove = MakeButton("Kütüphaneden kaldır", 36);
        btnRemove.Dock = DockStyle.Bottom;
        btnRemove.BackColor = Color.FromArgb(50, 50, 60);
        btnRemove.Click += (_, _) => RemoveSelected();

        _side.Controls.Add(_detailBody);
        _side.Controls.Add(_detailTitle);
        _side.Controls.Add(btnRemove);
        _side.Controls.Add(_btnPlay);

        // Status
        _status.Dock = DockStyle.Bottom;
        _status.Height = 28;
        _status.BackColor = Color.FromArgb(14, 14, 18);
        _status.ForeColor = Color.FromArgb(140, 140, 150);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Padding = new Padding(12, 0, 0, 0);
        _status.Text = "  Hazır · Yalnızca HTSTUDIO_APP_V1 uygulamaları";

        Controls.Add(_list);
        Controls.Add(_side);
        Controls.Add(top);
        Controls.Add(_status);

        Load += (_, _) => Reload();
    }

    Button MakeButton(string text, int height)
    {
        var b = new Button
        {
            Text = text,
            Width = 120,
            Height = height > 0 ? height : 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 45, 58),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    void Reload()
    {
        _apps = AppLibrary.Load();
        // Drop missing files but keep entry (show warning in detail)
        _list.Items.Clear();
        foreach (var a in _apps.OrderBy(x => x.Name))
        {
            var item = new ListViewItem(a.Name);
            item.SubItems.Add(a.Pkg);
            item.SubItems.Add(a.ExePath);
            item.Tag = a;
            if (!File.Exists(a.ExePath))
                item.ForeColor = Color.FromArgb(180, 80, 80);
            _list.Items.Add(item);
        }
        _status.Text = $"  {_apps.Count} uygulama · HTSTUDIO_APP_V1";
        UpdateDetail();
    }

    void UpdateDetail()
    {
        if (_list.SelectedItems.Count == 0)
        {
            _detailTitle.Text = "Bir uygulama seç";
            _detailBody.Text = "Soldan bir HtStudio uygulaması seç veya «Uygulama ekle» ile kütüphaneye ekle.";
            _btnPlay.Enabled = false;
            return;
        }
        var a = (LibraryEntry)_list.SelectedItems[0].Tag!;
        _detailTitle.Text = a.Name;
        var exists = File.Exists(a.ExePath);
        _detailBody.Text =
            $"Paket: {(string.IsNullOrEmpty(a.Pkg) ? "—" : a.Pkg)}\n" +
            $"Yol: {a.ExePath}\n" +
            $"Durum: {(exists ? "Hazır" : "Dosya bulunamadı")}\n" +
            $"Son oynatma: {(string.IsNullOrEmpty(a.LastPlayed) ? "—" : a.LastPlayed)}";
        _btnPlay.Enabled = exists;
    }

    void AddApp()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "HtStudio uygulaması seç (.exe)",
            Filter = "HtStudio (*.exe;*.hts)|*.exe;*.hts|EXE (*.exe)|*.exe|Paket (*.hts)|*.hts",
            CheckFileExists = true
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        var path = ofd.FileName;
        string name;
        string pkg = "";

        if (path.EndsWith(".hts", StringComparison.OrdinalIgnoreCase))
        {
            var opened = PackageFormat.Open(path);
            if (!opened.Ok)
            {
                MessageBox.Show(this, opened.Error, "HtStudio Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            name = string.IsNullOrWhiteSpace(opened.Manifest?.Name)
                ? Path.GetFileNameWithoutExtension(path)
                : opened.Manifest!.Name;
            pkg = opened.Type.ToString();
        }
        else
        {
            var v = MarkerVerifier.VerifyExe(path);
            if (!v.Ok)
            {
                MessageBox.Show(this, v.Error, "HtStudio Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            name = string.IsNullOrWhiteSpace(v.Info?.Name)
                ? Path.GetFileNameWithoutExtension(path)
                : v.Info!.Name;
            pkg = v.Info?.Pkg ?? "";
        }

        var existing = _apps.FirstOrDefault(x =>
            string.Equals(x.ExePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Name = name;
            existing.Pkg = pkg;
        }
        else
        {
            _apps.Add(new LibraryEntry
            {
                Name = name,
                ExePath = path,
                Pkg = pkg
            });
        }
        AppLibrary.Save(_apps);
        Reload();
        _status.Text = $"  Eklendi: {name}";
    }

    void RemoveSelected()
    {
        if (_list.SelectedItems.Count == 0) return;
        var a = (LibraryEntry)_list.SelectedItems[0].Tag!;
        _apps.RemoveAll(x => x.Id == a.Id);
        AppLibrary.Save(_apps);
        Reload();
    }

    void PlaySelected()
    {
        if (_list.SelectedItems.Count == 0) return;
        var a = (LibraryEntry)_list.SelectedItems[0].Tag!;
        if (!File.Exists(a.ExePath))
        {
            MessageBox.Show(this, "Uygulama dosyası bulunamadı.", "HtStudio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (a.ExePath.EndsWith(".hts", StringComparison.OrdinalIgnoreCase))
        {
            PackageRunner.RunHts(a.ExePath, this);
        }
        else
        {
            // Her başlatmada tekrar doğrula
            var v = MarkerVerifier.VerifyExe(a.ExePath);
            if (!v.Ok)
            {
                MessageBox.Show(this, v.Error, "HtStudio Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!MarkerVerifier.TryLaunch(a.ExePath, out var err))
            {
                MessageBox.Show(this, "Başlatılamadı:\n" + err, "HtStudio",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        a.LastPlayed = DateTime.Now.ToString("g");
        AppLibrary.Save(_apps);
        _status.Text = $"  Başlatıldı: {a.Name}";
        UpdateDetail();
    }
}
