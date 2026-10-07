using GameHub.Core.Models;
using GameHub.Core.Services;
using GameHub.Core.Compatibility;
using System.Diagnostics;
using System.Xml.Linq;

namespace GameHub.Core.Xbox;

public sealed class XboxGameScanner : IGameLibraryScanner
{
    private const string LogFileName = "gamehub-xbox-scan.log";

    private static readonly HashSet<string> ExcludedPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.GamingApp",
        "Microsoft.XboxApp",
        "Microsoft.Xbox.TCUI",
        "Microsoft.XboxGameOverlay",
        "Microsoft.XboxGameCallableUI",
        "Microsoft.XboxSpeechToTextOverlay",
        "Microsoft.XboxIdentityProvider",
        "Microsoft.XboxGamingOverlay",
        "Microsoft.Edge.GameAssist",
        "Microsoft.StorePurchaseApp",
    };

    public string SourceName => "Xbox / Game Pass";

    public async Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        List<GameEntry> games = [];
        games.AddRange(ScanGamingRootFolders(cancellationToken));

        try
        {
            IReadOnlyList<StartAppRecord> startApps = await QueryStartAppsAsync(cancellationToken);
            IReadOnlyList<AppxPackageRecord> packages = await QueryPackagesAsync(cancellationToken);

            Dictionary<string, StartAppRecord> startAppsByFamily = startApps
                .Where(record => !string.IsNullOrWhiteSpace(record.AppId))
                .GroupBy(record => record.AppId.Split('!')[0], StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (AppxPackageRecord package in packages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string installRoot = GetGameInstallRoot(package.InstallLocation);

                if (ExcludedPackages.Contains(package.Name) ||
                    string.IsNullOrWhiteSpace(package.PackageFamilyName) ||
                    string.IsNullOrWhiteSpace(installRoot) ||
                    !startAppsByFamily.TryGetValue(package.PackageFamilyName, out StartAppRecord? startApp) ||
                    !LooksLikeGamePackage(installRoot))
                {
                    continue;
                }

                games.Add(new GameEntry(
                    Id: $"xbox:{package.PackageFamilyName}",
                    Name: startApp.Name,
                    Platform: GamePlatform.Xbox,
                    LaunchCommand: "explorer.exe",
                    LaunchArguments: $"shell:AppsFolder\\{startApp.AppId}",
                    InstallPath: installRoot,
                    IconPath: ResolveLogoPath(installRoot),
                    LastPlayedAt: null));
            }
        }
        catch (OperationCanceledException)
        {
            LogMessage("AppX/start app scan canceled. Returning folder-scan results only.");
        }
        catch (Exception ex)
        {
            LogMessage($"AppX/start app scan failed. Returning folder-scan results only. {ex}");
        }

        return games
            .DistinctBy(game => game.Id)
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<GameEntry> ScanGamingRootFolders(CancellationToken cancellationToken)
    {
        foreach (string xboxGamesRoot in GetXboxGamesRoots())
        {
            LogMessage($"Scanning XboxGames root: {xboxGamesRoot}");
            IEnumerable<string> gameDirectories;
            try
            {
                gameDirectories = Directory.EnumerateDirectories(xboxGamesRoot, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to enumerate root {xboxGamesRoot}: {ex.Message}");
                continue;
            }

            foreach (string gameDirectory in gameDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LogMessage($"Inspecting Xbox game directory: {gameDirectory}");
                string[] candidateConfigPaths =
                [
                    Path.Combine(gameDirectory, "MicrosoftGame.config"),
                    Path.Combine(gameDirectory, "Content", "MicrosoftGame.config"),
                ];

                foreach (string configPath in candidateConfigPaths)
                {
                    if (!File.Exists(configPath))
                    {
                        continue;
                    }

                    LogMessage($"Found MicrosoftGame.config: {configPath}");
                    GameEntry? game = TryCreateGameFromConfig(configPath);
                    if (game is not null)
                    {
                        LogMessage($"Created Xbox game entry '{game.Name}' from {configPath}");
                        yield return game;
                        break;
                    }

                    LogMessage($"Failed to create Xbox game entry from {configPath}");
                }
            }
        }
    }

    private static GameEntry? TryCreateGameFromConfig(string configPath)
    {
        try
        {
            string installRoot = Path.GetDirectoryName(configPath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(installRoot) || !Directory.Exists(installRoot))
            {
                return null;
            }

            XDocument document = XDocument.Load(configPath);
            XElement? gameElement = document.Element("Game");
            XElement? shellVisuals = gameElement?.Element("ShellVisuals");
            XElement? executable = gameElement?.Element("ExecutableList")?.Element("Executable");
            if (shellVisuals is null || executable is null)
            {
                return null;
            }

            string? name = shellVisuals.Attribute("DefaultDisplayName")?.Value;
            string? executableName = executable.Attribute("Name")?.Value;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(executableName))
            {
                return null;
            }

            string launchCommand = ResolveLaunchCommand(installRoot, executableName);
            if (string.IsNullOrWhiteSpace(launchCommand))
            {
                return null;
            }

            string? iconPath = ResolveLogoPathFromConfig(installRoot, shellVisuals);

            return new GameEntry(
                Id: $"xbox-folder:{installRoot}",
                Name: name,
                Platform: GamePlatform.Xbox,
                LaunchCommand: launchCommand,
                LaunchArguments: null,
                InstallPath: installRoot,
                IconPath: iconPath,
                LastPlayedAt: null);
        }
        catch (Exception ex)
        {
            LogMessage($"Exception parsing {configPath}: {ex}");
            return null;
        }
    }

    private static string ResolveLaunchCommand(string installRoot, string executableName)
    {
        string helperPath = Path.Combine(installRoot, "GameLaunchHelper.exe");
        if (File.Exists(helperPath))
        {
            return helperPath;
        }

        string executablePath = Path.Combine(installRoot, executableName);
        return File.Exists(executablePath) ? executablePath : string.Empty;
    }

    private static string? ResolveLogoPathFromConfig(string installRoot, XElement shellVisuals)
    {
        string[] attributes =
        [
            "Square150x150Logo",
            "Square44x44Logo",
            "StoreLogo",
        ];

        foreach (string attributeName in attributes)
        {
            string? relativePath = shellVisuals.Attribute(attributeName)?.Value;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            string? resolvedPath = ResolvePackagedAssetPath(
                installRoot,
                relativePath.Replace('\\', Path.DirectorySeparatorChar));

            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                return resolvedPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetXboxGamesRoots()
    {
        foreach (DriveInfo drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady))
        {
            string xboxGamesRoot = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
            if (Directory.Exists(xboxGamesRoot))
            {
                yield return xboxGamesRoot;
            }
        }
    }

    private static bool LooksLikeGamePackage(string installRoot)
    {
        try
        {
            if (File.Exists(Path.Combine(installRoot, "MicrosoftGame.config")) ||
                File.Exists(Path.Combine(installRoot, "appxmanifest.xml")) ||
                File.Exists(Path.Combine(installRoot, "AppxManifest.xml")))
            {
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static string GetGameInstallRoot(string installLocation)
    {
        if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
        {
            return string.Empty;
        }

        if (LooksLikeGamePackageRoot(installLocation))
        {
            return installLocation;
        }

        string contentPath = Path.Combine(installLocation, "Content");
        return LooksLikeGamePackageRoot(contentPath) ? contentPath : string.Empty;
    }

    private static bool LooksLikeGamePackageRoot(string path)
    {
        return Directory.Exists(path) &&
            (File.Exists(Path.Combine(path, "MicrosoftGame.config")) ||
             File.Exists(Path.Combine(path, "appxmanifest.xml")) ||
             File.Exists(Path.Combine(path, "AppxManifest.xml")));
    }

    private static string? ResolveLogoPath(string installLocation)
    {
        try
        {
            string manifestPath = Path.Combine(installLocation, "AppxManifest.xml");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            string contents = File.ReadAllText(manifestPath);
            string[] candidateHints =
            [
                "Square150x150Logo",
                "Square44x44Logo",
                "Logo",
                "SmallLogo",
            ];

            foreach (string hint in candidateHints)
            {
                int markerIndex = contents.IndexOf($"{hint}=\"", StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                {
                    continue;
                }

                int valueStart = markerIndex + hint.Length + 2;
                int valueEnd = contents.IndexOf('"', valueStart);
                if (valueEnd <= valueStart)
                {
                    continue;
                }

                string relativePath = contents.Substring(valueStart, valueEnd - valueStart).Replace('\\', Path.DirectorySeparatorChar);
                string? resolvedPath = ResolvePackagedAssetPath(installLocation, relativePath);
                if (!string.IsNullOrWhiteSpace(resolvedPath))
                {
                    return resolvedPath;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? ResolvePackagedAssetPath(string installLocation, string relativePath)
    {
        string directPath = Path.Combine(installLocation, relativePath);
        if (File.Exists(directPath))
        {
            return directPath;
        }

        string directory = Path.Combine(installLocation, Path.GetDirectoryName(relativePath) ?? string.Empty);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(relativePath);
        string extension = Path.GetExtension(relativePath);

        return Directory
            .EnumerateFiles(directory, $"{fileNameWithoutExtension}*{extension}", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path.IndexOf("targetsize-256", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
            .ThenBy(path => path.IndexOf("scale-200", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
            .FirstOrDefault();
    }

    private static async Task<IReadOnlyList<AppxPackageRecord>> QueryPackagesAsync(CancellationToken cancellationToken)
    {
        string script = "Get-AppxPackage | Select-Object Name,PackageFamilyName,InstallLocation | ConvertTo-Json -Compress";
        string output = await RunPowerShellAsync(script, cancellationToken);
        return DeserializeRecords<AppxPackageRecord>(output);
    }

    private static async Task<IReadOnlyList<StartAppRecord>> QueryStartAppsAsync(CancellationToken cancellationToken)
    {
        string script = "Get-StartApps | Select-Object Name,AppID | ConvertTo-Json -Compress";
        string output = await RunPowerShellAsync(script, cancellationToken);
        return DeserializeRecords<StartAppRecord>(output);
    }

    private static async Task<string> RunPowerShellAsync(string script, CancellationToken cancellationToken)
    {
        foreach (string commandName in GetPowerShellCommands())
        {
            try
            {
                using Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = commandName,
                        Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    },
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await ProcessCompatibility.WaitForExitAsync(process, cancellationToken);

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    return output;
                }

                if (process.ExitCode != 0 && commandName == "pwsh")
                {
                    throw new InvalidOperationException(error);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch when (IsWindowsPowerShellCommand(commandName))
            {
                // Try the next PowerShell candidate if this one is unavailable.
            }
        }

        return "[]";
    }

    private static IEnumerable<string> GetPowerShellCommands()
    {
        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string sysnativePowerShell = Path.Combine(windir, "Sysnative", "WindowsPowerShell", "v1.0", "powershell.exe");
        string system32PowerShell = Path.Combine(windir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");

        if (File.Exists(sysnativePowerShell))
        {
            yield return sysnativePowerShell;
        }

        if (File.Exists(system32PowerShell))
        {
            yield return system32PowerShell;
        }

        yield return "powershell";
        yield return "pwsh";
    }

    private static bool IsWindowsPowerShellCommand(string commandName)
    {
        return commandName.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(commandName, "powershell", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<TRecord> DeserializeRecords<TRecord>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        string trimmedJson = json.TrimStart();
        if (trimmedJson.StartsWith("[", StringComparison.Ordinal))
        {
            return JsonCompatibility.Deserialize<TRecord[]>(json) ?? [];
        }

        TRecord? singleRecord = JsonCompatibility.Deserialize<TRecord>(json);
        return singleRecord is null ? [] : [singleRecord];
    }

    private static void LogMessage(string message)
    {
        try
        {
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "G.SKILL",
                "WigiDashManager",
                "Logs");

            Directory.CreateDirectory(logDirectory);
            string logPath = Path.Combine(logDirectory, LogFileName);
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
            File.AppendAllText(logPath, line);
        }
        catch
        {
        }
    }

    private sealed class AppxPackageRecord
    {
        public string Name { get; set; } = string.Empty;

        public string PackageFamilyName { get; set; } = string.Empty;

        public string InstallLocation { get; set; } = string.Empty;
    }

    private sealed class StartAppRecord
    {
        public string Name { get; set; } = string.Empty;

        public string AppID { get; set; } = string.Empty;

        public string AppId => AppID;
    }
}
