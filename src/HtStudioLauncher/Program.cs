using System.IO.Compression;

namespace HtStudio;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string? path = args.Length > 0 ? args[0] : null;

        if (string.IsNullOrWhiteSpace(path))
        {
            using var ofd = new OpenFileDialog
            {
                Title = "HtStudio uygulaması veya paketi seç",
                Filter =
                    "HtStudio (*.exe;*.hts;*.marker;*.htstudio)|*.exe;*.hts;*.marker;*.htstudio|" +
                    "EXE (*.exe)|*.exe|HtStudio paket (*.hts)|*.hts|Tüm dosyalar (*.*)|*.*",
                CheckFileExists = true
            };
            if (ofd.ShowDialog() != DialogResult.OK)
                return;
            path = ofd.FileName;
        }

        if (!File.Exists(path))
        {
            MessageBox.Show("Dosya bulunamadı:\n" + path, "HtStudio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();

        // --- HF builder EXE / marker yolu ---
        if (ext == ".exe")
        {
            var v = MarkerVerifier.VerifyExe(path);
            if (!v.Ok)
            {
                MessageBox.Show(v.Error, "HtStudio Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var name = string.IsNullOrWhiteSpace(v.Info?.Name) ? Path.GetFileName(path) : v.Info!.Name;
            // Sessizce çalıştır; istersen bilgi göster
            if (!MarkerVerifier.TryLaunch(path, out var err))
            {
                MessageBox.Show("Uygulama başlatılamadı:\n" + err, "HtStudio",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        // Marker dosyası seçildiyse yanındaki exe'yi bul
        if (ext is ".marker" or ".htstudio")
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
            var exe = Directory.GetFiles(dir, "*.exe").FirstOrDefault();
            if (exe == null)
            {
                MessageBox.Show("Bu klasörde çalıştırılacak .exe yok.", "HtStudio",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            path = exe;
            var v = MarkerVerifier.VerifyExe(path);
            if (!v.Ok)
            {
                MessageBox.Show(v.Error, "HtStudio Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MarkerVerifier.TryLaunch(path, out _);
            return;
        }

        // --- Eski .hts paket yolu (HTML WebView2) ---
        if (ext == ".hts")
        {
            RunHtsPackage(path);
            return;
        }

        MessageBox.Show(
            "Desteklenen: HtStudio ile üretilmiş .exe (HTSTUDIO_APP_V1) veya .hts paketi.",
            "HtStudio", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    static void RunHtsPackage(string packagePath)
    {
        var opened = PackageFormat.Open(packagePath);
        if (!opened.Ok)
        {
            MessageBox.Show(opened.Error, "HtStudio — Geçersiz paket",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (opened.Type != PackageFormat.ContentType.Html)
        {
            MessageBox.Show(
                $"Bu sürüm yalnızca HTML paketlerini WebView ile çalıştırır.\nTür: {opened.Type}",
                "HtStudio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var work = Path.Combine(Path.GetTempPath(), "HtStudio",
            opened.Manifest!.Id + "_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        try
        {
            var zipPath = Path.Combine(work, "payload.zip");
            File.WriteAllBytes(zipPath, opened.PayloadZip!);
            ZipFile.ExtractToDirectory(zipPath, work);
            File.Delete(zipPath);

            var entry = string.IsNullOrWhiteSpace(opened.Manifest.Entry)
                ? "index.html" : opened.Manifest.Entry;
            var index = Path.Combine(work, entry.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(index))
                index = Directory.GetFiles(work, "*.html", SearchOption.AllDirectories).FirstOrDefault() ?? "";

            if (string.IsNullOrEmpty(index) || !File.Exists(index))
            {
                MessageBox.Show("Pakette HTML yok.", "HtStudio",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new MainForm(opened.Manifest, index, work));
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { }
        }
    }
}
