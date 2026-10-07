using GameHub.Core.Compatibility;
using GameHub.Core.Models;
using GameHub.Core.Services;

namespace GameHub.Core.Gog;

public sealed class GogGalaxyScanner : IGameLibraryScanner
{
    public string SourceName => "GOG Galaxy";

    public async Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        List<GameEntry> games = [];

        foreach (string libraryRoot in GetLibraryRoots())
        {
            if (!Directory.Exists(libraryRoot))
            {
                continue;
            }

            foreach (string gameDirectory in Directory.EnumerateDirectories(libraryRoot, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? infoPath = Directory
                    .EnumerateFiles(gameDirectory, "goggame-*.info", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(infoPath))
                {
                    continue;
                }

                GogGameInfo? info = await ParseInfoAsync(infoPath, cancellationToken);
                if (info is null || string.IsNullOrWhiteSpace(info.GameId) || string.IsNullOrWhiteSpace(info.Name))
                {
                    continue;
                }

                GogPlayTask? playTask = info.PlayTasks?
                    .FirstOrDefault(task => task.IsPrimary && string.Equals(task.Type, "FileTask", StringComparison.OrdinalIgnoreCase))
                    ?? info.PlayTasks?.FirstOrDefault(task => string.Equals(task.Type, "FileTask", StringComparison.OrdinalIgnoreCase));

                string? launchCommand = ResolveLaunchCommand(gameDirectory, playTask);
                if (string.IsNullOrWhiteSpace(launchCommand))
                {
                    continue;
                }

                string iconPath = Path.Combine(gameDirectory, $"goggame-{info.GameId}.ico");
                games.Add(new GameEntry(
                    Id: $"gog:{info.GameId}",
                    Name: info.Name,
                    Platform: GamePlatform.Gog,
                    LaunchCommand: launchCommand,
                    LaunchArguments: null,
                    InstallPath: gameDirectory,
                    IconPath: File.Exists(iconPath) ? iconPath : null,
                    LastPlayedAt: null));
            }
        }

        return games
            .DistinctBy(game => game.Id)
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> GetLibraryRoots()
    {
        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);

        string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "GOG.com",
            "Galaxy",
            "config.json");

        if (File.Exists(configPath))
        {
            try
            {
                GogGalaxyConfig? config = JsonCompatibility.Deserialize<GogGalaxyConfig>(File.ReadAllText(configPath));
                if (!string.IsNullOrWhiteSpace(config?.LibraryPath))
                {
                    roots.Add(config.LibraryPath);
                }
            }
            catch
            {
            }
        }

        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games"));
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "Games"));

        return roots;
    }

    private static async Task<GogGameInfo?> ParseInfoAsync(string infoPath, CancellationToken cancellationToken)
    {
        try
        {
            using FileStream stream = File.OpenRead(infoPath);
            return await JsonCompatibility.DeserializeAsync<GogGameInfo>(stream, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveLaunchCommand(string gameDirectory, GogPlayTask? playTask)
    {
        if (!string.IsNullOrWhiteSpace(playTask?.Path))
        {
            string candidate = Path.IsPathRooted(playTask.Path)
                ? playTask.Path
                : Path.Combine(gameDirectory, playTask.Path);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Directory
            .EnumerateFiles(gameDirectory, "*.exe", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).IndexOf("unins", StringComparison.OrdinalIgnoreCase) < 0)
            .OrderBy(path => path.Length)
            .FirstOrDefault();
    }

    private sealed class GogGalaxyConfig
    {
        public string LibraryPath { get; set; } = string.Empty;
    }

    private sealed class GogGameInfo
    {
        public string GameId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<GogPlayTask>? PlayTasks { get; set; }
    }

    private sealed class GogPlayTask
    {
        public bool IsPrimary { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;
    }
}
