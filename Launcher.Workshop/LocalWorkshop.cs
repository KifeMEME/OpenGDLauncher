using Launcher.Themes;
using System.IO.Compression;

namespace Launcher.Workshop;

public sealed class LocalWorkshop
{
    private readonly ThemeService themes;
    public LocalWorkshop(ThemeService themes) => this.themes = themes;
    public void Import(string archivePath)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        if (zip.Entries.Count > 100 || zip.Entries.Sum(e => e.Length) > 50_000_000) throw new InvalidDataException("Archive exceeds safety limits.");
        var manifestEntry = zip.Entries.SingleOrDefault(e => e.FullName.Equals("theme.json", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidDataException("theme.json missing.");
        if (manifestEntry.Length > 64_000) throw new InvalidDataException("Theme manifest is too large.");
        var name = Path.GetFileNameWithoutExtension(archivePath);
        if (name.Length is < 1 or > 64 || name.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_'))) throw new InvalidDataException("Archive name must contain only letters, digits, hyphens or underscores.");
        var target = Path.Combine(themes.Root, name);
        if (Directory.Exists(target)) throw new IOException("Theme already exists.");
        Directory.CreateDirectory(target);
        long totalExtracted = 0;
        try
        {
            foreach (var entry in zip.Entries)
            {
                var relative = entry.FullName.Replace('\\', '/');
                if (relative.EndsWith('/')) continue;
                if (relative.StartsWith('/') || relative.Contains("..") || relative.Contains(':') || (!relative.Equals("theme.json", StringComparison.OrdinalIgnoreCase) && !relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !relative.Equals("hero.wav", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Unsafe archive entry.");
                var limit = relative.Equals("theme.json", StringComparison.OrdinalIgnoreCase) ? 64_000 : relative.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? 5_000_000 : 10_000_000;
                if (entry.Length > limit) throw new InvalidDataException("File too large.");
                var output = Path.GetFullPath(Path.Combine(target, relative));
                if (!output.StartsWith(Path.GetFullPath(target) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe path.");
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                using var input = entry.Open();
                using var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
                var buffer = new byte[64 * 1024];
                int read; long extracted = 0;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    extracted += read; totalExtracted += read;
                    if (extracted > limit || totalExtracted > 50_000_000) throw new InvalidDataException("Archive exceeds safety limits.");
                    destination.Write(buffer, 0, read);
                }
            }
            themes.Load(name);
        }
        catch { Directory.Delete(target, true); throw; }
    }
}
