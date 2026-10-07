using GameHub.Core.Models;

namespace GameHub.Core.Services;

public sealed class CompositeGameLibraryScanner : IGameLibraryScanner
{
    private readonly IReadOnlyList<IGameLibraryScanner> _scanners;

    public CompositeGameLibraryScanner(params IGameLibraryScanner[] scanners)
    {
        _scanners = scanners;
    }

    public string SourceName => "Installed Libraries";

    public async Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        List<GameEntry> games = [];

        foreach (IGameLibraryScanner scanner in _scanners)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<GameEntry> results = await scanner.ScanAsync(cancellationToken);
            games.AddRange(results);
        }

        return games
            .DistinctBy(game => game.Id)
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
