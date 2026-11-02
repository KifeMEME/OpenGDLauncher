using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Launcher.Core;

public sealed record DepotDownloadProgress(string Message, double? Percent = null, string? FileName = null);
public sealed record SteamQrProgress(string Message, string? QrText = null);

public sealed class DepotDownloaderService
{
    public const int DepotId = 322171;
    private const string ToolUrl = "https://github.com/SteamRE/DepotDownloader/releases/download/DepotDownloader_3.4.0/DepotDownloader-windows-x64.zip";
    private const string ToolHash = "41c9e9f0df54b3ad02e67a11726756e5c73283bd7c2e1b04acfa5ae4c2ed3767";
    private static readonly IReadOnlyDictionary<string, string> Manifests = new Dictionary<string, string>
    {
        ["1.9"]="1667324974366136291", ["2.0"]="1050808584461628025", ["2.01"]="1327679890501772194",
        ["2.11"]="4631472650007457724", ["2.111"]="3295169357678211274", ["2.113"]="641118531549408775",
        ["2.2"]="3500860979376500740", ["2.201"]="6284244757349794393", ["2.202"]="2018952014081804638",
        ["2.203"]="8259882692733807559", ["2.204"]="2530486154587189554", ["2.206"]="7903404280228366327",
        ["2.207"]="7323959135167311541", ["2.2071"]="1924617755796511196", ["2.2072"]="6397429455085000806",
        ["2.2073"]="7061773846290027563", ["2.2074"]="7678373534998244044", ["2.208"]="4708369212204820145",
        ["2.2081"]="3816559102876907245", ["2.209"]="1618314541028205441", ["2.2091"]="73633010031412782"
    };
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenGDLauncher");
    public static string? ManifestFor(string version) => Manifests.GetValueOrDefault(version);
    public static string VersionDirectory(string version) => Path.Combine(Root, "Versions", version);
    private static string ReceiptPath(string version) => Path.Combine(VersionDirectory(version), "opengd-download.json");

    public static bool IsDownloaded(string version)
    {
        var manifest = ManifestFor(version);
        if (manifest == null || !GameService.IsValid(VersionDirectory(version))) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(ReceiptPath(version)));
            return doc.RootElement.GetProperty("manifest").GetString() == manifest;
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException) { return false; }
    }

    public async Task DownloadAsync(string version, IProgress<DepotDownloadProgress>? status, CancellationToken token)
    {
        var manifest = ManifestFor(version) ?? throw new InvalidOperationException("No verified Steam manifest is configured for this version.");
        var auth = SteamAuthStore.Load() ?? throw new InvalidOperationException("Sign in with Steam QR in the bottom-left Steam panel first.");
        status?.Report(new("Preparing DepotDownloader..."));
        var tool = await EnsureToolAsync(new Progress<string>(message => status?.Report(new(message))), token);
        CheckOtherInstances();
        var destination = VersionDirectory(version);
        Directory.CreateDirectory(destination);
        status?.Report(new("Connecting to Steam..."));
        var start = CreateStart(tool);
        foreach (var argument in new[] { "-app", GameService.SteamAppId.ToString(), "-depot", DepotId.ToString(), "-manifest", manifest, "-dir", destination, "-username", auth.Username, "-remember-password", "-validate", "-loginid", LoginId() }) start.ArgumentList.Add(argument);
        var accountCache = DepotAccountCacheSnapshot.Capture();
        Directory.CreateDirectory(Path.GetDirectoryName(auth.CachePath)!);
        File.WriteAllBytes(auth.CachePath, auth.CacheBytes);
        int exitCode;
        var lastOutput = "";
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("DepotDownloader did not start.");
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lastOutput = e.Data;
                var match = Regex.Match(e.Data, @"^\s*(?<percent>\d+(?:[.,]\d+)?)%\s+(?<file>.+)$");
                if (match.Success && double.TryParse(match.Groups["percent"].Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percent))
                    status?.Report(new("Downloading game files...", Math.Clamp(percent, 0, 100), Path.GetFileName(match.Groups["file"].Value)));
                else if (e.Data.StartsWith("Pre-allocating ", StringComparison.OrdinalIgnoreCase) || e.Data.StartsWith("Validating ", StringComparison.OrdinalIgnoreCase))
                    status?.Report(new("Processing game files...", null, Path.GetFileName(e.Data[(e.Data.IndexOf(' ') + 1)..])));
            };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) lastOutput = e.Data; };
            process.BeginOutputReadLine(); process.BeginErrorReadLine(); process.StandardInput.Close();
            using var registration = token.Register(() => Stop(process));
            try { await process.WaitForExitAsync(token); process.WaitForExit(); }
            finally { Stop(process); await process.WaitForExitAsync(CancellationToken.None); }
            exitCode = process.ExitCode;
            var updated = accountCache.ChangedFiles().FirstOrDefault(file => file.Path.Equals(auth.CachePath, StringComparison.OrdinalIgnoreCase));
            if (updated.Bytes != null) SteamAuthStore.Save(auth.Username, auth.CachePath, updated.Bytes);
        }
        finally
        {
            accountCache.Restore();
        }
        if (exitCode != 0) throw new InvalidOperationException($"DepotDownloader exited with code {exitCode}. {lastOutput} Sign in again if the Steam session expired.");
        if (!GameService.IsValid(destination)) throw new InvalidDataException("Download finished, but GeometryDash.exe was not found.");
        await File.WriteAllTextAsync(ReceiptPath(version), JsonSerializer.Serialize(new { app = GameService.SteamAppId, depot = DepotId, version, manifest, completedUtc = DateTimeOffset.UtcNow }), token);
        status?.Report(new("Download complete. The selected version is ready.", 100));
    }

    public async Task<string> SignInAsync(string username, IProgress<SteamQrProgress>? progress, CancellationToken token)
    {
        username = username.Trim();
        if (username.Length is < 1 or > 64 || username.Any(char.IsControl)) throw new ArgumentException("Enter your Steam account login, not the display name.");
        progress?.Report(new("Preparing Steam QR sign-in..."));
        var tool = await EnsureToolAsync(new Progress<string>(message => progress?.Report(new(message))), token);
        CheckOtherInstances();
        var destination = Path.Combine(Root, "AuthProbe");
        Directory.CreateDirectory(destination);
        var start = CreateStart(tool);
        foreach (var argument in new[] { "-app", GameService.SteamAppId.ToString(), "-depot", DepotId.ToString(), "-manifest-only", "-dir", destination, "-qr", "-remember-password", "-loginid", LoginId() }) start.ArgumentList.Add(argument);
        var accountCache = DepotAccountCacheSnapshot.Capture();
        var qrLines = new List<string>();
        var qrMode = false;
        var lastOutput = "";
        string? authenticatedName = null;
        int exitCode;
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("DepotDownloader did not start.");
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                var line = e.Data;
                lastOutput = line;
                if (line.Contains("QR code has changed", StringComparison.OrdinalIgnoreCase) || line.Contains("Use the Steam Mobile App", StringComparison.OrdinalIgnoreCase))
                { qrMode = true; qrLines.Clear(); return; }
                if (qrMode && line.Length >= 20 && line.All(c => c is ' ' or '█' or '▀' or '▄'))
                { qrLines.Add(line); progress?.Report(new("Scan the QR code in Steam Mobile and confirm sign-in.", string.Join('\n', qrLines))); return; }
                if (qrMode && qrLines.Count > 0) qrMode = false;
                var namedAccount = Regex.Match(line, @"-username\s+(?<name>\S+)\s+-remember-password", RegexOptions.IgnoreCase);
                if (namedAccount.Success) authenticatedName = namedAccount.Groups["name"].Value;
                if (line.Contains("Logging in with QR", StringComparison.OrdinalIgnoreCase)) progress?.Report(new("Waiting for Steam QR code..."));
                else if (line.Contains("Got ", StringComparison.OrdinalIgnoreCase) && line.Contains("licenses", StringComparison.OrdinalIgnoreCase)) progress?.Report(new("Steam sign-in confirmed. Checking game access..."));
            };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) lastOutput = e.Data; };
            process.BeginOutputReadLine(); process.BeginErrorReadLine(); process.StandardInput.Close();
            using var registration = token.Register(() => Stop(process));
            try { await process.WaitForExitAsync(token); process.WaitForExit(); }
            finally { Stop(process); await process.WaitForExitAsync(CancellationToken.None); }
            exitCode = process.ExitCode;
            var changed = accountCache.ChangedFiles();
            var account = changed.FirstOrDefault(file => CacheHasUsername(file.Bytes, username));
            if (exitCode != 0) throw new InvalidOperationException($"Steam sign-in or game access failed. {lastOutput}");
            if (authenticatedName != null && !authenticatedName.Equals(username, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The Steam login entered here differs from the account used for the QR code.");
            if (account.Bytes == null) throw new InvalidOperationException("Steam account name did not match the QR sign-in. Enter the Steam login of the scanned account.");
            SteamAuthStore.Save(username, account.Path, account.Bytes);
        }
        finally { accountCache.Restore(); }
        progress?.Report(new("Steam account connected."));
        return username;
    }

    private static bool CacheHasUsername(byte[]? bytes, string username)
    {
        if (bytes == null) return false;
        try
        {
            using var input = new MemoryStream(bytes);
            using var stream = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192]; int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                if (output.Length + read > 1_000_000) return false;
                output.Write(buffer, 0, read);
            }
            return Encoding.UTF8.GetString(output.ToArray()).Contains(username, StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidDataException) { return false; }
    }

    private static ProcessStartInfo CreateStart(string tool) => new(tool)
    {
        WorkingDirectory = Path.GetDirectoryName(tool)!, UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
    };
    private static string LoginId() => Random.Shared.Next(1_000_000, 2_000_000_000).ToString();
    private static void Stop(Process process) { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
    private static void CheckOtherInstances()
    {
        var others = Process.GetProcessesByName("DepotDownloader");
        try { if (others.Any(p => !p.HasExited)) throw new InvalidOperationException("Close the other DepotDownloader window first."); }
        finally { foreach (var process in others) process.Dispose(); }
    }

    public static async Task<string> EnsureToolAsync(IProgress<string>? status, CancellationToken token)
    {
        var folder = Path.Combine(Root, "DepotDownloader-3.4.0");
        var archive = Path.Combine(Root, "DepotDownloader-3.4.0.zip");
        Directory.CreateDirectory(Root);
        if (!File.Exists(archive) || !await HasExpectedHash(archive, token))
        {
            // Reuse the original launcher's cached official release only after checking its pinned hash.
            var oldCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GDLauncherSteam", "DepotDownloader-3.4.0.zip");
            if (File.Exists(oldCache) && await HasExpectedHash(oldCache, token)) File.Copy(oldCache, archive, true);
            else
            {
                status?.Report("Downloading DepotDownloader 3.4.0 from its official GitHub release...");
                var partial = archive + ".partial";
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                using var response = await client.GetAsync(ToolUrl, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                await using (var input = await response.Content.ReadAsStreamAsync(token))
                await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write))
                {
                    var buffer = new byte[64 * 1024]; long total = 0; int read;
                    while ((read = await input.ReadAsync(buffer, token)) > 0)
                    {
                        total += read; if (total > 80_000_000) throw new InvalidDataException("DepotDownloader archive is too large.");
                        await output.WriteAsync(buffer.AsMemory(0, read), token);
                    }
                    await output.FlushAsync(token);
                }
                if (!await HasExpectedHash(partial, token)) throw new InvalidDataException("DepotDownloader SHA-256 does not match the pinned official release.");
                File.Move(partial, archive, true);
            }
        }
        status?.Report("Checking DepotDownloader files...");
        Directory.CreateDirectory(folder);
        ZipFile.ExtractToDirectory(archive, folder, true);
        var exe = Path.Combine(folder, "DepotDownloader.exe");
        if (!File.Exists(exe)) throw new InvalidDataException("DepotDownloader.exe is missing from the official archive.");
        return exe;
    }

    private static async Task<bool> HasExpectedHash(string file, CancellationToken token)
    {
        await using var stream = File.OpenRead(file);
        var hash = await SHA256.HashDataAsync(stream, token);
        return Convert.ToHexString(hash).Equals(ToolHash, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class DepotAccountCacheSnapshot
{
    private readonly Dictionary<string, byte[]> before;
    private readonly string[] roots;

    private DepotAccountCacheSnapshot(string[] roots, Dictionary<string, byte[]> before)
    {
        this.roots = roots;
        this.before = before;
    }

    public static DepotAccountCacheSnapshot Capture()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IsolatedStorage"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IsolatedStorage")
        };
        return Capture(roots);
    }

    internal static DepotAccountCacheSnapshot Capture(string[] roots)
    {
        var before = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in FindAccountFiles(roots)) before[file] = File.ReadAllBytes(file);
        return new DepotAccountCacheSnapshot(roots, before);
    }

    public void Restore()
    {
        foreach (var file in FindAccountFiles(roots))
        {
            if (before.TryGetValue(file, out var original))
            {
                if (!File.ReadAllBytes(file).AsSpan().SequenceEqual(original)) File.WriteAllBytes(file, original);
            }
            else File.Delete(file);
        }
        foreach (var (file, original) in before)
            if (!File.Exists(file))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllBytes(file, original);
            }
    }

    public IReadOnlyList<(string Path, byte[] Bytes)> ChangedFiles()
    {
        var changed = new List<(string, byte[])>();
        foreach (var file in FindAccountFiles(roots))
        {
            var bytes = File.ReadAllBytes(file);
            if (!before.TryGetValue(file, out var old) || !bytes.AsSpan().SequenceEqual(old)) changed.Add((file, bytes));
        }
        return changed;
    }

    private static IEnumerable<string> FindAccountFiles(IEnumerable<string> roots)
    {
        foreach (var root in roots)
            if (Directory.Exists(root))
                foreach (var file in Directory.EnumerateFiles(root, "account.config", SearchOption.AllDirectories))
                    yield return file;
    }
}
