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

        // args[0] = .hts paketi yolu (varsa)
        string? packagePath = args.Length > 0 ? args[0] : null;

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            using var ofd = new OpenFileDialog
            {
                Title = "HtStudio paketi seç (.hts)",
                Filter = "HtStudio paketi (*.hts)|*.hts|Tüm dosyalar (*.*)|*.*",
                CheckFileExists = true
            };
            if (ofd.ShowDialog() != DialogResult.OK)
                return;
            packagePath = ofd.FileName;
        }

        if (!File.Exists(packagePath))
        {
            MessageBox.Show("Dosya bulunamadı:\n" + packagePath, "HtStudio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

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
                $"Bu sürüm yalnızca HTML paketlerini çalıştırır.\nTür: {opened.Type}\n(Python/Java sonra eklenecek)",
                "HtStudio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Zip'i geçici klasöre aç
        var work = Path.Combine(Path.GetTempPath(), "HtStudio", opened.Manifest!.Id + "_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        try
        {
            var zipPath = Path.Combine(work, "payload.zip");
            File.WriteAllBytes(zipPath, opened.PayloadZip!);
            ZipFile.ExtractToDirectory(zipPath, work);
            File.Delete(zipPath);

            var entry = opened.Manifest.Entry;
            if (string.IsNullOrWhiteSpace(entry)) entry = "index.html";
            var index = Path.Combine(work, entry.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(index))
            {
                // fallback: kökte ilk html
                index = Directory.GetFiles(work, "*.html", SearchOption.AllDirectories).FirstOrDefault() ?? "";
            }
            if (string.IsNullOrEmpty(index) || !File.Exists(index))
            {
                MessageBox.Show("Pakette HTML giriş dosyası yok.", "HtStudio",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new MainForm(opened.Manifest, index, work));
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* kapanışta silinmeyebilir */ }
        }
    }
}
