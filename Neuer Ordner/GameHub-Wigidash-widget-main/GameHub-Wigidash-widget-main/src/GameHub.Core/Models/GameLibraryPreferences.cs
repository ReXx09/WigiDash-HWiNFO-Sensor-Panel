namespace GameHub.Core.Models;

public sealed record GameLibraryPreferences(
    IReadOnlyCollection<string> FavoriteGameIds,
    IReadOnlyCollection<string> HiddenGameIds)
{
    public static GameLibraryPreferences Empty { get; } =
        new GameLibraryPreferences(
            FavoriteGameIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            HiddenGameIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
