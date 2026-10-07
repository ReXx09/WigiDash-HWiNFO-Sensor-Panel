namespace GameHub.Core.Compatibility;

internal static class FileCompatibility
{
    public static Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
#if NET472
        return Task.Run(() => File.ReadAllText(path), cancellationToken);
#else
        return File.ReadAllTextAsync(path, cancellationToken);
#endif
    }
}
