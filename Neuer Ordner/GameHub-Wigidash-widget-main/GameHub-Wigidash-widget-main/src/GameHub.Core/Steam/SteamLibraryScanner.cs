using GameHub.Core.Models;
using GameHub.Core.Services;
using GameHub.Core.Compatibility;
using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace GameHub.Core.Steam;

public sealed class SteamLibraryScanner : IGameLibraryScanner
{
    private static readonly Regex PathRegex = new("\"path\"\\s+\"(?<value>.+?)\"", RegexOptions.Compiled);
    private static readonly Regex AppIdRegex = new("\"appid\"\\s+\"(?<value>\\d+)\"", RegexOptions.Compiled);
    private static readonly Regex NameRegex = new("\"name\"\\s+\"(?<value>.+?)\"", RegexOptions.Compiled);
    private static readonly Regex InstallDirRegex = new("\"installdir\"\\s+\"(?<value>.+?)\"", RegexOptions.Compiled);
    private static readonly Regex HiddenAppRegex = new("\"(?<appId>\\d+)\"\\s*\\{\\s*\"hidden\"\\s*\"1\"", RegexOptions.Compiled | RegexOptions.Singleline);

    public string SourceName => "Steam";

    public async Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        List<GameEntry> games = [];
        HashSet<string> hiddenAppIds = GetHiddenAppIds();

        foreach (string libraryPath in GetLibraryPaths())
        {
            cancellationToken.ThrowIfCancellationRequested();

            string steamAppsPath = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamAppsPath))
            {
                continue;
            }

            foreach (string manifestPath in Directory.EnumerateFiles(steamAppsPath, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                SteamManifestRecord? manifest = await ParseManifestAsync(manifestPath, libraryPath, cancellationToken);
                if (manifest is null ||
                    hiddenAppIds.Contains(manifest.AppId) ||
                    !Directory.Exists(manifest.InstallPath) ||
                    !IsLaunchableLibraryEntry(manifest))
                {
                    continue;
                }

                games.Add(new GameEntry(
                    Id: $"steam:{manifest.AppId}",
                    Name: manifest.Name,
                    Platform: GamePlatform.Steam,
                    LaunchCommand: manifest.LaunchCommand,
                    LaunchArguments: null,
                    InstallPath: manifest.InstallPath,
                    IconPath: ResolveIconPath(manifest.AppId),
                    LastPlayedAt: null));
            }
        }

        return games
            .DistinctBy(game => game.Id)
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> GetLibraryPaths()
    {
        HashSet<string> libraryPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (string steamRoot in GetSteamRoots())
        {
            string normalizedSteamRoot = NormalizePath(steamRoot);
            if (!Directory.Exists(normalizedSteamRoot))
            {
                continue;
            }

            libraryPaths.Add(normalizedSteamRoot);

            string libraryFoldersPath = Path.Combine(normalizedSteamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersPath))
            {
                continue;
            }

            string contents = File.ReadAllText(libraryFoldersPath);
            foreach (Match match in PathRegex.Matches(contents))
            {
                string value = match.Groups["value"].Value;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                libraryPaths.Add(NormalizePath(value));
            }
        }

        return libraryPaths;
    }

    private static IEnumerable<string> GetSteamRoots()
    {
        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);

        AddIfPresent(roots, ReadRegistryValue(RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Valve\Steam", "SteamPath"));
        AddIfPresent(roots, ReadRegistryValue(RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Valve\Steam", "SteamExe"));
        AddIfPresent(roots, ReadRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"));
        AddIfPresent(roots, ReadRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath"));
        AddIfPresent(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        AddIfPresent(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));

        return roots;
    }

    private static string? ReadRegistryValue(
        RegistryHive hive,
        RegistryView view,
        string subKeyPath,
        string valueName)
    {
        try
        {
            using RegistryKey? baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? subKey = baseKey.OpenSubKey(subKeyPath);
            object? value = subKey?.GetValue(valueName);

            if (value is not string text || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(text)
                : text;
        }
        catch
        {
            return null;
        }
    }

    private static void AddIfPresent(ISet<string> paths, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        paths.Add(NormalizePath(candidate));
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(@"\\", @"\").Trim();
    }

    private static bool IsLaunchableLibraryEntry(SteamManifestRecord manifest)
    {
        return manifest.Name.IndexOf("Common Redistributables", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static async Task<SteamManifestRecord?> ParseManifestAsync(
        string manifestPath,
        string libraryPath,
        CancellationToken cancellationToken)
    {
        string contents = await FileCompatibility.ReadAllTextAsync(manifestPath, cancellationToken);

        string? appId = TryMatch(AppIdRegex, contents);
        string? name = TryMatch(NameRegex, contents);
        string? installDir = TryMatch(InstallDirRegex, contents);

        if (string.IsNullOrWhiteSpace(appId) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(installDir))
        {
            return null;
        }

        return new SteamManifestRecord(
            AppId: appId,
            Name: name,
            InstallDirectory: installDir,
            LibraryPath: libraryPath);
    }

    private static string? TryMatch(Regex regex, string input)
    {
        Match match = regex.Match(input);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups["value"].Value;
    }

    private static string? ResolveIconPath(string appId)
    {
        foreach (string steamRoot in GetSteamRoots())
        {
            string candidate = Path.Combine(
                NormalizePath(steamRoot),
                "appcache",
                "librarycache",
                appId,
                "header.jpg");

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static HashSet<string> GetHiddenAppIds()
    {
        HashSet<string> hiddenAppIds = new(StringComparer.OrdinalIgnoreCase);

        foreach (string steamRoot in GetSteamRoots())
        {
            string userdataRoot = Path.Combine(NormalizePath(steamRoot), "userdata");
            if (!Directory.Exists(userdataRoot))
            {
                continue;
            }

            IEnumerable<string> configPaths;
            try
            {
                configPaths = Directory.EnumerateFiles(userdataRoot, "sharedconfig.vdf", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (string configPath in configPaths)
            {
                try
                {
                    string contents = File.ReadAllText(configPath);
                    foreach (Match match in HiddenAppRegex.Matches(contents))
                    {
                        string appId = match.Groups["appId"].Value;
                        if (!string.IsNullOrWhiteSpace(appId))
                        {
                            hiddenAppIds.Add(appId);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        return hiddenAppIds;
    }
}
