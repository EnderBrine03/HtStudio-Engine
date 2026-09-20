namespace HtStudio;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Komut satırından .exe verilirse: doğrula + kütüphaneye ekle + başlat (opsiyonel kısayol)
        if (args.Length > 0 && File.Exists(args[0]) &&
            args[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var v = MarkerVerifier.VerifyExe(args[0]);
            if (!v.Ok)
            {
                MessageBox.Show(v.Error, "HtStudio Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var apps = AppLibrary.Load();
            var name = string.IsNullOrWhiteSpace(v.Info?.Name)
                ? Path.GetFileNameWithoutExtension(args[0]) : v.Info!.Name;
            if (!apps.Any(x => string.Equals(x.ExePath, args[0], StringComparison.OrdinalIgnoreCase)))
            {
                apps.Add(new LibraryEntry
                {
                    Name = name,
                    ExePath = Path.GetFullPath(args[0]),
                    Pkg = v.Info?.Pkg ?? ""
                });
                AppLibrary.Save(apps);
            }
            MarkerVerifier.TryLaunch(args[0], out _);
            // Hub'ı da aç
        }

        // Bağımsız launcher arayüzü (Rockstar tarzı)
        Application.Run(new LauncherForm());
    }
}
