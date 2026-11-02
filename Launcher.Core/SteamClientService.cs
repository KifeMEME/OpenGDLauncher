using System.Text.RegularExpressions;

namespace Launcher.Core;

public sealed record SteamLocalCheck(bool? AccountMatches, bool HasInstallRecord);

/// <summary>Checks local Steam client hints. These cannot prove game ownership.</summary>
public static class SteamClientService
{
    public static SteamLocalCheck Check(string login, string gameDirectory)
    {
        login = login.Trim();
        if (login.Length is < 1 or > 64) throw new ArgumentException("Enter a Steam account login (1–64 characters).");
        var knownAccount = LastSteamAccount(gameDirectory);
        bool? matches = knownAccount == null ? null : string.Equals(login, knownAccount, StringComparison.OrdinalIgnoreCase);
        return new SteamLocalCheck(matches, GameService.HasSteamInstallRecord(gameDirectory));
    }

    private static string? LastSteamAccount(string gameDirectory)
    {
        if (!GameService.IsValid(gameDirectory)) return null;
        try
        {
            var game = new DirectoryInfo(gameDirectory);
            var steamapps = game.Parent?.Parent;
            if (!string.Equals(game.Parent?.Name, "common", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(steamapps?.Name, "steamapps", StringComparison.OrdinalIgnoreCase)) return null;
            var file = Path.Combine(steamapps!.Parent!.FullName, "config", "loginusers.vdf");
            if (!File.Exists(file) || new FileInfo(file).Length > 1_000_000) return null;
            var text = File.ReadAllText(file);
            foreach (Match account in Regex.Matches(text, "\"[0-9]{17}\"\\s*\\{(?<body>[^{}]*)\\}", RegexOptions.Singleline))
            {
                var body = account.Groups["body"].Value;
                if (!Regex.IsMatch(body, "\"MostRecent\"\\s*\"1\"", RegexOptions.IgnoreCase)) continue;
                var name = Regex.Match(body, "\"AccountName\"\\s*\"(?<name>[^\"]+)\"", RegexOptions.IgnoreCase);
                if (name.Success) return name.Groups["name"].Value;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }
}
