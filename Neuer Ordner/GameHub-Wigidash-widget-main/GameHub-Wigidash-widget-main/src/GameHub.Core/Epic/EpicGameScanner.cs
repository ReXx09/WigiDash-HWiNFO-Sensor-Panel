using GameHub.Core.Models;
using GameHub.Core.Services;
using GameHub.Core.Compatibility;

namespace GameHub.Core.Epic;

public sealed class EpicGameScanner : IGameLibraryScanner
{
    private static readonly string[] ManifestRoots =
    [
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests"),
    ];

    public string SourceName => "Epic Games";

    public async Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        List<GameEntry> games = [];

        foreach (string manifestRoot in ManifestRoots)
        {
            if (!Directory.Exists(manifestRoot))
            {
                continue;
            }

            foreach (string manifestPath in Directory.EnumerateFiles(manifestRoot, "*.item", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                EpicManifestRecord? manifest = await ParseManifestAsync(manifestPath, cancellationToken);
                if (manifest is null || !Directory.Exists(manifest.InstallLocation))
                {
                    continue;
                }

                string launchExecutablePath = ResolveLaunchExecutablePath(manifest);
                if (string.IsNullOrWhiteSpace(launchExecutablePath))
                {
                    continue;
                }

                games.Add(new GameEntry(
                    Id: $"epic:{manifest.CatalogNamespace}:{manifest.CatalogItemId}:{manifest.AppName}",
                    Name: manifest.DisplayName,
                    Platform: GamePlatform.Epic,
                    LaunchCommand: launchExecutablePath,
                    LaunchArguments: string.IsNullOrWhiteSpace(manifest.LaunchCommand) ? null : manifest.LaunchCommand,
                    InstallPath: manifest.InstallLocation,
                    IconPath: ResolveIconPath(manifest),
                    LastPlayedAt: null));
            }
        }

        return games
            .DistinctBy(game => game.Id)
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<EpicManifestRecord?> ParseManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        using FileStream stream = File.OpenRead(manifestPath);

        EpicManifestRecord? manifest = await JsonCompatibility.DeserializeAsync<EpicManifestRecord>(
            stream,
            cancellationToken);

        if (manifest is null ||
            string.IsNullOrWhiteSpace(manifest.DisplayName) ||
            string.IsNullOrWhiteSpace(manifest.InstallLocation) ||
            string.IsNullOrWhiteSpace(manifest.AppName))
        {
            return null;
        }

        return manifest;
    }

    private static string ResolveLaunchExecutablePath(EpicManifestRecord manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.LaunchExecutable))
        {
            string launchPath = Path.Combine(manifest.InstallLocation, manifest.LaunchExecutable);
            if (File.Exists(launchPath))
            {
                return launchPath;
            }
        }

        string[] executableFiles = Directory
            .EnumerateFiles(manifest.InstallLocation, "*.exe", SearchOption.TopDirectoryOnly)
            .ToArray();

        return executableFiles.FirstOrDefault() ?? string.Empty;
    }

    private static string? ResolveIconPath(EpicManifestRecord manifest)
    {
        string gamingAppEpicRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            "Microsoft.GamingApp_8wekyb3d8bbwe",
            "LocalState",
            "ThirdPartyLibraries",
            "epic");

        string candidate = Path.Combine(
            gamingAppEpicRoot,
            $"epic_{manifest.CatalogNamespace}_{manifest.CatalogItemId}_{manifest.AppName}.png");

        if (File.Exists(candidate))
        {
            return candidate;
        }

        return null;
    }

    private sealed class EpicManifestRecord
    {
        public string DisplayName { get; set; } = string.Empty;

        public string InstallLocation { get; set; } = string.Empty;

        public string LaunchExecutable { get; set; } = string.Empty;

        public string LaunchCommand { get; set; } = string.Empty;

        public string CatalogNamespace { get; set; } = string.Empty;

        public string CatalogItemId { get; set; } = string.Empty;

        public string AppName { get; set; } = string.Empty;
    }
}
