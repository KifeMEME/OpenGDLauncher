using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace Launcher.Themes;

public sealed class ThemeManifest
{
    public string Name { get; set; } = "Untitled";
    public string Author { get; set; } = "Local user";
    public string Version { get; set; } = "1.0";
    public string BackgroundColor { get; set; } = "#18202B";
    public string PanelColor { get; set; } = "#253244";
    public string ButtonColor { get; set; } = "#3976B9";
    public string TextColor { get; set; } = "#FFFFFF";
    public string MutedTextColor { get; set; } = "#B8C6D9";
    public string? RibbonColor { get; set; }
    public string? RibbonTextColor { get; set; }
    public string? VersionBackgroundColor { get; set; }
    public string? VersionCardColor { get; set; }
    public string? VersionSelectedColor { get; set; }
    public string? SteamIndicatorColor { get; set; }
    public string FontFamily { get; set; } = "LemonMilk";
    public double PanelOpacity { get; set; } = 1;
    public string? BackgroundImage { get; set; }
    public string? SidebarImage { get; set; }
    public string? HeaderImage { get; set; }
    public string? LanguageRuImage { get; set; }
    public string? LanguageEnImage { get; set; }
    public string? PanelImage { get; set; }
    public string? CardImage { get; set; }
    public string? LogoImage { get; set; }
    public string? IconImage { get; set; }
    public string? DecorationImage { get; set; }
    public string? HeroImage { get; set; }
    public string? HeroSound { get; set; }
    public bool HeroSoundEnabled { get; set; } = true;
    public string? RibbonImage { get; set; }
    public string? SteamCardImage { get; set; }
    public string? ContentCardImage { get; set; }
    public string? ModRowImage { get; set; }
    public string? VersionBackgroundImage { get; set; }
    public string? VersionCardNormalImage { get; set; }
    public string? VersionCardHoverImage { get; set; }
    public string? VersionCardSelectedImage { get; set; }
    public string? VersionButtonNormalImage { get; set; }
    public string? VersionButtonHoverImage { get; set; }
    public string? VersionButtonPressedImage { get; set; }
    public string? ButtonNormalImage { get; set; }
    public string? ButtonHoverImage { get; set; }
    public string? ButtonPressedImage { get; set; }
    public string? ButtonDisabledImage { get; set; }
    public string? NavigationNormalImage { get; set; }
    public string? NavigationHoverImage { get; set; }
    public string? NavigationPressedImage { get; set; }
    public string? NavigationDisabledImage { get; set; }
    public IEnumerable<string?> Images() => new[] { BackgroundImage, SidebarImage, HeaderImage, LanguageRuImage, LanguageEnImage, PanelImage, CardImage, LogoImage, IconImage, DecorationImage, HeroImage, RibbonImage, SteamCardImage, ContentCardImage, ModRowImage, VersionBackgroundImage, VersionCardNormalImage, VersionCardHoverImage, VersionCardSelectedImage, VersionButtonNormalImage, VersionButtonHoverImage, VersionButtonPressedImage, ButtonNormalImage, ButtonHoverImage, ButtonPressedImage, ButtonDisabledImage, NavigationNormalImage, NavigationHoverImage, NavigationPressedImage, NavigationDisabledImage };
}

public sealed class ThemeService
{
    public string Root { get; }
    public ThemeService(string root) { Root = root; Directory.CreateDirectory(root); EnsureDefault(); }
    public IEnumerable<string> Names() => Directory.EnumerateDirectories(Root).Where(x => File.Exists(Path.Combine(x, "theme.json"))).Select(Path.GetFileName)!;
    public ThemeManifest Load(string name)
    {
        if (!SafeName(name)) throw new InvalidDataException("Invalid theme name.");
        var path = Path.Combine(Root, name);
        var manifestFile = Path.Combine(path, "theme.json");
        if (new FileInfo(manifestFile).Length > 64_000) throw new InvalidDataException("Theme manifest is too large.");
        var manifest = JsonSerializer.Deserialize<ThemeManifest>(File.ReadAllText(manifestFile), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Invalid theme manifest.");
        foreach (var color in new[] { manifest.BackgroundColor, manifest.PanelColor, manifest.ButtonColor, manifest.TextColor, manifest.MutedTextColor })
            if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) throw new InvalidDataException("Invalid color.");
        foreach (var color in new[] { manifest.RibbonColor, manifest.RibbonTextColor, manifest.VersionBackgroundColor, manifest.VersionCardColor, manifest.VersionSelectedColor, manifest.SteamIndicatorColor })
            if (color != null && !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) throw new InvalidDataException("Invalid color.");
        if (manifest.PanelOpacity is < 0 or > 1 || double.IsNaN(manifest.PanelOpacity)) throw new InvalidDataException("Invalid panel opacity.");
        if (manifest.FontFamily.Length > 100 || manifest.Name.Length > 100 || manifest.Author.Length > 100) throw new InvalidDataException("Text field too long.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(manifest.FontFamily, "^[\\p{L}\\p{N} ,._-]+$")) throw new InvalidDataException("Invalid font name.");
        foreach (var item in manifest.Images())
            if (item != null) ValidateImage(path, item);
        if (manifest.HeroSound != null) ValidateSound(path, manifest.HeroSound);
        return manifest;
    }
    public string? ImagePath(string name, string? relative)
    {
        if (relative == null) return null;
        if (name == "Default")
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "ThemeAssets", "Default", relative);
            if (File.Exists(bundled)) return bundled;
        }
        var path = Path.Combine(Root, name, relative);
        return File.Exists(path) ? path : null;
    }
    public string? ResolveImage(string themeName, string? custom, string? standard) => ImagePath(themeName, custom) ?? ImagePath("Default", standard);
    public string? ResolveSound(string themeName, string? custom, string? standard) => SoundPath(themeName, custom) ?? SoundPath("Default", standard);
    private string? SoundPath(string name, string? relative)
    {
        if (relative == null) return null;
        if (name == "Default")
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "ThemeAssets", "Default", relative);
            if (File.Exists(bundled)) return bundled;
        }
        var path = Path.Combine(Root, name, relative);
        return File.Exists(path) ? path : null;
    }
    public void Delete(string name)
    {
        if (name == "Default" || !SafeName(name)) throw new InvalidOperationException("Theme cannot be deleted.");
        Directory.Delete(Path.Combine(Root, name), true);
    }
    private static bool SafeName(string name) => name.Length is > 0 and <= 64 && name.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    private static void ValidateImage(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains("..") || relative.Contains(':') || !relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Only local PNG images are allowed.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid image path.");
        if (!File.Exists(path)) return; // fallback to the default theme
        if (new FileInfo(path).Length > 10_000_000) throw new InvalidDataException("Image is too large.");
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[24];
        if (stream.Read(header) != 24 || !header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException("Invalid PNG.");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
        if (width < 1 || height < 1 || width > 4096 || height > 4096) throw new InvalidDataException("Image dimensions exceed limits.");
    }
    private static void ValidateSound(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains("..") || relative.Contains(':') || !relative.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Only local WAV audio is allowed.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid audio path.");
        if (!File.Exists(path)) return;
        if (new FileInfo(path).Length > 5_000_000) throw new InvalidDataException("Audio is too large.");
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        if (stream.Read(header) != 12 || !header[..4].SequenceEqual("RIFF"u8) || !header[8..].SequenceEqual("WAVE"u8)) throw new InvalidDataException("Invalid WAV file.");
    }
    private void EnsureDefault()
    {
        var folder = Path.Combine(Root, "Default"); Directory.CreateDirectory(folder);
        var bundled = Path.Combine(AppContext.BaseDirectory, "ThemeAssets", "Default");
        if (Directory.Exists(bundled))
        {
            foreach (var source in Directory.EnumerateFiles(bundled, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(folder, Path.GetRelativePath(bundled, source));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                try
                {
                    // WPF may keep an image open in another launcher instance. Skip identical assets,
                    // and defer replacement of a locked old asset until the next launch.
                    if (File.Exists(destination) && SameContent(source, destination)) continue;
                    File.Copy(source, destination, true);
                }
                catch (IOException) when (File.Exists(destination)) { }
            }
        }
        else
        {
            var path = Path.Combine(folder, "theme.json");
            if (!File.Exists(path)) File.WriteAllText(path, JsonSerializer.Serialize(new ThemeManifest { Name = "Default", Author = "OpenGDLauncher" }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private static bool SameContent(string left, string right)
    {
        if (new FileInfo(left).Length != new FileInfo(right).Length) return false;
        using var a = File.OpenRead(left);
        using var b = File.OpenRead(right);
        return SHA256.HashData(a).SequenceEqual(SHA256.HashData(b));
    }
}
