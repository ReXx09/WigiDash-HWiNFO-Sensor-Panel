using GameHub.Core.Models;

namespace GameHub.Core.Services;

public interface IGameLibraryPreferencesStore
{
    Task<GameLibraryPreferences> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(GameLibraryPreferences preferences, CancellationToken cancellationToken = default);
}
