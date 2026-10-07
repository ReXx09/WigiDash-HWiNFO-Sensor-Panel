namespace GameHub.Core.Models;

public sealed record GameLibraryView(
    IReadOnlyList<GameEntry> AllGames,
    IReadOnlyList<GameEntry> VisibleGames,
    IReadOnlyList<GameEntry> HiddenGames,
    IReadOnlyList<GameEntry> FavoriteGames);
