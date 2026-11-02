using System.IO.Compression;
using System.Text.Json;

namespace Launcher.Core;

public sealed record GeodeMod(string Id, string Name, string Developer, string Version, bool Enabled, byte[]? Icon);

public static class GeodeModScanner
{
    public static IReadOnlyList<GeodeMod> Scan(string gameDirectory)
    {
        if (!GameService.IsValid(gameDirectory)) return Array.Empty<GeodeMod>();
        return ScanModsDirectory(Path.Combine(gameDirectory, "geode", "mods"));
    }

    public static IReadOnlyList<string> FindModsDirectories(string gameDirectory)
    {
        var paths = new List<string>();
        if (GameService.IsValid(gameDirectory)) paths.Add(Path.Combine(gameDirectory, "geode", "mods"));
        // The original launcher stores each installed game version in its own directory.
        var versions = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GDLauncherSteam", "versions");
        if (Directory.Exists(versions))
            foreach (var version in Directory.EnumerateDirectories(versions))
                if (GameService.IsValid(version)) paths.Add(Path.Combine(version, "geode", "mods"));
        return paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string? ChooseModsDirectory(string gameDirectory, string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred) && Directory.Exists(preferred)) return preferred;
        // Install into the game this launcher actually launches, when that game has Geode.
        var activeGameMods = Path.Combine(gameDirectory, "geode", "mods");
        if (GameService.HasGeode(gameDirectory))
        {
            Directory.CreateDirectory(activeGameMods);
            return activeGameMods;
        }
        return FindModsDirectories(gameDirectory).FirstOrDefault();
    }

    public static IReadOnlyList<GeodeMod> ScanModsDirectory(string root)
    {
        if (!Directory.Exists(root)) return Array.Empty<GeodeMod>();
        var result = new List<GeodeMod>();
        foreach (var file in Directory.EnumerateFiles(root).Where(p => p.EndsWith(".geode", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".geode.disabled", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                if (new FileInfo(file).Length > 250_000_000) continue;
                using var archive = ZipFile.OpenRead(file);
                var manifest = archive.GetEntry("mod.json");
                if (manifest == null || manifest.Length > 1_000_000) continue;
                using var manifestStream = manifest.Open();
                using var document = JsonDocument.Parse(manifestStream);
                var json = document.RootElement;
                string Field(string key) => json.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
                var id = Field("id");
                var name = Field("name"); if (name.Length == 0) name = id.Length > 0 ? id : Path.GetFileNameWithoutExtension(file);
                var developer = Field("developer");
                if (developer.Length == 0 && json.TryGetProperty("developers", out var developers) && developers.ValueKind == JsonValueKind.Array)
                    developer = string.Join(", ", developers.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()));
                byte[]? icon = null;
                var logo = archive.GetEntry("logo.png");
                if (logo != null && logo.Length is > 23 and <= 2_000_000)
                {
                    using var stream = logo.Open(); using var memory = new MemoryStream(); stream.CopyTo(memory);
                    var bytes = memory.ToArray();
                    if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                    {
                        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
                        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
                        if (width is > 0 and <= 2048 && height is > 0 and <= 2048) icon = bytes;
                    }
                }
                result.Add(new GeodeMod(id, name, developer, Field("version"), !file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase), icon));
            }
            catch (InvalidDataException) { }
            catch (JsonException) { }
            catch (IOException) { }
        }
        return result.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}
