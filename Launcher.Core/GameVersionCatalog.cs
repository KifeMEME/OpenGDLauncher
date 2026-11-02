namespace Launcher.Core;

public sealed record GameVersionEntry(string Version, string? LocalDirectory, bool IsCurrentSteamVersion);

public static class GameVersionCatalog
{
    // Labels for the picker. DepotDownloaderService owns authenticated downloads.
    private static readonly string[] KnownVersions =
    [
        "1.9", "1.91", "1.92", "2.0", "2.01", "2.1", "2.11", "2.111",
        "2.113", "2.2", "2.201", "2.202", "2.203", "2.204", "2.206", "2.207",
        "2.2071", "2.2072", "2.2073", "2.2074", "2.208", "2.2081", "2.209", "2.2091"
    ];

    public static string? CurrentSteamVersion()
    {
        var game = GameService.Detect();
        if (!GameService.IsValid(game)) return null;
        return GeodeCatalogService.DetectRuntime(Path.Combine(game!, "geode", "mods")).GameVersion;
    }

    public static IReadOnlyList<GameVersionEntry> GetEntries()
    {
        var steamDirectory = GameService.Detect();
        var steamVersion = CurrentSteamVersion();
        var archiveRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GDLauncherSteam", "versions");
        var versions = new List<string>(KnownVersions);
        if (steamVersion is not null && !versions.Contains(steamVersion, StringComparer.OrdinalIgnoreCase)) versions.Add(steamVersion);
        return versions.Select(version =>
        {
            var isSteam = version == steamVersion && GameService.IsValid(steamDirectory);
            var archive = Path.Combine(archiveRoot, version);
            var managed = DepotDownloaderService.VersionDirectory(version);
            return new GameVersionEntry(version, DepotDownloaderService.IsDownloaded(version) ? managed : GameService.IsValid(archive) ? archive : null, isSteam);
        }).ToArray();
    }
}
