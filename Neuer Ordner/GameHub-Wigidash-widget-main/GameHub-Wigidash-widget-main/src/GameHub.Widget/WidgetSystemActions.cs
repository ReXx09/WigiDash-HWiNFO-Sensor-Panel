using GameHub.Core.Models;
using System.Diagnostics;
using System.Management;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace GameHub.Widget;

internal static class WidgetSystemActions
{
    public static bool TryLaunchLauncher(GamePlatform platform, out string statusMessage)
    {
        if (TryGetLauncherExecutablePath(platform, out string? targetPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true,
            });

            statusMessage = $"Launched {GetLauncherDisplayName(platform)}";
            return true;
        }

        statusMessage = $"{GetLauncherDisplayName(platform)} was not found.";
        return false;
    }

    public static bool TryGetLauncherExecutablePath(GamePlatform platform, out string? targetPath)
    {
        foreach (string candidate in GetLauncherCandidatePaths(platform))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            targetPath = candidate;
            return true;
        }

        targetPath = null;
        return false;
    }

    public static bool TryLaunchWeMod(out string statusMessage)
    {
        if (TryGetWeModExecutablePath(out string? targetPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true,
            });

            statusMessage = "Launched Wand (WeMod)";
            return true;
        }

        statusMessage = "Wand (WeMod) was not found.";
        return false;
    }

    public static bool TryGetWeModExecutablePath(out string? targetPath)
    {
        foreach (string candidate in GetWeModCandidatePaths())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            targetPath = candidate;
            return true;
        }

        targetPath = null;
        return false;
    }

    public static bool TryLaunchDiscord(out string statusMessage)
    {
        if (TryGetDiscordExecutablePath(out string? targetPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true,
            });

            statusMessage = "Launched Discord";
            return true;
        }

        statusMessage = "Discord was not found.";
        return false;
    }

    public static bool TryGetDiscordExecutablePath(out string? targetPath)
    {
        foreach (string candidate in GetDiscordCandidatePaths())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            targetPath = candidate;
            return true;
        }

        targetPath = null;
        return false;
    }

    public static bool TryLaunchDefaultBrowser(out string statusMessage)
    {
        if (TryGetDefaultBrowserExecutablePath(out string? targetPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true,
            });

            statusMessage = $"Launched {Path.GetFileNameWithoutExtension(targetPath)}";
            return true;
        }

        statusMessage = "Default browser was not found.";
        return false;
    }

    public static bool TryGetDefaultBrowserExecutablePath(out string? targetPath)
    {
        targetPath = TryGetBrowserExecutableFromProgId(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
        if (File.Exists(targetPath))
        {
            return true;
        }

        targetPath = TryGetBrowserExecutableFromProgId(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
        if (File.Exists(targetPath))
        {
            return true;
        }

        targetPath = null;
        return false;
    }

    public static bool TryToggleMicrophoneMute(out string statusMessage)
    {
        return TryToggleMicrophoneMute(out statusMessage, out _);
    }

    public static bool TryToggleMicrophoneMute(out string statusMessage, out bool isMuted)
    {
        try
        {
            isMuted = SetMicrophoneMute(toggle: true);
            statusMessage = isMuted ? "Microphone muted" : "Microphone unmuted";
            return true;
        }
        catch (Exception ex)
        {
            statusMessage = $"Mic toggle failed: {ex.Message}";
            isMuted = false;
            return false;
        }
    }

    public static bool TryGetMicrophoneMuteState(out bool isMuted)
    {
        try
        {
            isMuted = GetMicrophoneMuteState();
            return true;
        }
        catch
        {
        }

        isMuted = false;
        return false;
    }

    public static (string Cpu, string Gpu) GetTemperatureSummary()
    {
        string cpu = TryReadCpuTemperature();
        return (cpu, "--");
    }

    private static IEnumerable<string> GetWeModCandidatePaths()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        yield return Path.Combine(localAppData, "WeMod", "WeMod.exe");
        yield return Path.Combine(localAppData, "Programs", "WeMod", "WeMod.exe");
        yield return Path.Combine(programFiles, "WeMod", "WeMod.exe");
        yield return Path.Combine(programFilesX86, "WeMod", "WeMod.exe");
    }

    private static IEnumerable<string> GetDiscordCandidatePaths()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string discordRoot = Path.Combine(localAppData, "Discord");

        if (Directory.Exists(discordRoot))
        {
            foreach (string appDirectory in Directory.EnumerateDirectories(discordRoot, "app-*")
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase))
            {
                yield return Path.Combine(appDirectory, "Discord.exe");
            }

            yield return Path.Combine(discordRoot, "Update.exe");
        }

        yield return Path.Combine(programFiles, "Discord", "Discord.exe");
        yield return Path.Combine(programFilesX86, "Discord", "Discord.exe");
    }

    private static IEnumerable<string> GetLauncherCandidatePaths(GamePlatform platform)
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
                yield return Path.Combine(programFilesX86, "Battle.net", "Battle.net.exe");
                yield return Path.Combine(programFilesX86, "Battle.net", "Battle.net Launcher.exe");
                yield return Path.Combine(programFiles, "Battle.net", "Battle.net.exe");
                yield return Path.Combine(programFiles, "Battle.net", "Battle.net Launcher.exe");
                break;
            case GamePlatform.Xbox:
                yield return Path.Combine(localAppData, "Microsoft", "WindowsApps", "XboxPcApp.exe");
                yield return Path.Combine(localAppData, "Microsoft", "WindowsApps", "XboxPcAppCE.exe");
                yield return Path.Combine(localAppData, "Microsoft", "WindowsApps", "XboxPcAppAdminServer.exe");
                yield return Path.Combine(localAppData, "Microsoft", "WindowsApps", "XboxApp.exe");
                break;
            case GamePlatform.Ubisoft:
                yield return Path.Combine(programFilesX86, "Ubisoft", "Ubisoft Game Launcher", "UbisoftConnect.exe");
                yield return Path.Combine(programFilesX86, "Ubisoft", "Ubisoft Game Launcher", "upc.exe");
                yield return Path.Combine(programFiles, "Ubisoft", "Ubisoft Game Launcher", "UbisoftConnect.exe");
                yield return Path.Combine(programFiles, "Ubisoft", "Ubisoft Game Launcher", "upc.exe");
                break;
        }
    }

    private static string GetLauncherDisplayName(GamePlatform platform)
    {
        switch (platform)
        {
            case GamePlatform.Steam:
                return "Steam";
            case GamePlatform.Epic:
                return "Epic";
            case GamePlatform.Ea:
                return "EA";
            case GamePlatform.Gog:
                return "GOG";
            case GamePlatform.BattleNet:
                return "Battle.net";
            case GamePlatform.Xbox:
                return "Xbox";
            case GamePlatform.Ubisoft:
                return "Ubisoft";
            default:
                return platform.ToString();
        }
    }

    private static string? TryGetBrowserExecutableFromProgId(string userChoiceKeyPath)
    {
        try
        {
            using RegistryKey? userChoiceKey = Registry.CurrentUser.OpenSubKey(userChoiceKeyPath);
            string? progId = userChoiceKey?.GetValue("ProgId") as string;
            if (string.IsNullOrWhiteSpace(progId))
            {
                return null;
            }

            using RegistryKey? commandKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
            string? command = commandKey?.GetValue(null) as string;
            return ExtractExecutablePath(command);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        string trimmed = command.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            int closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                return trimmed.Substring(1, closingQuote - 1);
            }
        }

        int exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex > 0)
        {
            return trimmed.Substring(0, exeIndex + 4).Trim();
        }

        return null;
    }

    private static string TryReadCpuTemperature()
    {
        try
        {
            using ManagementObjectSearcher searcher = new(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject instance in searcher.Get())
            {
                object? rawValue = instance["CurrentTemperature"];
                if (rawValue is null)
                {
                    continue;
                }

                double kelvinTimesTen = Convert.ToDouble(rawValue);
                double celsius = (kelvinTimesTen / 10.0) - 273.15;
                if (celsius > 0 && celsius < 150)
                {
                    return $"{Math.Round(celsius):0}C";
                }
            }
        }
        catch
        {
        }

        return "--";
    }

    private static bool GetMicrophoneMuteState()
    {
        IAudioEndpointVolume? endpointVolume = null;
        try
        {
            endpointVolume = GetMicrophoneEndpointVolume();
            endpointVolume.GetMute(out bool muted);
            return muted;
        }
        finally
        {
            ReleaseComObject(endpointVolume);
        }
    }

    private static bool SetMicrophoneMute(bool toggle)
    {
        IAudioEndpointVolume? endpointVolume = null;
        try
        {
            endpointVolume = GetMicrophoneEndpointVolume();
            endpointVolume.GetMute(out bool muted);
            bool next = toggle ? !muted : muted;
            endpointVolume.SetMute(next, Guid.Empty);
            return next;
        }
        finally
        {
            ReleaseComObject(endpointVolume);
        }
    }

    private static IAudioEndpointVolume GetMicrophoneEndpointVolume()
    {
        IMMDeviceEnumerator? deviceEnumerator = null;
        IMMDevice? device = null;
        object? endpointObject = null;

        try
        {
            deviceEnumerator = (IMMDeviceEnumerator)Activator.CreateInstance(typeof(MMDeviceEnumeratorComObject));
            Marshal.ThrowExceptionForHR(deviceEnumerator.GetDefaultAudioEndpoint(1, 0, out device));

            Guid interfaceId = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref interfaceId, 23, IntPtr.Zero, out endpointObject));

            return (IAudioEndpointVolume)endpointObject;
        }
        finally
        {
            ReleaseComObject(device);
            ReleaseComObject(deviceEnumerator);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumeratorComObject;

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);
        int GetMute(out bool pbMute);
    }
}
