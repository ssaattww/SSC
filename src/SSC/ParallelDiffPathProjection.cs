namespace SSC;

/// <summary>
/// 標準 path の各 segment を利用側定義 path へ変換します。
/// </summary>
/// <remarks>
/// 標準 path は SSC が <see cref="ParallelDiffEntry.Path"/> に格納する比較 tree 上の正式な path です。
/// 利用側定義 path は表示、分類、filter のために利用側が生成する別表現であり、node lookup には使用しません。
/// </remarks>
public interface IParallelDiffPathProjector
{
    /// <summary>
    /// 現在の標準 path segment を利用側定義 path でどのように扱うか返します。
    /// </summary>
    /// <param name="context">現在位置と祖先 node の文脈情報。</param>
    /// <returns>標準 segment の維持、置換、または省略を表す結果。</returns>
    ParallelDiffPathSegmentProjection Project(
        ParallelDiffPathProjectionContext context);
}

/// <summary>
/// 差分 path segment を投影する際の現在位置と祖先情報を表します。
/// </summary>
public sealed class ParallelDiffPathProjectionContext
{
    /// <summary>
    /// 差分 entry の投影処理で祖先と現在位置の文脈を生成します。
    /// </summary>
    internal ParallelDiffPathProjectionContext(
        IReadOnlyList<ParallelDiffPathNodeContext> ancestors,
        ParallelDiffPathNodeContext current)
    {
        ArgumentNullException.ThrowIfNull(ancestors);
        ArgumentNullException.ThrowIfNull(current);
        Ancestors = Array.AsReadOnly(ancestors.ToArray());
        Current = current;
    }

    /// <summary>
    /// root 側から現在の親までの文脈情報を取得します。現在位置自身は含みません。
    /// </summary>
    public IReadOnlyList<ParallelDiffPathNodeContext> Ancestors { get; }

    /// <summary>
    /// 現在投影している path segment の文脈情報を取得します。
    /// </summary>
    public ParallelDiffPathNodeContext Current { get; }
}

/// <summary>
/// 差分 path の1階層に対応する node と標準 segment の文脈情報を表します。
/// </summary>
public sealed class ParallelDiffPathNodeContext
{
    /// <summary>
    /// 差分 entry の投影処理で現在 segment の node 文脈を生成します。
    /// </summary>
    internal ParallelDiffPathNodeContext(
        ParallelDiffPathSegment standardSegment,
        IParallelNode parentNode,
        IParallelNode? node,
        IReadOnlyList<IParallelNode> siblings)
    {
        ArgumentNullException.ThrowIfNull(standardSegment);
        ArgumentNullException.ThrowIfNull(parentNode);
        ArgumentNullException.ThrowIfNull(siblings);
        StandardSegment = standardSegment;
        ParentNode = parentNode;
        Node = node;
        Siblings = Array.AsReadOnly(siblings.ToArray());
    }

    /// <summary>
    /// SSC が現在位置に生成する標準 path segment を取得します。
    /// </summary>
    public ParallelDiffPathSegment StandardSegment { get; }

    /// <summary>
    /// <see cref="StandardSegment"/> を所有する親 node を取得します。
    /// </summary>
    public IParallelNode ParentNode { get; }

    /// <summary>
    /// segment が指す現在の node を取得します。
    /// </summary>
    /// <remarks>
    /// empty container と missing container の差分など、要素 node を持たない container presence entry では
    /// <see langword="null"/> です。
    /// </remarks>
    public IParallelNode? Node { get; }

    /// <summary>
    /// 同じ <see cref="ParallelChildSet"/> に属する sibling node 一覧を取得します。
    /// </summary>
    /// <remarks>
    /// sibling は同じ親を持つ兄弟 node を意味します。container presence entry では空です。
    /// </remarks>
    public IReadOnlyList<IParallelNode> Siblings { get; }
}

/// <summary>
/// 標準 path segment に対する投影方法を表します。
/// </summary>
public enum ParallelDiffPathSegmentProjectionKind
{
    /// <summary>
    /// 標準 segment をそのまま利用します。
    /// </summary>
    KeepStandard,

    /// <summary>
    /// 指定された別の具体 segment へ置き換えます。
    /// </summary>
    Replace,

    /// <summary>
    /// 利用側定義 path から標準 segment を省略します。
    /// </summary>
    Omit,
}

/// <summary>
/// 標準 path segment 1件に対する投影結果を表します。
/// </summary>
public readonly struct ParallelDiffPathSegmentProjection
{
    private ParallelDiffPathSegmentProjection(
        ParallelDiffPathSegmentProjectionKind kind,
        ParallelDiffPathSegment? replacement)
    {
        Kind = kind;
        Replacement = replacement;
    }

    /// <summary>
    /// 投影方法を取得します。
    /// </summary>
    public ParallelDiffPathSegmentProjectionKind Kind { get; }

    /// <summary>
    /// <see cref="Kind"/> が <see cref="ParallelDiffPathSegmentProjectionKind.Replace"/> の場合の置換先を取得します。
    /// </summary>
    public ParallelDiffPathSegment? Replacement { get; }

    /// <summary>
    /// 標準 segment をそのまま利用する結果を返します。
    /// </summary>
    /// <returns>標準 segment を維持する結果。</returns>
    public static ParallelDiffPathSegmentProjection KeepStandard()
    {
        return new ParallelDiffPathSegmentProjection(
            ParallelDiffPathSegmentProjectionKind.KeepStandard,
            replacement: null);
    }

    /// <summary>
    /// 指定した具体 segment へ置き換える結果を返します。
    /// </summary>
    /// <param name="segment">利用側定義 path で使用する segment。</param>
    /// <returns>指定 segment へ置き換える結果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="segment"/> が <see langword="null"/> の場合。</exception>
    public static ParallelDiffPathSegmentProjection Replace(
        ParallelDiffPathSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return new ParallelDiffPathSegmentProjection(
            ParallelDiffPathSegmentProjectionKind.Replace,
            segment);
    }

    /// <summary>
    /// 利用側定義 path から標準 segment を省略する結果を返します。
    /// </summary>
    /// <returns>標準 segment を省略する結果。</returns>
    public static ParallelDiffPathSegmentProjection Omit()
    {
        return new ParallelDiffPathSegmentProjection(
            ParallelDiffPathSegmentProjectionKind.Omit,
            replacement: null);
    }
}

/// <summary>
/// 標準差分 entry と、投影器によって生成された利用側定義 path の組を表します。
/// </summary>
public sealed class ParallelDiffEntryPathProjection
{
    /// <summary>
    /// 標準差分 entry と投影済み path の組を生成します。
    /// </summary>
    internal ParallelDiffEntryPathProjection(
        ParallelDiffEntry entry,
        string projectedPath,
        string? projectedParentPath)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrEmpty(projectedPath);
        Entry = entry;
        ProjectedPath = projectedPath;
        ProjectedParentPath = projectedParentPath;
    }

    /// <summary>
    /// SSC が生成した標準差分 entry を取得します。
    /// </summary>
    public ParallelDiffEntry Entry { get; }

    /// <summary>
    /// 比較した model slot 数を取得します。
    /// </summary>
    public int Count => Entry.Values.Count;

    /// <summary>
    /// 指定した model slot の値を取得します。
    /// </summary>
    /// <param name="modelIndex">参照する model slot の index。</param>
    /// <returns>指定 slot の値。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="modelIndex"/> が範囲外の場合。
    /// </exception>
    public object? this[int modelIndex] => GetValue(modelIndex);

    /// <summary>
    /// 投影器を適用して生成した利用側定義 path を取得します。
    /// </summary>
    public string ProjectedPath { get; }

    /// <summary>
    /// 標準 parent path と同じ segment 範囲へ投影器を適用した path を取得します。
    /// </summary>
    /// <remarks>
    /// root 直下の entry に加え、標準 parent path の範囲にあるすべての segment を投影器が省略した場合も
    /// <see langword="null"/> です。
    /// </remarks>
    public string? ProjectedParentPath { get; }

    /// <summary>
    /// 指定した model slot の状態を取得します。
    /// </summary>
    /// <param name="modelIndex">参照する model slot の index。</param>
    /// <returns>指定 slot の状態。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="modelIndex"/> が範囲外の場合。
    /// </exception>
    public ValueState GetState(int modelIndex)
    {
        return GetDiffValue(modelIndex).State;
    }

    private object? GetValue(int modelIndex)
    {
        return GetDiffValue(modelIndex).Value;
    }

    private ParallelDiffValue GetDiffValue(int modelIndex)
    {
        if (modelIndex < 0 || modelIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(modelIndex));
        }

        return Entry.Values[modelIndex];
    }
}

/// <summary>
/// 利用側定義 path を持つ差分 entry の path pattern 判定を提供します。
/// </summary>
public static class ParallelDiffEntryPathProjectionExtensions
{
    /// <summary>
    /// 利用側定義 path 自身、またはその祖先が指定 pattern に一致するか判定します。
    /// </summary>
    /// <param name="projection">判定する差分 entry projection。</param>
    /// <param name="pattern">照合する path pattern。</param>
    /// <returns>
    /// pattern の全 segment が <see cref="ParallelDiffEntryPathProjection.ProjectedPath"/> の先頭から一致する場合は
    /// <see langword="true"/>。
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="projection"/> または <paramref name="pattern"/> が <see langword="null"/> の場合。
    /// </exception>
    public static bool PathMatches(
        this ParallelDiffEntryPathProjection projection,
        ParallelDiffPathPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(pattern);
        return pattern.IsMatch(projection.ProjectedPath);
    }
}
