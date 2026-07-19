namespace SSC;

/// <summary>
/// 差分 path の選択子が要素を識別する方法を表します。
/// </summary>
public enum ParallelDiffPathSelectorKind
{
    /// <summary>
    /// 比較 key の文字列表現で要素を識別します。
    /// </summary>
    Key,

    /// <summary>
    /// key を持たない sequence 内の並び順で要素を識別します。
    /// </summary>
    Ordinal,
}

/// <summary>
/// 差分 path segment の選択子を表します。
/// </summary>
public readonly struct ParallelDiffPathSelector
{
    private ParallelDiffPathSelector(
        ParallelDiffPathSelectorKind kind,
        string? keyText,
        int? ordinal)
    {
        Kind = kind;
        KeyText = keyText;
        Ordinal = ordinal;
    }

    /// <summary>
    /// 要素を識別する方法を取得します。
    /// </summary>
    public ParallelDiffPathSelectorKind Kind { get; }

    /// <summary>
    /// <see cref="Kind"/> が <see cref="ParallelDiffPathSelectorKind.Key"/> の場合の比較 key 文字列を取得します。
    /// </summary>
    public string? KeyText { get; }

    /// <summary>
    /// <see cref="Kind"/> が <see cref="ParallelDiffPathSelectorKind.Ordinal"/> の場合の並び順を取得します。
    /// </summary>
    public int? Ordinal { get; }

    /// <summary>
    /// 内部で生成した比較 key 文字列から key 選択子を生成します。
    /// </summary>
    internal static ParallelDiffPathSelector FromKey(string keyText)
    {
        return new ParallelDiffPathSelector(ParallelDiffPathSelectorKind.Key, keyText, null);
    }

    /// <summary>
    /// 内部で生成した並び順から ordinal 選択子を生成します。
    /// </summary>
    internal static ParallelDiffPathSelector FromOrdinal(int ordinal)
    {
        return new ParallelDiffPathSelector(ParallelDiffPathSelectorKind.Ordinal, null, ordinal);
    }
}

/// <summary>
/// SSC の差分 path を構成する具体的な segment 1件を表します。
/// </summary>
/// <remarks>
/// segment は <c>Root</c>、<c>Items[A]</c>、<c>Children[#0]</c> のような、dot で区切られる要素です。
/// この型は特定位置を表すため、pattern 用の wildcard は保持しません。
/// </remarks>
public sealed class ParallelDiffPathSegment
{
    private ParallelDiffPathSegment(
        string memberName,
        ParallelDiffPathSelector? selector)
    {
        ValidateMemberName(memberName);
        MemberName = memberName;
        Selector = selector;
    }

    /// <summary>
    /// member 名を取得します。
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// container 要素を識別する選択子を取得します。通常の member の場合は <see langword="null"/> です。
    /// </summary>
    public ParallelDiffPathSelector? Selector { get; }

    /// <summary>
    /// 選択子を持たない member segment を生成します。
    /// </summary>
    /// <param name="memberName">member 名。</param>
    /// <returns>生成した segment。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="memberName"/> が空、または path grammar で表現できない文字を含む場合。
    /// </exception>
    public static ParallelDiffPathSegment Member(string memberName)
    {
        return new ParallelDiffPathSegment(memberName, selector: null);
    }

    /// <summary>
    /// 比較 key で container 要素を識別する segment を生成します。
    /// </summary>
    /// <param name="memberName">container member 名。</param>
    /// <param name="keyText">比較 key の文字列表現。</param>
    /// <returns>生成した segment。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="memberName"/> または <paramref name="keyText"/> が空、
    /// もしくは <paramref name="memberName"/> が path grammar で表現できない文字を含む場合。
    /// </exception>
    public static ParallelDiffPathSegment Key(string memberName, string keyText)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyText);
        return new ParallelDiffPathSegment(memberName, ParallelDiffPathSelector.FromKey(keyText));
    }

    /// <summary>
    /// 標準差分 path の生成用に、空文字列を含む比較 key で container 要素を識別する segment を生成します。
    /// </summary>
    /// <param name="memberName">container member 名。</param>
    /// <param name="keyText">比較処理が生成した比較 key の文字列表現。</param>
    /// <returns>標準差分 path 用の segment。</returns>
    /// <remarks>
    /// この内部経路は既存比較結果の標準 path 互換性を維持するためだけに使用します。
    /// 公開 <see cref="Key(string, string)"/> の空文字列拒否契約は変更しません。
    /// </remarks>
    internal static ParallelDiffPathSegment StandardKey(string memberName, string keyText)
    {
        ArgumentNullException.ThrowIfNull(keyText);
        return new ParallelDiffPathSegment(memberName, ParallelDiffPathSelector.FromKey(keyText));
    }

    /// <summary>
    /// key を持たない sequence 内の並び順で要素を識別する segment を生成します。
    /// </summary>
    /// <param name="memberName">container member 名。</param>
    /// <param name="ordinal">0から始まる並び順。</param>
    /// <returns>生成した segment。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="memberName"/> が空、または path grammar で表現できない文字を含む場合。
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ordinal"/> が負数の場合。</exception>
    public static ParallelDiffPathSegment Ordinal(string memberName, int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        return new ParallelDiffPathSegment(memberName, ParallelDiffPathSelector.FromOrdinal(ordinal));
    }

    /// <summary>
    /// 現在の選択子を維持し、member 名だけを変更した segment を生成します。
    /// </summary>
    /// <param name="memberName">新しい member 名。</param>
    /// <returns>member 名を変更した segment。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="memberName"/> が空、または path grammar で表現できない文字を含む場合。
    /// </exception>
    public ParallelDiffPathSegment WithMemberName(string memberName)
    {
        return new ParallelDiffPathSegment(memberName, Selector);
    }

    private static void ValidateMemberName(string memberName)
    {
        ArgumentException.ThrowIfNullOrEmpty(memberName);

        if (memberName.Contains('.')
            || memberName.Contains('[')
            || memberName.Contains(']'))
        {
            throw new ArgumentException(
                "Diff path member name cannot contain '.', '[' or ']'.",
                nameof(memberName));
        }
    }
}
