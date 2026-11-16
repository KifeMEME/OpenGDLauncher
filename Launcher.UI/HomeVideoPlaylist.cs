using System.IO;

namespace Launcher.UI;

internal static class HomeVideoPlaylist
{
    public static IReadOnlyList<string> Load(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        var orderFile = Path.Combine(directory, "playlist.txt");
        if (!File.Exists(orderFile)) return Directory.EnumerateFiles(directory, "*.mp4").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
        return File.ReadLines(orderFile)
            .Select(line => line.Trim())
            .Where(name => name.Length > 0 && !name.StartsWith('#') && name.Equals(Path.GetFileName(name), StringComparison.Ordinal) && name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            .Select(name => Path.Combine(directory, name))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }
}
