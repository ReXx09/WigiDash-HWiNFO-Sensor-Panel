namespace GameHub.Core.Steam;

public sealed record SteamManifestRecord(
    string AppId,
    string Name,
    string InstallDirectory,
    string LibraryPath)
{
    public string InstallPath => Path.Combine(LibraryPath, "steamapps", "common", InstallDirectory);

    public string LaunchCommand => $"steam://rungameid/{AppId}";
}
