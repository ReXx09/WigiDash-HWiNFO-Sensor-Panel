using GameHub.Core.Models;

namespace GameHub.Core.Services;

public interface ILibrarySnapshotStore
{
    Task<GameLibrarySnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(GameLibrarySnapshot snapshot, CancellationToken cancellationToken = default);
}
