namespace HtStudio;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        DependencyCheck.EnsureReady();

        if (args.Length > 0 && File.Exists(args[0]))
        {
            var path = Path.GetFullPath(args[0]);
            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".exe")
            {
                var v = MarkerVerifier.VerifyExe(path);
                if (!v.Ok)
                {
                    MessageBox.Show(v.Error, "HtStudio Launcher",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var apps = AppLibrary.Load();
                var name = string.IsNullOrWhiteSpace(v.Info?.Name)
                    ? Path.GetFileNameWithoutExtension(path) : v.Info!.Name;
                if (!apps.Any(x => string.Equals(x.ExePath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    apps.Add(new LibraryEntry
                    {
                        Name = name,
                        ExePath = path,
                        Pkg = v.Info?.Pkg ?? ""
                    });
                    AppLibrary.Save(apps);
                }
                MarkerVerifier.TryLaunch(path, out _);
            }
            else if (ext == ".hts")
            {
                PackageRunner.RunHts(path);
            }
        }

        Application.Run(new LauncherForm());
    }
}
