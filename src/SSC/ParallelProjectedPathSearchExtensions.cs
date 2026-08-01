namespace SSC;

/// <summary>
/// 投影済み差分 path による検索を提供します。
/// </summary>
public static class ParallelProjectedPathSearchExtensions
{
    /// <summary>
    /// 利用側定義 path が完全一致する差分 entry projection を返します。
    /// </summary>
    /// <typeparam name="T">比較対象 model の型。</typeparam>
    /// <param name="result">比較結果。</param>
    /// <param name="projector">標準 path segment の扱いを決定する投影器。</param>
    /// <param name="projectedPath">検索する利用側定義 path。</param>
    /// <returns>完全一致した projection の一覧。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/>、<paramref name="projector"/>、または <paramref name="projectedPath"/> が
    /// <see langword="null"/> の場合。
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="projectedPath"/> が空文字列の場合。</exception>
    public static IReadOnlyList<ParallelDiffEntryPathProjection> GetDiffEntryPathProjections<T>(
        this CompareResult<T> result,
        IParallelDiffPathProjector projector,
        string projectedPath)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentException.ThrowIfNullOrEmpty(projectedPath);

        return result
            .GetDiffEntryPathProjections(projector)
            .Where(projection => string.Equals(
                projection.ProjectedPath,
                projectedPath,
                StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>
    /// 利用側定義 path が指定 pattern に一致する差分 entry projection を返します。
    /// </summary>
    /// <typeparam name="T">比較対象 model の型。</typeparam>
    /// <param name="result">比較結果。</param>
    /// <param name="projector">標準 path segment の扱いを決定する投影器。</param>
    /// <param name="pattern">検索する path pattern。</param>
    /// <returns>pattern に一致した projection の一覧。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/>、<paramref name="projector"/>、または <paramref name="pattern"/> が
    /// <see langword="null"/> の場合。
    /// </exception>
    public static IReadOnlyList<ParallelDiffEntryPathProjection> GetDiffEntryPathProjections<T>(
        this CompareResult<T> result,
        IParallelDiffPathProjector projector,
        ParallelDiffPathPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(pattern);

        return result
            .GetDiffEntryPathProjections(projector)
            .Where(projection => projection.PathMatches(pattern))
            .ToArray();
    }
}
