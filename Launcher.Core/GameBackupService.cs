namespace Launcher.Core;

public sealed record GameBackupResult(string Folder, int FilesCopied, int AudioFilesSkipped);

public static class GameBackupService
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3", ".ogg", ".wav", ".flac", ".m4a", ".aac", ".wma", ".opus", ".mid", ".midi" };

    public static string LocalGameData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeometryDash");

    public static Task<GameBackupResult> CreateBackupAsync(string destinationParent, CancellationToken token = default) =>
        CreateBackupAsync(LocalGameData, destinationParent, token);

    // The source overload also lets the backup rules be checked without touching the player's saves.
    public static Task<GameBackupResult> CreateBackupAsync(string sourceDirectory, string destinationParent, CancellationToken token) =>
        Task.Run(() => CopyFiles(sourceDirectory, destinationParent, token), token);

    private static GameBackupResult CopyFiles(string sourceDirectory, string destinationParent, CancellationToken token)
    {
        var source = Path.GetFullPath(sourceDirectory);
        var destination = Path.GetFullPath(destinationParent);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("Папка локальных данных Geometry Dash не найдена: " + source);
        if (!Directory.Exists(destination)) throw new DirectoryNotFoundException("Папка для бекапа не найдена: " + destination);
        var sourcePrefix = Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar;
        if (destination.Equals(Path.TrimEndingDirectorySeparator(source), StringComparison.OrdinalIgnoreCase) ||
            destination.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Выберите папку вне локальных данных Geometry Dash, чтобы бекап не попал внутрь самого себя.");

        var files = 0;
        var skipped = 0;
        var backup = Path.Combine(destination, $"GeometryDashBackup-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}");
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        foreach (var file in Directory.EnumerateFiles(source, "*", options))
        {
            token.ThrowIfCancellationRequested();
            if (AudioExtensions.Contains(Path.GetExtension(file))) { skipped++; continue; }
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(backup, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
            files++;
        }
        if (files == 0) throw new InvalidOperationException("В локальной папке Geometry Dash нет файлов для бекапа, кроме музыки.");
        return new GameBackupResult(backup, files, skipped);
    }
}
