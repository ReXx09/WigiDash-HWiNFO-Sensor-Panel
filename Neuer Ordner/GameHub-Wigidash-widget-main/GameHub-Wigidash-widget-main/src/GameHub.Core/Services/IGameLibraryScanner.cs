using GameHub.Core.Models;

namespace GameHub.Core.Services;

public interface IGameLibraryScanner
{
    string SourceName { get; }

    Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default);
}
