using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Launcher.Core;

public sealed record SteamAuthSession(string Username, string CachePath, byte[] CacheBytes);

public static class SteamAuthStore
{
    private sealed record ProtectedSession(int Version, string Username, string CachePath, string Ciphertext);
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OpenGDLauncher.DepotDownloader.SteamAuth.v1");
    private static string FilePath => Path.Combine(SettingsStore.DirectoryPath, "steam-auth.json");

    public static string? AccountName
    {
        get { try { return Load()?.Username; } catch { return null; } }
    }

    public static SteamAuthSession? Load()
    {
        if (!File.Exists(FilePath)) return null;
        var stored = JsonSerializer.Deserialize<ProtectedSession>(File.ReadAllText(FilePath))
            ?? throw new InvalidDataException("Steam sign-in file is invalid.");
        if (stored.Version != 1 || string.IsNullOrWhiteSpace(stored.Username) || !IsAllowedCachePath(stored.CachePath))
            throw new InvalidDataException("Steam sign-in file has an invalid format.");
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(stored.Ciphertext), Entropy, DataProtectionScope.CurrentUser);
        return new SteamAuthSession(stored.Username, stored.CachePath, bytes);
    }

    public static void Save(string username, string cachePath, byte[] cacheBytes)
    {
        if (string.IsNullOrWhiteSpace(username) || !IsAllowedCachePath(cachePath) || cacheBytes.Length is < 1 or > 1_000_000)
            throw new InvalidDataException("DepotDownloader did not provide a valid Steam sign-in cache.");
        var encrypted = ProtectedData.Protect(cacheBytes, Entropy, DataProtectionScope.CurrentUser);
        var data = JsonSerializer.Serialize(new ProtectedSession(1, username, cachePath, Convert.ToBase64String(encrypted)));
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var partial = FilePath + ".partial";
        File.WriteAllText(partial, data);
        File.Move(partial, FilePath, true);
    }

    public static void Clear()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }

    private static bool IsAllowedCachePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.GetFileName(path).Equals("account.config", StringComparison.OrdinalIgnoreCase)) return false;
        var full = Path.GetFullPath(path);
        foreach (var special in new[] { Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ApplicationData })
        {
            var root = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(special), "IsolatedStorage")) + Path.DirectorySeparatorChar;
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
