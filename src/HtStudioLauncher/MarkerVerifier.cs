using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace HtStudio;

/// <summary>
/// Hugging Face htstudio-builder-exxe ile uyumlu doğrulama.
/// Magic: HTSTUDIO_APP_V1
/// Sıra: yanındaki marker → resources/htstudio.marker → EXE içinde string ara
/// </summary>
public static class MarkerVerifier
{
    public const string Magic = "HTSTUDIO_APP_V1";

    public sealed class MarkerInfo
    {
        public string Magic { get; set; } = "";
        public string Name { get; set; } = "";
        public string Pkg { get; set; } = "";
        public string Builder { get; set; } = "";
        public string Platform { get; set; } = "";
        public string Source { get; set; } = ""; // sidecar | resources | binary
    }

    public sealed class VerifyResult
    {
        public bool Ok { get; init; }
        public string Error { get; init; } = "";
        public MarkerInfo? Info { get; init; }
    }

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static VerifyResult VerifyExe(string exePath)
    {
        if (!File.Exists(exePath))
            return Fail("Dosya bulunamadı: " + exePath);

        var dir = Path.GetDirectoryName(Path.GetFullPath(exePath))!;

        // 1) Yanındaki htstudio.marker veya *.htstudio
        var sidecar = FindSidecarMarker(dir);
        if (sidecar != null)
        {
            var parsed = ParseMarkerFile(sidecar);
            if (parsed.Ok)
            {
                parsed.Info!.Source = "sidecar:" + Path.GetFileName(sidecar);
                return parsed;
            }
            // bozuk marker varsa devam et, binary dene
        }

        // 2) resources/htstudio.marker (kurulu Electron)
        var resMarker = Path.Combine(dir, "resources", "htstudio.marker");
        if (File.Exists(resMarker))
        {
            var parsed = ParseMarkerFile(resMarker);
            if (parsed.Ok)
            {
                parsed.Info!.Source = "resources/htstudio.marker";
                return parsed;
            }
        }

        // 3) EXE içinde ASCII magic ara (ilk ~32 MB yeterli)
        if (BinaryContainsMagic(exePath, Magic))
        {
            return new VerifyResult
            {
                Ok = true,
                Info = new MarkerInfo
                {
                    Magic = Magic,
                    Name = Path.GetFileNameWithoutExtension(exePath),
                    Builder = "HtStudio",
                    Platform = "windows",
                    Source = "binary-scan"
                }
            };
        }

        return Fail(
            "Sorry, this application was not developed with HtStudio. " +
            "HtStudio Launcher only supports applications developed with HtStudio.");
    }

    static string? FindSidecarMarker(string dir)
    {
        var exact = Path.Combine(dir, "htstudio.marker");
        if (File.Exists(exact)) return exact;
        try
        {
            return Directory.GetFiles(dir, "*.htstudio").FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    static VerifyResult ParseMarkerFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var info = JsonSerializer.Deserialize<MarkerInfo>(json, JsonOpts);
            if (info is null)
                return Fail("Marker JSON okunamadı.");
            if (!string.Equals(info.Magic, Magic, StringComparison.Ordinal))
                return Fail("Marker magic geçersiz (HTSTUDIO_APP_V1 bekleniyor).");
            return new VerifyResult { Ok = true, Info = info };
        }
        catch (Exception ex)
        {
            return Fail("Marker dosyası bozuk: " + ex.Message);
        }
    }

    static bool BinaryContainsMagic(string path, string magic)
    {
        var needle = Encoding.ASCII.GetBytes(magic);
        const int chunk = 1024 * 1024;
        const long maxScan = 32L * 1024 * 1024; // 32 MB
        var buf = new byte[chunk + needle.Length];
        try
        {
            using var fs = File.OpenRead(path);
            long readTotal = 0;
            int carry = 0;
            while (readTotal < maxScan)
            {
                int n = fs.Read(buf, carry, chunk);
                if (n <= 0) break;
                int len = carry + n;
                if (IndexOf(buf, len, needle) >= 0)
                    return true;
                // overlap for boundary matches
                carry = Math.Min(needle.Length - 1, len);
                Buffer.BlockCopy(buf, len - carry, buf, 0, carry);
                readTotal += n;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    static int IndexOf(byte[] hay, int hayLen, byte[] needle)
    {
        for (int i = 0; i <= hayLen - needle.Length; i++)
        {
            int j = 0;
            for (; j < needle.Length; j++)
                if (hay[i + j] != needle[j]) break;
            if (j == needle.Length) return i;
        }
        return -1;
    }

    static VerifyResult Fail(string msg) => new() { Ok = false, Error = msg };

    public static bool TryLaunch(string exePath, out string error)
    {
        error = "";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(exePath))!,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
