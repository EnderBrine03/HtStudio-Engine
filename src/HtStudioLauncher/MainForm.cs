using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HtStudio;

public class MainForm : Form
{
    readonly WebView2 _web = new();
    readonly string _indexPath;
    readonly string _workDir;

    public MainForm(PackageFormat.Manifest manifest, string indexPath, string workDir)
    {
        _indexPath = indexPath;
        _workDir = workDir;

        Text = string.IsNullOrWhiteSpace(manifest.Name) ? "HtStudio" : manifest.Name;
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 480);

        _web.Dock = DockStyle.Fill;
        Controls.Add(_web);

        Load += async (_, _) => await InitWebAsync();
        FormClosed += (_, _) =>
        {
            try { Directory.Delete(_workDir, true); } catch { }
        };
    }

    async Task InitWebAsync()
    {
        try
        {
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HtStudio", "WebView2");
            Directory.CreateDirectory(userData);

            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await _web.EnsureCoreWebView2Async(env);

            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _web.CoreWebView2.Settings.IsWebMessageEnabled = true;

            var uri = new Uri(_indexPath).AbsoluteUri;
            _web.CoreWebView2.Navigate(uri);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "WebView2 başlatılamadı.\n\n" +
                "Windows 10/11'de Edge WebView2 Runtime gerekir.\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/\n\n" +
                ex.Message,
                "HtStudio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }
}
