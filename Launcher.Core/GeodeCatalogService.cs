using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Launcher.Core;

public sealed record GeodeCatalogMod(string Id, string Name, string Version, string Hash, string Developer, int Downloads, string GameVersion, string RequiredGeodeVersion);
public sealed record GeodeCatalogPage(IReadOnlyList<GeodeCatalogMod> Mods, int Page, bool HasMore);
public sealed record GeodeInstallResult(string FilePath, long Bytes);
public sealed record GeodeRuntimeInfo(string? GameVersion, string? GeodeVersion);

/// <summary>Client for the public Geode Mod Index. No credentials or executable content are loaded into the launcher.</summary>
public sealed class GeodeCatalogService
{
    private const string BaseUrl = "https://api.geode-sdk.org/v1/mods";
    private const int PageSize = 20;
    private const int MaxPackageBytes = 64 * 1024 * 1024;
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<GeodeCatalogPage> SearchAsync(string query, int page, string? gameVersion = null, string? geodeVersion = null, CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        var url = $"{BaseUrl}?page={page}&per_page={PageSize}&platforms=win&sort=downloads";
        if (!string.IsNullOrWhiteSpace(query)) url += "&query=" + Uri.EscapeDataString(query.Trim());
        if (!string.IsNullOrWhiteSpace(gameVersion)) url += "&gd=" + Uri.EscapeDataString(gameVersion);
        if (!string.IsNullOrWhiteSpace(geodeVersion)) url += "&geode=" + Uri.EscapeDataString(geodeVersion);
        using var response = await Client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var payload = document.RootElement.GetProperty("payload");
        var data = payload.GetProperty("data");
        if (data.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid Geode catalog response.");
        var mods = new List<GeodeCatalogMod>();
        foreach (var item in data.EnumerateArray())
        {
            var id = String(item, "id");
            if (!ValidId(id)) continue;
            var version = "";
            var name = "";
            var hash = "";
            var supportedGameVersion = "";
            var requiredGeodeVersion = "";
            if (item.TryGetProperty("versions", out var versions) && versions.ValueKind == JsonValueKind.Array && versions.GetArrayLength() > 0)
            {
                version = String(versions[0], "version");
                name = String(versions[0], "name");
                hash = String(versions[0], "hash");
                requiredGeodeVersion = String(versions[0], "geode");
                if (versions[0].TryGetProperty("gd", out var gd)) supportedGameVersion = String(gd, "win");
            }
            var developer = "";
            if (item.TryGetProperty("developers", out var developers) && developers.ValueKind == JsonValueKind.Array)
                developer = string.Join(", ", developers.EnumerateArray().Select(d => String(d, "display_name") is { Length: > 0 } display ? display : String(d, "username")).Where(s => s.Length > 0));
            var downloads = item.TryGetProperty("download_count", out var count) && count.TryGetInt32(out var n) ? n : 0;
            mods.Add(new GeodeCatalogMod(id, name.Length > 0 ? name : id, version, hash, developer, downloads, supportedGameVersion, requiredGeodeVersion));
        }
        var total = payload.TryGetProperty("count", out var countElement) && countElement.TryGetInt32(out var c) ? c : -1;
        return new GeodeCatalogPage(mods, page, total >= 0 ? page * PageSize < total : data.GetArrayLength() == PageSize);
    }

    public async Task<byte[]?> GetLogoAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!ValidId(id)) return null;
        var url = $"{BaseUrl}/{Uri.EscapeDataString(id)}/logo";
        for (var redirect = 0; redirect < 5; redirect++)
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location;
                if (location == null) return null;
                var next = location.IsAbsoluteUri ? location : new Uri(new Uri(url), location);
                if (next.Scheme != Uri.UriSchemeHttps) return null;
                url = next.ToString();
                continue;
            }
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > 2_000_000) return null;
            var bytes = await ReadLimitedAsync(response.Content, 2_000_000, cancellationToken);
            if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return null;
            var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
            return width is > 0 and <= 2048 && height is > 0 and <= 2048 ? bytes : null;
        }
        return null;
    }

    public async Task<GeodeInstallResult> InstallAsync(GeodeCatalogMod mod, string modsDirectory, CancellationToken cancellationToken = default)
    {
        var id = mod.Id;
        if (!ValidId(id)) throw new ArgumentException("Invalid Geode mod ID.", nameof(id));
        if (mod.Version.Length == 0 || !Regex.IsMatch(mod.Hash, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Catalog version or SHA-256 hash is missing.");
        if (!Directory.Exists(modsDirectory)) throw new DirectoryNotFoundException(modsDirectory);
        var destination = Path.Combine(modsDirectory, id + ".geode");
        if (File.Exists(destination) || File.Exists(destination + ".disabled")) throw new IOException("This mod is already installed.");
        var url = $"{BaseUrl}/{Uri.EscapeDataString(id)}/versions/{Uri.EscapeDataString(mod.Version)}/download";
        HttpResponseMessage? response = null;
        string? temporaryFile = null;
        try
        {
            for (var redirect = 0; redirect < 6; redirect++)
            {
                response?.Dispose();
                response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    var location = response.Headers.Location ?? throw new InvalidDataException("Missing download redirect.");
                    var next = location.IsAbsoluteUri ? location : new Uri(new Uri(url), location);
                    if (next.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("Mod download requires HTTPS.");
                    url = next.ToString();
                    continue;
                }
                break;
            }
            if (response == null || !response.IsSuccessStatusCode) throw new HttpRequestException("Geode download failed.");
            var bytes = await ReadLimitedAsync(response.Content, MaxPackageBytes, cancellationToken);
            var actualHash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!actualHash.Equals(mod.Hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Geode package SHA-256 does not match the catalog.");
            using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
            {
                var manifest = archive.GetEntry("mod.json");
                if (manifest == null || manifest.Length is < 2 or > 1_000_000) throw new InvalidDataException("Missing Geode mod manifest.");
                using var reader = JsonDocument.Parse(manifest.Open());
                if (String(reader.RootElement, "id") != id) throw new InvalidDataException("Downloaded mod ID does not match the catalog.");
            }
            temporaryFile = Path.Combine(modsDirectory, $".{id}.{Guid.NewGuid():N}.download");
            await File.WriteAllBytesAsync(temporaryFile, bytes, cancellationToken);
            File.Move(temporaryFile, destination);
            temporaryFile = null;
            var written = new FileInfo(destination).Length;
            if (written != bytes.Length) throw new IOException("Downloaded Geode file was not fully saved.");
            return new GeodeInstallResult(destination, written);
        }
        finally
        {
            response?.Dispose();
            if (temporaryFile != null && File.Exists(temporaryFile)) File.Delete(temporaryFile);
        }
    }

    private static bool ValidId(string value) => value.Length is > 0 and <= 120 && Regex.IsMatch(value, "^[a-zA-Z0-9_.-]+$");
    public static bool IsCompatible(GeodeCatalogMod mod, GeodeRuntimeInfo runtime)
    {
        if (runtime.GameVersion == null || runtime.GeodeVersion == null || mod.GameVersion != runtime.GameVersion) return false;
        return Version.TryParse(runtime.GeodeVersion, out var installed) && Version.TryParse(mod.RequiredGeodeVersion, out var required) && installed >= required;
    }
    public static GeodeRuntimeInfo DetectRuntime(string? modsDirectory)
    {
        if (string.IsNullOrWhiteSpace(modsDirectory)) return new(null, null);
        try
        {
            var gameDirectory = Directory.GetParent(modsDirectory)?.Parent;
            var folderVersion = gameDirectory?.Name;
            var gameVersion = folderVersion != null && Regex.IsMatch(folderVersion, "^2\\.[0-9]+$") ? folderVersion : null;
            var logs = Path.Combine(gameDirectory?.FullName ?? "", "geode", "logs");
            if (!Directory.Exists(logs)) return new(gameVersion, null);
            var log = Directory.EnumerateFiles(logs, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (log == null) return new(gameVersion, null);
            var exe = Path.Combine(gameDirectory!.FullName, "GeometryDash.exe");
            if (File.Exists(exe) && File.GetLastWriteTimeUtc(log) < File.GetLastWriteTimeUtc(exe)) return new(gameVersion, null);
            using var reader = new StreamReader(log);
            var header = reader.ReadLine() ?? "";
            if (header.Length > 500) return new(gameVersion, null);
            var found = Regex.Match(header, "Running Geode v(?<geode>[0-9]+\\.[0-9]+\\.[0-9]+) in Geometry Dash v(?<game>2\\.[0-9]+)");
            if (!found.Success) return new(gameVersion, null);
            return new(gameVersion ?? found.Groups["game"].Value, found.Groups["geode"].Value);
        }
        catch (IOException) { return new(null, null); }
        catch (UnauthorizedAccessException) { return new(null, null); }
    }
    private static string String(JsonElement value, string property) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() ?? "" : "";
    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, int limit, CancellationToken token)
    {
        if (content.Headers.ContentLength > limit) throw new InvalidDataException("Geode file is too large.");
        await using var input = await content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + read > limit) throw new InvalidDataException("Geode file is too large.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
