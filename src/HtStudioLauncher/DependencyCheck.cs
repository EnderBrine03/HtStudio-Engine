using System.Diagnostics;
using System.Net.Http;
using Microsoft.Win32;

namespace HtStudio;

/// <summary>
/// İlk açılışta gereksinimleri kontrol eder; eksikse yüklemeyi dener.
/// Self-contained build'de .NET zaten gömülü; asıl ihtiyaç WebView2 Runtime.
/// </summary>
public static class DependencyCheck
{
    const string WebView2BootstrapperUrl =
        "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    public static bool EnsureReady(IWin32Window? owner = null)
    {
        if (IsWebView2Installed())
            return true;

        var r = MessageBox.Show(
            owner,
            "HtStudio Launcher için Microsoft Edge WebView2 Runtime gerekli.\n\n" +
            "Şimdi otomatik indirilip kurulsun mu?\n\n" +
            "(Kurulum kısa sürer; yönetici onayı istenebilir.)",
            "HtStudio — Gereksinim",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (r != DialogResult.Yes)
        {
            MessageBox.Show(
                owner,
                "WebView2 olmadan HTML önizleme ve bazı özellikler çalışmayabilir.\n" +
                "Manuel indirme: https://developer.microsoft.com/microsoft-edge/webview2/",
                "HtStudio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            // Marker'lı EXE başlatma WebView2 istemez; yine de devam et
            return true;
        }

        try
        {
            return InstallWebView2(owner);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                owner,
                "WebView2 kurulumu başarısız:\n" + ex.Message +
                "\n\nManuel: https://developer.microsoft.com/microsoft-edge/webview2/",
                "HtStudio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return true; // launcher yine açılsın
        }
    }

    public static bool IsWebView2Installed()
    {
        // Evergreen runtime registry (user + machine, x64/x86)
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
                catch { /* ignore */ }
            }
        }

        // Fallback: fixed path
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var edge = Path.Combine(pf, "Microsoft", "EdgeWebView", "Application");
        if (Directory.Exists(edge) && Directory.GetDirectories(edge).Length > 0)
            return true;

        return false;
    }

    static bool InstallWebView2(IWin32Window? owner)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
        {
            // Sync download with simple UI wait
            var data = http.GetByteArrayAsync(WebView2BootstrapperUrl).GetAwaiter().GetResult();
            File.WriteAllBytes(tmp, data);
        }

        var psi = new ProcessStartInfo
        {
            FileName = tmp,
            // Sessiz kurulum (kullanıcı UAC görürse onaylar)
            Arguments = "/silent /install",
            UseShellExecute = true,
        };
        using var p = Process.Start(psi);
        p?.WaitForExit(10 * 60 * 1000);

        // Kısa bekleme — registry güncellensin
        Thread.Sleep(1500);

        if (IsWebView2Installed())
        {
            MessageBox.Show(owner, "WebView2 kuruldu.", "HtStudio",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        MessageBox.Show(owner,
            "Kurulum tamamlanmış görünmüyor. Launcher yine de açılacak.\n" +
            "Gerekirse WebView2'yi elle kurun.",
            "HtStudio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return true;
    }
}
