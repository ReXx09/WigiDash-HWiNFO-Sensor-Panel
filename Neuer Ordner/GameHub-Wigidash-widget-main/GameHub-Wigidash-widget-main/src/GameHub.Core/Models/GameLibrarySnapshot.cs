namespace GameHub.Core.Models;

public sealed record GameLibrarySnapshot(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<GameEntry> AllGames,
    IReadOnlyList<GameEntry> Favorites,
    IReadOnlyList<GameEntry> Recent);
