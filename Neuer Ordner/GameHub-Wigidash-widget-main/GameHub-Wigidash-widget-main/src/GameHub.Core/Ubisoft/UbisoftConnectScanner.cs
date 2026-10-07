using GameHub.Core.Compatibility;
using GameHub.Core.Models;
using GameHub.Core.Services;

namespace GameHub.Core.Ubisoft;

public sealed class UbisoftConnectScanner : IGameLibraryScanner
{
    private static readonly string[] ManifestRoots =
    [
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Ubisoft",
            "Ubisoft Game Launcher",
            "cache",
            "installation"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Ubisoft",
            "Ubisoft Game Launcher",
            "cache",
            "installation"),
    ];

    public string SourceName => "Ubisoft Connect";

    public async Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        List<GameEntry> games = [];

        foreach (string manifestRoot in ManifestRoots)
        {
            if (!Directory.Exists(manifestRoot))
            {
                continue;
            }

            foreach (string manifestPath in Directory.EnumerateFiles(manifestRoot, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                UbisoftInstallRecord? manifest = await ParseManifestAsync(manifestPath, cancellationToken);
                if (manifest is null ||
                    string.IsNullOrWhiteSpace(manifest.DisplayName) ||
                    string.IsNullOrWhiteSpace(manifest.InstallPath) ||
                    !Directory.Exists(manifest.InstallPath))
                {
                    continue;
                }

                string? launchCommand = ResolveLaunchCommand(manifest);
                if (string.IsNullOrWhiteSpace(launchCommand))
                {
                    continue;
                }

                games.Add(new GameEntry(
                    Id: $"ubisoft:{manifest.GameId}",
                    Name: manifest.DisplayName,
                    Platform: GamePlatform.Ubisoft,
                    LaunchCommand: launchCommand,
                    LaunchArguments: ResolveLaunchArguments(manifest),
                    InstallPath: manifest.InstallPath,
                    IconPath: ResolveIconPath(manifest),
                    LastPlayedAt: null));
            }
        }

        return games
            .DistinctBy(game => game.Id)
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<UbisoftInstallRecord?> ParseManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        try
        {
            string contents = await FileCompatibility.ReadAllTextAsync(manifestPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(contents) || !contents.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                return null;
            }

            Dictionary<string, object>? root = JsonCompatibility.Deserialize<Dictionary<string, object>>(contents);
            if (root is null)
            {
                return null;
            }

            string gameId = ReadString(root, "gameId", "rootGameId", "id");
            string displayName = ReadString(root, "displayName", "name", "gameName");
            string installPath = ReadString(root, "installPath", "installDir", "installLocation");
            string launchExecutable = ReadString(root, "launchExecutable", "launchExe", "executablePath");
            string launchArguments = ReadString(root, "launchArguments", "arguments", "commandLine");
            string iconPath = ReadString(root, "iconPath", "thumbnailPath", "boxArtPath");

            if (string.IsNullOrWhiteSpace(gameId))
            {
                gameId = Path.GetFileNameWithoutExtension(manifestPath);
            }

            return new UbisoftInstallRecord(
                gameId,
                displayName,
                installPath,
                launchExecutable,
                launchArguments,
                iconPath);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadString(IDictionary<string, object> values, params string[] keys)
    {
        foreach (string key in keys)
        {
            foreach (KeyValuePair<string, object> pair in values)
            {
                if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) || pair.Value is null)
                {
                    continue;
                }

                string text = Convert.ToString(pair.Value) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }

    private static string? ResolveLaunchCommand(UbisoftInstallRecord manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.LaunchExecutable))
        {
            string candidate = Path.Combine(manifest.InstallPath, manifest.LaunchExecutable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.IsNullOrWhiteSpace(manifest.GameId) ? null : "explorer.exe";
    }

    private static string? ResolveLaunchArguments(UbisoftInstallRecord manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.LaunchExecutable))
        {
            return string.IsNullOrWhiteSpace(manifest.LaunchArguments) ? null : manifest.LaunchArguments;
        }

        return string.IsNullOrWhiteSpace(manifest.GameId)
            ? null
            : $"uplay://launch/{manifest.GameId}/0";
    }

    private static string? ResolveIconPath(UbisoftInstallRecord manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.IconPath))
        {
            return null;
        }

        string candidate = Path.IsPathRooted(manifest.IconPath)
            ? manifest.IconPath
            : Path.Combine(manifest.InstallPath, manifest.IconPath);

        return File.Exists(candidate) ? candidate : null;
    }

    private sealed class UbisoftInstallRecord
    {
        public UbisoftInstallRecord(
            string gameId,
            string displayName,
            string installPath,
            string launchExecutable,
            string launchArguments,
            string iconPath)
        {
            GameId = gameId;
            DisplayName = displayName;
            InstallPath = installPath;
            LaunchExecutable = launchExecutable;
            LaunchArguments = launchArguments;
            IconPath = iconPath;
        }

        public string GameId { get; }

        public string DisplayName { get; }

        public string InstallPath { get; }

        public string LaunchExecutable { get; }

        public string LaunchArguments { get; }

        public string IconPath { get; }
    }
}
