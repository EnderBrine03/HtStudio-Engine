using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HtStudio;

/// <summary>
/// HtStudio paket formatı (.hts)
/// [4 magic "HTS1"][1 formatVer][1 type][4 manifestLen LE][manifest JSON][4 payloadLen LE][payload XOR]
/// Token/etiket = magic + type + manifest; şifre/login değil.
/// </summary>
public static class PackageFormat
{
    public static readonly byte[] Magic = Encoding.ASCII.GetBytes("HTS1");
    public const byte FormatVersion = 1;

    // Hafif obfuscation anahtarı (login token değil; format açma etiketi)
    public static readonly byte[] FormatKey = Encoding.UTF8.GetBytes("HtStudio-Format-Key-v1");

    public enum ContentType : byte
    {
        Unknown = 0,
        Html = 1,
        Python = 2,
        Java = 3
    }

    public sealed class Manifest
    {
        public string Id { get; set; } = "app";
        public string Name { get; set; } = "HtStudio App";
        public string Version { get; set; } = "1.0.0";
        public string Entry { get; set; } = "index.html";
        public string Type { get; set; } = "html";
        public string ContentHash { get; set; } = "";
    }

    public sealed class OpenResult
    {
        public bool Ok { get; init; }
        public string Error { get; init; } = "";
        public Manifest? Manifest { get; init; }
        public ContentType Type { get; init; }
        public byte[]? PayloadZip { get; init; }
    }

    public static void XorInPlace(byte[] data, byte[] key)
    {
        for (int i = 0; i < data.Length; i++)
            data[i] ^= key[i % key.Length];
    }

    public static string Sha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static OpenResult Open(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            var magic = br.ReadBytes(4);
            if (magic.Length < 4 || !Magic.AsSpan().SequenceEqual(magic))
                return Fail("Bu bir HtStudio paketi değil (HTS1 imzası yok).");

            var ver = br.ReadByte();
            if (ver != FormatVersion)
                return Fail($"Desteklenmeyen paket sürümü: {ver}");

            var typeByte = br.ReadByte();
            var type = Enum.IsDefined(typeof(ContentType), typeByte)
                ? (ContentType)typeByte
                : ContentType.Unknown;

            if (type == ContentType.Unknown)
                return Fail("Bilinmeyen içerik türü.");

            var manifestLen = br.ReadInt32();
            if (manifestLen <= 0 || manifestLen > 1_000_000)
                return Fail("Geçersiz manifest boyutu.");

            var manifestBytes = br.ReadBytes(manifestLen);
            if (manifestBytes.Length != manifestLen)
                return Fail("Manifest okunamadı (dosya eksik/bozuk).");

            // Manifest düz JSON (etiket); isteğe bağlı XOR'suz bırakıyoruz ki debug kolay olsun
            var manifestJson = Encoding.UTF8.GetString(manifestBytes);
            var manifest = JsonSerializer.Deserialize<Manifest>(manifestJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest is null)
                return Fail("Manifest çözülemedi.");

            var payloadLen = br.ReadInt32();
            if (payloadLen <= 0 || payloadLen > 200_000_000)
                return Fail("Geçersiz içerik boyutu.");

            var payload = br.ReadBytes(payloadLen);
            if (payload.Length != payloadLen)
                return Fail("İçerik eksik (dosya kesilmiş olabilir).");

            XorInPlace(payload, FormatKey);

            var hash = Sha256Hex(payload);
            if (!string.IsNullOrEmpty(manifest.ContentHash) &&
                !hash.Equals(manifest.ContentHash, StringComparison.OrdinalIgnoreCase))
                return Fail("İçerik bozulmuş veya değiştirilmiş (hash uyuşmuyor).");

            // Tür etiketi tutarlı mı?
            var expected = manifest.Type?.ToLowerInvariant() switch
            {
                "html" or "htm" => ContentType.Html,
                "python" or "py" => ContentType.Python,
                "java" => ContentType.Java,
                _ => ContentType.Unknown
            };
            if (expected != ContentType.Unknown && expected != type)
                return Fail("Tür etiketi ile paket türü uyuşmuyor.");

            return new OpenResult
            {
                Ok = true,
                Manifest = manifest,
                Type = type,
                PayloadZip = payload
            };
        }
        catch (Exception ex)
        {
            return Fail("Paket açılamadı: " + ex.Message);
        }
    }

    static OpenResult Fail(string msg) => new() { Ok = false, Error = msg };

    public static byte[] Build(ContentType type, Manifest manifest, byte[] zipPayload)
    {
        manifest.ContentHash = Sha256Hex(zipPayload);
        manifest.Type = type switch
        {
            ContentType.Html => "html",
            ContentType.Python => "python",
            ContentType.Java => "java",
            _ => "unknown"
        };

        var manifestJson = JsonSerializer.Serialize(manifest);
        var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);

        var payload = (byte[])zipPayload.Clone();
        XorInPlace(payload, FormatKey);

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(Magic);
            bw.Write(FormatVersion);
            bw.Write((byte)type);
            bw.Write(manifestBytes.Length);
            bw.Write(manifestBytes);
            bw.Write(payload.Length);
            bw.Write(payload);
        }
        return ms.ToArray();
    }
}
