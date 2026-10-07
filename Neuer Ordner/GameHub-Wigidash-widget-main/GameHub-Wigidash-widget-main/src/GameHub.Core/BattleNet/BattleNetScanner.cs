using GameHub.Core.Models;
using GameHub.Core.Services;
using Microsoft.Win32;

namespace GameHub.Core.BattleNet;

public sealed class BattleNetScanner : IGameLibraryScanner
{
    private static readonly string[] UninstallRoots =
    [
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    public string SourceName => "Battle.net";

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        List<GameEntry> games = [];

        foreach (string uninstallRoot in UninstallRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using RegistryKey? rootKey = Registry.LocalMachine.OpenSubKey(uninstallRoot);
            if (rootKey is null)
            {
                continue;
            }

            foreach (string subKeyName in rootKey.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using RegistryKey? entryKey = rootKey.OpenSubKey(subKeyName);
                GameEntry? game = TryCreateGame(entryKey);
                if (game is not null)
                {
                    games.Add(game);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<GameEntry>>(
            games
                .DistinctBy(game => game.Id)
                .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static GameEntry? TryCreateGame(RegistryKey? entryKey)
    {
        if (entryKey is null)
        {
            return null;
        }

        string displayName = ReadString(entryKey, "DisplayName");
        if (string.IsNullOrWhiteSpace(displayName) || IsLauncherEntry(displayName))
        {
            return null;
        }

        string publisher = ReadString(entryKey, "Publisher");
        if (publisher.IndexOf("Blizzard", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null;
        }

        string installLocation = NormalizeDirectory(ReadString(entryKey, "InstallLocation"));
        string displayIcon = NormalizeExecutablePath(ReadString(entryKey, "DisplayIcon"));
        string uninstallString = NormalizeExecutablePath(ReadString(entryKey, "UninstallString"));

        ShortcutLaunchInfo? shortcutInfo = TryGetShortcutLaunchInfo(displayName);
        if (shortcutInfo is not null && File.Exists(shortcutInfo.TargetPath))
        {
            return new GameEntry(
                Id: $"battlenet:{NormalizeId(displayName)}",
                Name: displayName,
                Platform: GamePlatform.BattleNet,
                LaunchCommand: shortcutInfo.TargetPath,
                LaunchArguments: string.IsNullOrWhiteSpace(shortcutInfo.Arguments) ? null : shortcutInfo.Arguments,
                InstallPath: Directory.Exists(shortcutInfo.WorkingDirectory) ? shortcutInfo.WorkingDirectory : installLocation,
                IconPath: File.Exists(displayIcon) ? displayIcon : shortcutInfo.TargetPath,
                LastPlayedAt: null);
        }

        string? launchCommand = FindLaunchCommand(displayName, displayIcon, installLocation, uninstallString);
        if (string.IsNullOrWhiteSpace(launchCommand))
        {
            return null;
        }

        string? launchArguments = null;
        string? productUid = TryReadProductUid(installLocation);
        string? battleNetPath = FindBattleNetExecutable();
        if (!string.IsNullOrWhiteSpace(productUid) && !string.IsNullOrWhiteSpace(battleNetPath))
        {
            launchCommand = battleNetPath;
            launchArguments = $"--game={productUid} --gamepath=\"{installLocation}\"";
        }

        string? iconPath = File.Exists(displayIcon) ? displayIcon : launchCommand;
        return new GameEntry(
            Id: $"battlenet:{NormalizeId(displayName)}",
            Name: displayName,
            Platform: GamePlatform.BattleNet,
            LaunchCommand: launchCommand,
            LaunchArguments: launchArguments,
            InstallPath: Directory.Exists(installLocation) ? installLocation : Path.GetDirectoryName(launchCommand),
            IconPath: iconPath,
            LastPlayedAt: null);
    }

    private static string ReadString(RegistryKey key, string valueName)
    {
        return key.GetValue(valueName)?.ToString()?.Trim() ?? string.Empty;
    }

    private static bool IsLauncherEntry(string displayName)
    {
        return displayName.Equals("Battle.net", StringComparison.OrdinalIgnoreCase) ||
            displayName.IndexOf("Launcher", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().Trim('"').TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string NormalizeExecutablePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        int commaIndex = trimmed.IndexOf(',');
        if (commaIndex > 0)
        {
            trimmed = trimmed.Substring(0, commaIndex);
        }

        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            int closingQuoteIndex = trimmed.IndexOf('"', 1);
            if (closingQuoteIndex > 1)
            {
                trimmed = trimmed.Substring(1, closingQuoteIndex - 1);
            }
        }

        return trimmed.Trim().Trim('"');
    }

    private static string? FindLaunchCommand(string displayName, string displayIcon, string installLocation, string uninstallString)
    {
        if (Directory.Exists(installLocation))
        {
            string? launcherStub = FindLauncherStub(displayName, installLocation);
            if (!string.IsNullOrWhiteSpace(launcherStub))
            {
                return launcherStub;
            }
        }

        if (File.Exists(displayIcon) && LooksLikeGameExecutable(displayIcon))
        {
            return displayIcon;
        }

        if (Directory.Exists(installLocation))
        {
            string? candidate = Directory
                .EnumerateFiles(installLocation, "*.exe", SearchOption.AllDirectories)
                .Where(LooksLikeGameExecutable)
                .OrderBy(path => ScoreExecutable(path, installLocation))
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        if (File.Exists(uninstallString) && LooksLikeGameExecutable(uninstallString))
        {
            return uninstallString;
        }

        return null;
    }

    private static string? FindLauncherStub(string displayName, string installLocation)
    {
        string sanitizedName = displayName.Replace(":", string.Empty).Trim();
        string[] candidates =
        [
            Path.Combine(installLocation, $"{sanitizedName} Launcher.exe"),
            Path.Combine(installLocation, $"{Path.GetFileName(installLocation)} Launcher.exe"),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Directory
            .EnumerateFiles(installLocation, "*Launcher.exe", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).IndexOf("battle.net", StringComparison.OrdinalIgnoreCase) < 0)
            .OrderBy(path => Path.GetFileName(path).Length)
            .FirstOrDefault();
    }

    private static string? TryReadProductUid(string installLocation)
    {
        string configRoot = Path.Combine(installLocation, "Data", "config");
        if (!Directory.Exists(configRoot))
        {
            return null;
        }

        try
        {
            foreach (string path in Directory.EnumerateFiles(configRoot, "*", SearchOption.AllDirectories))
            {
                foreach (string line in File.ReadLines(path))
                {
                    const string prefix = "build-uid =";
                    if (!line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string uid = line.Substring(line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) + prefix.Length).Trim();
                    if (!string.IsNullOrWhiteSpace(uid))
                    {
                        return uid;
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? FindBattleNetExecutable()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string[] candidates =
        [
            Path.Combine(programFilesX86, "Battle.net", "Battle.net.exe"),
            Path.Combine(programFilesX86, "Battle.net", "Battle.net Launcher.exe"),
            Path.Combine(programFiles, "Battle.net", "Battle.net.exe"),
            Path.Combine(programFiles, "Battle.net", "Battle.net Launcher.exe"),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static ShortcutLaunchInfo? TryGetShortcutLaunchInfo(string displayName)
    {
        string startMenuRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs");

        if (!Directory.Exists(startMenuRoot))
        {
            return null;
        }

        string? shortcutPath = Directory
            .EnumerateFiles(startMenuRoot, "*.lnk", SearchOption.AllDirectories)
            .FirstOrDefault(path => string.Equals(
                Path.GetFileNameWithoutExtension(path),
                displayName,
                StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(shortcutPath))
        {
            return null;
        }

        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            object shell = Activator.CreateInstance(shellType)!;
            object shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);

            Type shortcutType = shortcut.GetType();
            string targetPath = shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null)?.ToString()?.Trim() ?? string.Empty;
            string arguments = shortcutType.InvokeMember("Arguments", System.Reflection.BindingFlags.GetProperty, null, shortcut, null)?.ToString()?.Trim() ?? string.Empty;
            string workingDirectory = shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.GetProperty, null, shortcut, null)?.ToString()?.Trim() ?? string.Empty;

            return !string.IsNullOrWhiteSpace(targetPath)
                ? new ShortcutLaunchInfo(targetPath, arguments, workingDirectory)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikeGameExecutable(string path)
    {
        string fileName = Path.GetFileName(path);
        return fileName.IndexOf("unins", StringComparison.OrdinalIgnoreCase) < 0 &&
            fileName.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) < 0 &&
            fileName.IndexOf("launcher", StringComparison.OrdinalIgnoreCase) < 0 &&
            fileName.IndexOf("crash", StringComparison.OrdinalIgnoreCase) < 0 &&
            fileName.IndexOf("report", StringComparison.OrdinalIgnoreCase) < 0 &&
            fileName.IndexOf("support", StringComparison.OrdinalIgnoreCase) < 0 &&
            fileName.IndexOf("redis", StringComparison.OrdinalIgnoreCase) < 0 &&
            fileName.IndexOf("battle.net", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static int ScoreExecutable(string path, string installLocation)
    {
        int score = 0;
        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(path);
        string installFolderName = Path.GetFileName(installLocation);

        if (string.Equals(directory, installLocation, StringComparison.OrdinalIgnoreCase))
        {
            score -= 40;
        }

        if (!string.IsNullOrWhiteSpace(installFolderName) &&
            string.Equals(fileName, installFolderName, StringComparison.OrdinalIgnoreCase))
        {
            score -= 30;
        }

        if (!string.IsNullOrWhiteSpace(installFolderName) &&
            fileName.IndexOf(installFolderName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score -= 10;
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

    private sealed class ShortcutLaunchInfo
    {
        public ShortcutLaunchInfo(string targetPath, string arguments, string workingDirectory)
        {
            TargetPath = targetPath;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
        }

        public string TargetPath { get; }

        public string Arguments { get; }

        public string WorkingDirectory { get; }
    }
}
