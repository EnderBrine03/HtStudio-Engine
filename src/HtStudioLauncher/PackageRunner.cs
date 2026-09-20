using System.Diagnostics;
using System.IO.Compression;

namespace HtStudio;

public static class PackageRunner
{
    public static void RunHts(string packagePath, IWin32Window? owner = null)
    {
        var opened = PackageFormat.Open(packagePath);
        if (!opened.Ok)
        {
            MessageBox.Show(owner, opened.Error, "HtStudio — Geçersiz paket",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            switch (opened.Type)
            {
                case PackageFormat.ContentType.Html:
                    RunHtml(opened.Manifest, work, owner);
                    break;
                case PackageFormat.ContentType.Python:
                    RunPython(opened.Manifest, work, owner);
                    break;
                case PackageFormat.ContentType.Java:
                    RunJava(opened.Manifest, work, owner);
                    break;
                default:
                    MessageBox.Show(owner, "Bilinmeyen paket türü.", "HtStudio",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "Paket çalıştırılamadı:\n" + ex.Message, "HtStudio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            try { Directory.Delete(work, true); } catch { }
        }
    }

    static void RunHtml(PackageFormat.Manifest manifest, string work, IWin32Window? owner)
    {
        var entry = string.IsNullOrWhiteSpace(manifest.Entry) ? "index.html" : manifest.Entry;
        var index = Path.Combine(work, entry.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(index))
            index = Directory.GetFiles(work, "*.html", SearchOption.AllDirectories).FirstOrDefault() ?? "";
        if (string.IsNullOrEmpty(index) || !File.Exists(index))
        {
            MessageBox.Show(owner, "Pakette HTML yok.", "HtStudio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        // Form kapanınca work silinir
        var form = new MainForm(manifest, index, work);
        form.ShowDialog(owner);
    }

    static void RunPython(PackageFormat.Manifest manifest, string work, IWin32Window? owner)
    {
        if (!DependencyCheck.EnsurePython(owner))
            return;

        var py = DependencyCheck.FindPython();
        if (py == null)
        {
            MessageBox.Show(owner, "Python bulunamadı.", "HtStudio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var entry = string.IsNullOrWhiteSpace(manifest.Entry) ? "main.py" : manifest.Entry;
        var script = Path.Combine(work, entry.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(script))
            script = Directory.GetFiles(work, "*.py", SearchOption.AllDirectories).FirstOrDefault() ?? "";
        if (string.IsNullOrEmpty(script) || !File.Exists(script))
        {
            MessageBox.Show(owner, "Pakette .py dosyası yok.", "HtStudio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = py,
            Arguments = Quote(script),
            WorkingDirectory = Path.GetDirectoryName(script)!,
            UseShellExecute = false,
        };
        // py launcher: py -3 script.py
        if (Path.GetFileName(py).Equals("py.exe", StringComparison.OrdinalIgnoreCase))
            psi.Arguments = "-3 " + Quote(script);

        using var p = Process.Start(psi);
        p?.WaitForExit();
        try { Directory.Delete(work, true); } catch { }
    }

    static void RunJava(PackageFormat.Manifest manifest, string work, IWin32Window? owner)
    {
        if (!DependencyCheck.EnsureJava(owner))
            return;

        var java = DependencyCheck.FindJava();
        if (java == null)
        {
            MessageBox.Show(owner, "Java bulunamadı.", "HtStudio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // 1) jar varsa: java -jar app.jar
        var jar = Directory.GetFiles(work, "*.jar", SearchOption.AllDirectories).FirstOrDefault();
        ProcessStartInfo psi;
        if (jar != null)
        {
            psi = new ProcessStartInfo
            {
                FileName = java,
                Arguments = "-jar " + Quote(jar),
                WorkingDirectory = Path.GetDirectoryName(jar)!,
                UseShellExecute = false
            };
        }
        else
        {
            // 2) entry = MainClass veya MainClass.class
            var entry = string.IsNullOrWhiteSpace(manifest.Entry) ? "Main" : manifest.Entry;
            entry = entry.Replace(".class", "").Replace('/', '.');
            psi = new ProcessStartInfo
            {
                FileName = java,
                Arguments = "-cp " + Quote(work) + " " + entry,
                WorkingDirectory = work,
                UseShellExecute = false
            };
        }

        using var p = Process.Start(psi);
        p?.WaitForExit();
        try { Directory.Delete(work, true); } catch { }
    }

    static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";
}
