using GameHub.Core.Models;
using GameHub.Core.Services;

namespace GameHub.Core.EA;

public sealed class EaAppScanner : IGameLibraryScanner
{
    private static readonly string[] DefaultLibraryRoots =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EA Games"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "EA Games"),
    ];

    private static readonly string[] IgnoredDirectoryNames =
    [
        "__Installer",
        "__Overlay",
        "__Support",
        "Installer",
        "Support",
        "Redist",
        "Redistributable",
    ];

    public string SourceName => "EA App";

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        List<GameEntry> games = [];

        foreach (string libraryRoot in GetLibraryRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(libraryRoot))
            {
                continue;
            }

            foreach (string gameDirectory in Directory.EnumerateDirectories(libraryRoot, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? executablePath = FindLaunchableExecutable(gameDirectory);
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    continue;
                }

                string gameName = Path.GetFileName(gameDirectory);
                games.Add(new GameEntry(
                    Id: $"ea:{NormalizeId(gameName)}",
                    Name: gameName,
                    Platform: GamePlatform.Ea,
                    LaunchCommand: executablePath,
                    LaunchArguments: null,
                    InstallPath: gameDirectory,
                    IconPath: null,
                    LastPlayedAt: null));
            }
        }

        return Task.FromResult<IReadOnlyList<GameEntry>>(
            games
                .DistinctBy(game => game.Id)
                .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IEnumerable<string> GetLibraryRoots()
    {
        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);

        string machineIniPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EA Desktop",
            "machine.ini");

        if (File.Exists(machineIniPath))
        {
            foreach (string line in File.ReadLines(machineIniPath))
            {
                if (!line.StartsWith("machine.downloadinplacedir=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string path = line.Substring("machine.downloadinplacedir=".Length).Trim();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    roots.Add(path.Trim().Trim('"'));
                }
            }
        }

        foreach (string root in DefaultLibraryRoots)
        {
            roots.Add(root);
        }

        return roots;
    }

    private static string? FindLaunchableExecutable(string gameDirectory)
    {
        try
        {
            return Directory
                .EnumerateFiles(gameDirectory, "*.exe", SearchOption.AllDirectories)
                .Where(path => !IsIgnoredPath(path))
                .Where(path => !IsToolExecutable(path))
                .OrderBy(path => ScoreExecutable(path, gameDirectory))
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsIgnoredPath(string path)
    {
        foreach (string segment in path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (IgnoredDirectoryNames.Any(name => string.Equals(segment, name, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsToolExecutable(string path)
    {
        string fileName = Path.GetFileName(path);
        return fileName.IndexOf("unins", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fileName.IndexOf("crash", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fileName.IndexOf("report", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fileName.IndexOf("updater", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fileName.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fileName.IndexOf("installer", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fileName.IndexOf("ea", StringComparison.OrdinalIgnoreCase) >= 0 && fileName.IndexOf("desktop", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int ScoreExecutable(string path, string gameDirectory)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        string gameName = Path.GetFileName(gameDirectory);
        int score = 0;

        if (string.Equals(Path.GetDirectoryName(path), gameDirectory, StringComparison.OrdinalIgnoreCase))
        {
            score -= 50;
        }

        if (string.Equals(fileName, gameName, StringComparison.OrdinalIgnoreCase))
        {
            score -= 40;
        }

        if (fileName.IndexOf(gameName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score -= 20;
        }

        score += path.Count(character => character == Path.DirectorySeparatorChar);
        score += fileName.Length;
        return score;
    }

    private static string NormalizeId(string value)
    {
        return string.Concat(value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-'))
            .Trim('-');
    }
}
