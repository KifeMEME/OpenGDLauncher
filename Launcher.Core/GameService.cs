using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Launcher.Core;

public sealed class AppSettings
{
    public string GameDirectory { get; set; } = "";
    public string GeodeModsDirectory { get; set; } = "";
    public string Language { get; set; } = "ru";
    public string Theme { get; set; } = "Default";
    public string WorkshopUrl { get; set; } = "";
    public string SelectedVersion { get; set; } = "";
}

public static class SettingsStore
{
    public static string DirectoryPath
    {
        get
        {
            var userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenGDLauncher");
            try
            {
                Directory.CreateDirectory(userPath);
                var probe = Path.Combine(userPath, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return userPath;
            }
            catch (UnauthorizedAccessException) { return Path.Combine(AppContext.BaseDirectory, "UserData"); }
        }
    }
    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }
    public static void Save(AppSettings value)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class GameService
{
    public const int SteamAppId = 322170;
    public static bool IsValid(string? directory) => !string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, "GeometryDash.exe"));
    public static bool HasGeode(string? directory) => IsValid(directory) && (File.Exists(Path.Combine(directory!, "Geode.dll")) || Directory.Exists(Path.Combine(directory!, "geode")));
    public static string? Detect()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            foreach (var key in new[] { @"SOFTWARE\Valve\Steam", @"SOFTWARE\WOW6432Node\Valve\Steam" })
                try
                {
                    using var sub = hive.OpenSubKey(key);
                    var path = (sub?.GetValue("SteamPath") ?? sub?.GetValue("InstallPath")) as string;
                    if (!string.IsNullOrWhiteSpace(path)) roots.Add(path.Replace('/', '\\'));
                } catch { }
        foreach (var basePath in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            roots.Add(Path.Combine(basePath, "Steam"));
        foreach (var steamRoot in roots)
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };
            var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
                    libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
            foreach (var library in libraries)
            {
                var candidate = Path.Combine(library, "steamapps", "common", "Geometry Dash");
                if (IsValid(candidate)) return candidate;
            }
        }
        return null;
    }
    public static Process Launch(string directory)
    {
        if (!IsValid(directory)) throw new FileNotFoundException("GeometryDash.exe not found.");
        return Process.Start(new ProcessStartInfo(Path.Combine(directory, "GeometryDash.exe")) { WorkingDirectory = directory, UseShellExecute = true })
            ?? throw new InvalidOperationException("GeometryDash.exe did not start.");
    }
    public static Process LaunchDownloadedVersion(string version)
    {
        if (!DepotDownloaderService.IsDownloaded(version)) throw new InvalidOperationException("The selected Steam depot version is not fully downloaded.");
        var directory = DepotDownloaderService.VersionDirectory(version);
        // Steamworks documents this file for starting an app outside Steam's library folder.
        // SteamAPI_Init still requires the active Steam account to own the app.
        var appIdFile = Path.Combine(directory, "steam_appid.txt");
        File.WriteAllText(appIdFile, SteamAppId.ToString(), System.Text.Encoding.ASCII);
        return Launch(directory);
    }
    public static bool HasSteamInstallRecord(string directory)
    {
        if (!IsValid(directory)) return false;
        try
        {
            var game = new DirectoryInfo(Path.GetFullPath(directory));
            var common = game.Parent; var steamapps = common?.Parent;
            if (!string.Equals(common?.Name, "common", StringComparison.OrdinalIgnoreCase) || !string.Equals(steamapps?.Name, "steamapps", StringComparison.OrdinalIgnoreCase)) return false;
            var manifest = Path.Combine(steamapps!.FullName, "appmanifest_322170.acf");
            if (!File.Exists(manifest)) return false;
            var contents = File.ReadAllText(manifest);
            return Regex.IsMatch(contents, "\"appid\"\\s*\"322170\"", RegexOptions.IgnoreCase) && Regex.IsMatch(contents, "\"installdir\"\\s*\"" + Regex.Escape(game.Name) + "\"", RegexOptions.IgnoreCase);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    public static void LaunchThroughSteam() => OpenUrl("steam://run/322170");
    public static void OpenSteamStore() => OpenUrl("https://store.steampowered.com/app/322170/");
    public static void OpenSteamInstall() => OpenUrl("steam://install/322170");
    public static void OpenSteamUpdate() => OpenUrl("steam://open/games");
    public static void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        start.ArgumentList.Add(path);
        Process.Start(start);
    }
    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || (parsed.Scheme != "https" && parsed.Scheme != "steam"))
            throw new ArgumentException("Only HTTPS and Steam links are allowed.");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
