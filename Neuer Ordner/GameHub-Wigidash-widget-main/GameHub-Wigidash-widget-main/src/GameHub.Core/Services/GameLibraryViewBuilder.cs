using GameHub.Core.Models;

namespace GameHub.Core.Services;

public static class GameLibraryViewBuilder
{
    public static GameLibraryView Build(
        IReadOnlyList<GameEntry> detectedGames,
        GameLibraryPreferences preferences)
    {
        HashSet<string> hiddenIds = new(preferences.HiddenGameIds, StringComparer.OrdinalIgnoreCase);
        HashSet<string> favoriteIds = new(preferences.FavoriteGameIds, StringComparer.OrdinalIgnoreCase);

        List<GameEntry> allGames = detectedGames
            .Select(game => game with { IsFavorite = favoriteIds.Contains(game.Id) })
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<GameEntry> hiddenGames = allGames
            .Where(game => hiddenIds.Contains(game.Id))
            .ToList();

        List<GameEntry> visibleGames = allGames
            .Where(game => !hiddenIds.Contains(game.Id))
            .ToList();

        List<GameEntry> favoriteGames = visibleGames
            .Where(game => game.IsFavorite)
            .ToList();

        return new GameLibraryView(
            AllGames: allGames,
            VisibleGames: visibleGames,
            HiddenGames: hiddenGames,
            FavoriteGames: favoriteGames);
    }
}
