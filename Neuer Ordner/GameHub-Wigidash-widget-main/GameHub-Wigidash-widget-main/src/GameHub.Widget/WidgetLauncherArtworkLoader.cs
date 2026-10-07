using GameHub.Core.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace GameHub.Widget;

internal static class WidgetLauncherArtworkLoader
{
    private static readonly Dictionary<GamePlatform, bool> AvailabilityCache = [];
    private static readonly Dictionary<string, string?> AppxInstallLocationCache = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsAvailable(GamePlatform platform)
    {
        if (AvailabilityCache.TryGetValue(platform, out bool isAvailable))
        {
            return isAvailable;
        }

        isAvailable = GetCandidatePaths(platform).Any(File.Exists);
        AvailabilityCache[platform] = isAvailable;
        return isAvailable;
    }

    public static Image? TryLoad(GamePlatform platform, Size targetSize)
    {
        foreach (string candidatePath in GetCandidatePaths(platform))
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            try
            {
                if (IsBitmapAsset(candidatePath))
                {
                    using Image source = Image.FromFile(candidatePath);
                    return Resize(source, targetSize);
                }

                using Icon? icon = Icon.ExtractAssociatedIcon(candidatePath);
                if (icon is null)
                {
                    continue;
                }

                using Bitmap bitmap = icon.ToBitmap();
                return Resize(bitmap, targetSize);
            }
            catch
            {
            }
        }

        return null;
    }

    public static Image? TryLoad(string candidatePath, Size targetSize)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
        {
            return null;
        }

        try
        {
            if (IsBitmapAsset(candidatePath))
            {
                using Image source = Image.FromFile(candidatePath);
                return Resize(source, targetSize);
            }

            using Icon? icon = Icon.ExtractAssociatedIcon(candidatePath);
            if (icon is null)
            {
                return null;
            }

            using Bitmap bitmap = icon.ToBitmap();
            return Resize(bitmap, targetSize);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> GetCandidatePaths(GamePlatform platform)
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        switch (platform)
        {
            case GamePlatform.Steam:
                yield return Path.Combine(programFilesX86, "Steam", "steam.exe");
                yield return Path.Combine(programFiles, "Steam", "steam.exe");
                break;
            case GamePlatform.Epic:
                yield return Path.Combine(programFilesX86, "Epic Games", "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe");
                yield return Path.Combine(programFilesX86, "Epic Games", "Launcher", "Portal", "Binaries", "Win32", "EpicGamesLauncher.exe");
                yield return Path.Combine(programFiles, "Epic Games", "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe");
                break;
            case GamePlatform.Ea:
                yield return Path.Combine(programFiles, "Electronic Arts", "EA Desktop", "EA Desktop", "EADesktop.exe");
                yield return Path.Combine(programFiles, "Electronic Arts", "EA Desktop", "EA Desktop", "EALauncher.exe");
                yield return Path.Combine(programFilesX86, "Electronic Arts", "EA Desktop", "EA Desktop", "EADesktop.exe");
                yield return Path.Combine(programFilesX86, "Electronic Arts", "EA Desktop", "EA Desktop", "EALauncher.exe");
                break;
            case GamePlatform.Gog:
                yield return Path.Combine(programFilesX86, "GOG Galaxy", "GalaxyClient.exe");
                yield return Path.Combine(programFiles, "GOG Galaxy", "GalaxyClient.exe");
                break;
            case GamePlatform.BattleNet:
                yield return Path.Combine(programFilesX86, "Battle.net", "Battle.net Launcher.exe");
                yield return Path.Combine(programFilesX86, "Battle.net", "Battle.net.exe");
                yield return Path.Combine(programFiles, "Battle.net", "Battle.net Launcher.exe");
                yield return Path.Combine(programFiles, "Battle.net", "Battle.net.exe");
                break;
            case GamePlatform.Xbox:
                foreach (string assetPath in GetXboxAssetPaths())
                {
                    yield return assetPath;
                }

                yield return Path.Combine(localAppData, "Microsoft", "WindowsApps", "XboxPcApp.exe");
                yield return Path.Combine(localAppData, "Microsoft", "WindowsApps", "XboxPcAppCE.exe");
                yield return Path.Combine(localAppData, "Microsoft", "WindowsApps", "XboxPcAppAdminServer.exe");
                yield return Path.Combine(localAppData, "Microsoft", "WindowsApps", "XboxApp.exe");
                yield return Path.Combine(localAppData, "Microsoft", "WindowsApps", "Microsoft.XboxGamingOverlay_8wekyb3d8bbwe");
                break;
            case GamePlatform.Ubisoft:
                yield return Path.Combine(programFilesX86, "Ubisoft", "Ubisoft Game Launcher", "UbisoftConnect.exe");
                yield return Path.Combine(programFilesX86, "Ubisoft", "Ubisoft Game Launcher", "upc.exe");
                yield return Path.Combine(programFiles, "Ubisoft", "Ubisoft Game Launcher", "UbisoftConnect.exe");
                yield return Path.Combine(programFiles, "Ubisoft", "Ubisoft Game Launcher", "upc.exe");
                break;
        }
    }

    private static IEnumerable<string> GetXboxAssetPaths()
    {
        string? gamingAppPath = TryGetAppxInstallLocation("Microsoft.GamingApp");
        if (!string.IsNullOrWhiteSpace(gamingAppPath))
        {
            yield return Path.Combine(gamingAppPath, "Assets", "Xbox_NotificationLogo.png");
            yield return Path.Combine(gamingAppPath, "Assets", "Xbox_StoreLogo.scale-200_contrast-white.png");
            yield return Path.Combine(gamingAppPath, "Assets", "Xbox_StoreLogo.scale-200.png");
            yield return Path.Combine(gamingAppPath, "Assets", "XboxLogo_SplashScreen.png");
            yield return Path.Combine(gamingAppPath, "Assets", "Sponsor", "Xbox-Logo-Black.png");
            yield return Path.Combine(gamingAppPath, "Assets", "Square44x44Logo.scale-200.png");
            yield return Path.Combine(gamingAppPath, "Assets", "StoreLogo.png");
        }

        string? legacyXboxAppPath = TryGetAppxInstallLocation("Microsoft.XboxApp");
        if (!string.IsNullOrWhiteSpace(legacyXboxAppPath))
        {
            yield return Path.Combine(legacyXboxAppPath, "Assets", "GamesXboxHubStoreLogo.scale-100_contrast-white.png");
            yield return Path.Combine(legacyXboxAppPath, "Assets", "GamesXboxHubBadgeLogo.scale-100_contrast-white.png");
            yield return Path.Combine(legacyXboxAppPath, "Assets", "GamesXboxHubStoreLogo.scale-100.png");
        }
    }

    private static string? TryGetAppxInstallLocation(string packageName)
    {
        if (AppxInstallLocationCache.TryGetValue(packageName, out string? cachedLocation))
        {
            return cachedLocation;
        }

        foreach (string command in GetPowerShellCommands())
        {
            try
            {
                using Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"(Get-AppxPackage {packageName}).InstallLocation\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    },
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(3000);

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    AppxInstallLocationCache[packageName] = output;
                    return output;
                }
            }
            catch
            {
            }
        }

        AppxInstallLocationCache[packageName] = null;
        return null;
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
    }

    private static bool IsBitmapAsset(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static Bitmap Resize(Image image, Size targetSize)
    {
        Bitmap canvas = new(targetSize.Width, targetSize.Height);

        using Graphics graphics = Graphics.FromImage(canvas);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);
        graphics.DrawImage(image, Fit(image.Size, targetSize));

        return canvas;
    }

    private static Rectangle Fit(Size sourceSize, Size targetSize)
    {
        double ratio = Math.Min(
            targetSize.Width / (double)Math.Max(sourceSize.Width, 1),
            targetSize.Height / (double)Math.Max(sourceSize.Height, 1));

        int width = Math.Max(1, (int)Math.Round(sourceSize.Width * ratio));
        int height = Math.Max(1, (int)Math.Round(sourceSize.Height * ratio));
        int x = (targetSize.Width - width) / 2;
        int y = (targetSize.Height - height) / 2;

        return new Rectangle(x, y, width, height);
    }
}
