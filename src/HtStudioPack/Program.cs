using System.IO.Compression;
using System.Text;

namespace HtStudio;

/// <summary>
/// Kullanım: HtStudioPack <proje-klasörü> <çıkış.hts> [html|python|java]
/// Proje klasöründe genelde index.html olur.
/// </summary>
static class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Kullanım: HtStudioPack <proje-klasörü> <çıkış.hts> [html]");
            return 1;
        }

        var projectDir = Path.GetFullPath(args[0]);
        var outPath = Path.GetFullPath(args[1]);
        var typeStr = args.Length >= 3 ? args[2].ToLowerInvariant() : "html";

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine("Klasör yok: " + projectDir);
            return 1;
        }

        var type = typeStr switch
        {
            "html" or "htm" => PackageFormat.ContentType.Html,
            "python" or "py" => PackageFormat.ContentType.Python,
            "java" => PackageFormat.ContentType.Java,
            _ => PackageFormat.ContentType.Html
        };

        if (type != PackageFormat.ContentType.Html)
        {
            Console.WriteLine("Uyarı: Bu paketleyici sürümü HTML odaklı; tür yine de etikete yazılacak.");
        }

        var entry = File.Exists(Path.Combine(projectDir, "index.html")) ? "index.html"
            : Directory.GetFiles(projectDir, "*.html").Select(Path.GetFileName).FirstOrDefault() ?? "index.html";

        var manifest = new PackageFormat.Manifest
        {
            Id = SanitizeId(Path.GetFileName(projectDir)),
            Name = Path.GetFileName(projectDir),
            Version = "1.0.0",
            Entry = entry!,
            Type = "html"
        };

        byte[] zipBytes;
        using (var ms = new MemoryStream())
        {
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var file in Directory.EnumerateFiles(projectDir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(projectDir, file).Replace('\\', '/');
                    zip.CreateEntryFromFile(file, rel, CompressionLevel.Optimal);
                }
            }
            zipBytes = ms.ToArray();
        }

        var packet = PackageFormat.Build(type, manifest, zipBytes);
        File.WriteAllBytes(outPath, packet);
        Console.WriteLine($"Oluşturuldu: {outPath} ({packet.Length} bayt)");
        Console.WriteLine($"Tür: {type}  Entry: {manifest.Entry}  Hash: {manifest.ContentHash[..16]}...");
        return 0;
    }

    static string SanitizeId(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c is '.' or '_' or '-') sb.Append(c);
            else sb.Append('_');
        }
        var r = sb.ToString().Trim(' ', '.', '_');
        return string.IsNullOrEmpty(r) ? "app" : r[..Math.Min(80, r.Length)];
    }
}
