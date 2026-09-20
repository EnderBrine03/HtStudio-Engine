using System.Diagnostics;
using System.Net.Http;
using Microsoft.Win32;

namespace HtStudio;

/// <summary>
/// İlk açılışta gereksinimleri kontrol eder; eksikse yüklemeyi dener.
/// WebView2 (HTML), Python, Java.
/// </summary>
public static class DependencyCheck
{
    const string WebView2BootstrapperUrl =
        "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    public static void EnsureReady(IWin32Window? owner = null)
    {
        // HTML motoru
        if (!IsWebView2Installed())
            TryInstallWebView2(owner);

        // Python / Java: sessiz kontrol; yoksa uyarı (zorunlu kurulum değil — sadece ilgili paket açılınca gerekir)
        // İlk açılışta bilgilendir
        var py = FindPython();
        var java = FindJava();
        if (py == null || java == null)
        {
            var missing = new List<string>();
            if (py == null) missing.Add("Python");
            if (java == null) missing.Add("Java (JRE/JDK)");
            // Sessiz: sadece status; kullanıcı Python/Java paketi açınca tekrar sorulacak
            Debug.WriteLine("Eksik runtime: " + string.Join(", ", missing));
        }
    }

    public static bool EnsurePython(IWin32Window? owner = null)
    {
        if (FindPython() != null) return true;

        var r = MessageBox.Show(owner,
            "Bu HtStudio uygulaması Python gerektiriyor.\n\n" +
            "Python yüklü değil. Microsoft Store / python.org üzerinden kurulum denensin mi?\n\n" +
            "Not: Kurulumdan sonra Launcher'ı yeniden açman gerekebilir.",
            "HtStudio — Python",
            MessageBoxButtons.YesNo, MessageBoxIcon.Information);

        if (r != DialogResult.Yes) return false;

        // winget ile dene
        if (TryWingetInstall("Python.Python.3.12") || TryWingetInstall("Python.Python.3.11"))
        {
            Thread.Sleep(2000);
            if (FindPython() != null)
            {
                MessageBox.Show(owner, "Python kuruldu.", "HtStudio",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
        }

        // Store ms-windows-store
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-windows-store://pdp/?ProductId=9PJPW5LDXLZ5",
                UseShellExecute = true
            });
        }
        catch { /* ignore */ }

        MessageBox.Show(owner,
            "Otomatik kurulum tamamlanamadı.\n" +
            "https://www.python.org/downloads/ adresinden Python kurup PATH'e ekleyin.",
            "HtStudio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return FindPython() != null;
    }

    public static bool EnsureJava(IWin32Window? owner = null)
    {
        if (FindJava() != null) return true;

        var r = MessageBox.Show(owner,
            "Bu HtStudio uygulaması Java gerektiriyor.\n\n" +
            "Java (JRE/JDK) yüklü değil. winget ile Microsoft Build of OpenJDK denensin mi?",
            "HtStudio — Java",
            MessageBoxButtons.YesNo, MessageBoxIcon.Information);

        if (r != DialogResult.Yes) return false;

        if (TryWingetInstall("Microsoft.OpenJDK.17") || TryWingetInstall("Microsoft.OpenJDK.21"))
        {
            Thread.Sleep(2000);
            if (FindJava() != null)
            {
                MessageBox.Show(owner, "Java kuruldu.", "HtStudio",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://learn.microsoft.com/en-us/java/openjdk/download",
                UseShellExecute = true
            });
        }
        catch { }

        MessageBox.Show(owner,
            "Otomatik kurulum tamamlanamadı.\nJava'yı kurup PATH'e ekleyin.",
            "HtStudio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return FindJava() != null;
    }

    public static string? FindPython()
    {
        foreach (var cmd in new[] { "py", "python", "python3" })
        {
            var p = Which(cmd);
            if (p != null) return p;
        }
        return null;
    }

    public static string? FindJava()
    {
        var p = Which("java");
        if (p != null) return p;
        // JAVA_HOME
        var home = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(home))
        {
            var java = Path.Combine(home, "bin", "java.exe");
            if (File.Exists(java)) return java;
        }
        return null;
    }

    static string? Which(string name)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = name,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            var line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            return line != null && File.Exists(line) ? line : null;
        }
        catch { return null; }
    }

    static bool TryWingetInstall(string id)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"install -e --id {id} --accept-package-agreements --accept-source-agreements --silent",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(10 * 60 * 1000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    public static bool IsWebView2Installed()
    {
        string[] keys =
        {
            @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
            @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        };
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var k in keys)
            {
                try
                {
                    using var key = root.OpenSubKey(k);
                    var pv = key?.GetValue("pv") as string;
                    if (!string.IsNullOrEmpty(pv) && pv != "0.0.0.0")
                        return true;
                }
                catch { }
            }
        }
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var edge = Path.Combine(pf, "Microsoft", "EdgeWebView", "Application");
        return Directory.Exists(edge) && Directory.GetDirectories(edge).Length > 0;
    }

    static void TryInstallWebView2(IWin32Window? owner)
    {
        var r = MessageBox.Show(owner,
            "HtStudio Launcher için Microsoft Edge WebView2 Runtime gerekli (HTML).\n\n" +
            "Şimdi otomatik indirilip kurulsun mu?",
            "HtStudio — Gereksinim",
            MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (r != DialogResult.Yes) return;

        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                var data = http.GetByteArrayAsync(WebView2BootstrapperUrl).GetAwaiter().GetResult();
                File.WriteAllBytes(tmp, data);
            }
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = tmp,
                Arguments = "/silent /install",
                UseShellExecute = true
            });
            p?.WaitForExit(10 * 60 * 1000);
            Thread.Sleep(1500);
            if (IsWebView2Installed())
                MessageBox.Show(owner, "WebView2 kuruldu.", "HtStudio",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "WebView2 kurulumu başarısız:\n" + ex.Message,
                "HtStudio", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
