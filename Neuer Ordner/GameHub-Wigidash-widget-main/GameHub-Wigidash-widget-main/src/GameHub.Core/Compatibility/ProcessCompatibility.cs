namespace GameHub.Core.Compatibility;

internal static class ProcessCompatibility
{
    public static Task WaitForExitAsync(System.Diagnostics.Process process, CancellationToken cancellationToken = default)
    {
#if NET472
        return Task.Run(() => process.WaitForExit(), cancellationToken);
#else
        return process.WaitForExitAsync(cancellationToken);
#endif
    }
}
