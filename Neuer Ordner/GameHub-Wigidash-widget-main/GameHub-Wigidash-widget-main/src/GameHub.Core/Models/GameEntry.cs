namespace GameHub.Core.Models;

public sealed record GameEntry(
    string Id,
    string Name,
    GamePlatform Platform,
    string LaunchCommand,
    string? LaunchArguments,
    string? InstallPath,
    string? IconPath,
    DateTimeOffset? LastPlayedAt,
    bool IsFavorite = false);
