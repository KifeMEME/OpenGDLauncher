using System.Text.Json;

namespace Launcher.Core;

public sealed class PreviewSettings
{
    public bool EnableVideoPreview { get; set; } = true;
}

public sealed class PreviewSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string FilePath { get; }

    public PreviewSettingsStore(string? directory = null)
    {
        FilePath = Path.Combine(directory ?? SettingsStore.DirectoryPath, "preview.json");
    }

    public PreviewSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<PreviewSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
            var defaults = new PreviewSettings();
            Save(defaults);
            return defaults;
        }
        catch (IOException) { return new PreviewSettings(); }
        catch (UnauthorizedAccessException) { return new PreviewSettings(); }
        catch (JsonException) { return new PreviewSettings(); }
    }

    public void Save(PreviewSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
