using System.Text.Json;

namespace HtStudio;

public sealed class LibraryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string Pkg { get; set; } = "";
    public string AddedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string LastPlayed { get; set; } = "";
}

public static class AppLibrary
{
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static string StorePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HtStudio", "Launcher");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "library.json");
        }
    }

    public static List<LibraryEntry> Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return new List<LibraryEntry>();
            var json = File.ReadAllText(StorePath);
            return JsonSerializer.Deserialize<List<LibraryEntry>>(json) ?? new List<LibraryEntry>();
        }
        catch
        {
            return new List<LibraryEntry>();
        }
    }

    public static void Save(List<LibraryEntry> list)
    {
        File.WriteAllText(StorePath, JsonSerializer.Serialize(list, Opts));
    }
}
