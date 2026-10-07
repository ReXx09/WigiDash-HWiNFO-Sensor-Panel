namespace System.Linq;

public static class EnumerableCompatibilityExtensions
{
#if !NET6_0_OR_GREATER
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector)
    {
        return DistinctBy(source, keySelector, comparer: null);
    }

    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        IEqualityComparer<TKey>? comparer)
    {
        HashSet<TKey> seenKeys = new(comparer);

        foreach (TSource element in source)
        {
            if (seenKeys.Add(keySelector(element)))
            {
                yield return element;
            }
        }
    }
#endif
}
