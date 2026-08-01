namespace SSC.Internal;

internal enum XPathLikePathSelectorKind
{
    Key,
    Ordinal,
}

internal sealed class XPathLikePath
{
    public XPathLikePath(string? rootName, IReadOnlyList<XPathLikePathSegment> segments)
    {
        RootName = rootName;
        Segments = segments;
    }

    public string? RootName { get; }

    public IReadOnlyList<XPathLikePathSegment> Segments { get; }
}

internal sealed class XPathLikePathSegment
{
    public XPathLikePathSegment(string memberName, XPathLikePathSelector? selector)
    {
        MemberName = memberName;
        Selector = selector;
    }

    public string MemberName { get; }

    public XPathLikePathSelector? Selector { get; }
}

internal sealed class XPathLikePathSelector
{
    private XPathLikePathSelector(XPathLikePathSelectorKind kind, string? keyText, int? ordinal)
    {
        Kind = kind;
        KeyText = keyText;
        Ordinal = ordinal;
    }

    public XPathLikePathSelectorKind Kind { get; }

    public string? KeyText { get; }

    public int? Ordinal { get; }

    public static XPathLikePathSelector Key(string keyText)
    {
        return new XPathLikePathSelector(XPathLikePathSelectorKind.Key, keyText, null);
    }

    public static XPathLikePathSelector FromOrdinal(int ordinal)
    {
        return new XPathLikePathSelector(XPathLikePathSelectorKind.Ordinal, null, ordinal);
    }
}

internal static class XPathLikePathParser
{
    public static bool TryParse(string path, out XPathLikePath? parsedPath)
    {
        return TryParse(path, rootName: null, allowsEmptyKeySelector: false, out parsedPath);
    }

    public static bool TryParse(string path, string? rootName, out XPathLikePath? parsedPath)
    {
        return TryParse(path, rootName, allowsEmptyKeySelector: false, out parsedPath);
    }

    /// <summary>
    /// 既存の <c>GetDiffEntries()</c> が空文字列の比較 key に対して生成する legacy 空 selector を、空 key selector として解析します。
    /// </summary>
    /// <param name="path">解析する既存差分 path。</param>
    /// <param name="parsedPath">解析に成功した場合の構造化 path。</param>
    /// <returns>解析に成功した場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
    internal static bool TryParseLegacyEmptyKeySelectorPath(string path, out XPathLikePath? parsedPath)
    {
        return TryParse(path, rootName: null, allowsEmptyKeySelector: true, out parsedPath);
    }

    private static bool TryParse(
        string path,
        string? rootName,
        bool allowsEmptyKeySelector,
        out XPathLikePath? parsedPath)
    {
        parsedPath = null;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (!TrySplitSegments(path, out var rawSegments))
        {
            return false;
        }

        var parsedRootName = GetRootName(rawSegments, rootName);
        var segmentStartIndex = parsedRootName is null ? 0 : 1;
        var segments = new List<XPathLikePathSegment>(rawSegments.Count - segmentStartIndex);
        for (var index = segmentStartIndex; index < rawSegments.Count; index++)
        {
            if (!TryParseSegment(rawSegments[index], allowsEmptyKeySelector, out var segment))
            {
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            return false;
        }

        parsedPath = new XPathLikePath(parsedRootName, segments);
        return true;
    }

    private static string? GetRootName(IReadOnlyList<string> rawSegments, string? rootName)
    {
        if (rootName is null
            || rawSegments.Count <= 1
            || rawSegments[0].Contains('[', StringComparison.Ordinal)
            || !string.Equals(rawSegments[0], rootName, StringComparison.Ordinal))
        {
            return null;
        }

        return rootName;
    }

    private static bool TrySplitSegments(string path, out List<string> segments)
    {
        segments = [];
        var segmentStart = 0;
        var inSelector = false;
        var escaping = false;
        var selectorClosedInSegment = false;

        for (var index = 0; index < path.Length; index++)
        {
            var current = path[index];
            if (inSelector)
            {
                if (escaping)
                {
                    escaping = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (current == ']')
                {
                    inSelector = false;
                    selectorClosedInSegment = true;
                }

                continue;
            }

            if (current == '[')
            {
                if (selectorClosedInSegment)
                {
                    return false;
                }

                inSelector = true;
                continue;
            }

            if (current == ']')
            {
                return false;
            }

            if (current != '.')
            {
                continue;
            }

            if (index == segmentStart)
            {
                return false;
            }

            segments.Add(path[segmentStart..index]);
            segmentStart = index + 1;
            selectorClosedInSegment = false;
        }

        if (inSelector || escaping || segmentStart == path.Length)
        {
            return false;
        }

        segments.Add(path[segmentStart..]);
        return true;
    }

    private static bool TryParseSegment(
        string rawSegment,
        bool allowsEmptyKeySelector,
        out XPathLikePathSegment segment)
    {
        segment = null!;
        if (string.IsNullOrEmpty(rawSegment))
        {
            return false;
        }

        var selectorStart = rawSegment.IndexOf('[', StringComparison.Ordinal);
        if (selectorStart < 0)
        {
            segment = new XPathLikePathSegment(rawSegment, null);
            return true;
        }

        if (selectorStart == 0 || !rawSegment.EndsWith(']'))
        {
            return false;
        }

        var memberName = rawSegment[..selectorStart];
        var selectorText = rawSegment[(selectorStart + 1)..^1];
        if (memberName.Length == 0 || (selectorText.Length == 0 && !allowsEmptyKeySelector))
        {
            return false;
        }

        if (!TryParseSelector(selectorText, allowsEmptyKeySelector, out var selector))
        {
            return false;
        }

        segment = new XPathLikePathSegment(memberName, selector);
        return true;
    }

    private static bool TryParseSelector(
        string selectorText,
        bool allowsEmptyKeySelector,
        out XPathLikePathSelector selector)
    {
        selector = null!;
        if (selectorText.Length == 0 && allowsEmptyKeySelector)
        {
            selector = XPathLikePathSelector.Key(string.Empty);
            return true;
        }

        if (selectorText[0] == '#')
        {
            if (selectorText.Length == 1 || !selectorText[1..].All(char.IsDigit))
            {
                return false;
            }

            if (!int.TryParse(selectorText[1..], out var ordinal))
            {
                return false;
            }

            selector = XPathLikePathSelector.FromOrdinal(ordinal);
            return true;
        }

        if (!TryUnescapeKey(selectorText, out var keyText) || keyText.Length == 0)
        {
            return false;
        }

        selector = XPathLikePathSelector.Key(keyText);
        return true;
    }

    private static bool TryUnescapeKey(string selectorText, out string keyText)
    {
        var builder = new System.Text.StringBuilder(selectorText.Length);
        for (var index = 0; index < selectorText.Length; index++)
        {
            var current = selectorText[index];
            if (current != '\\')
            {
                builder.Append(current);
                continue;
            }

            if (index + 1 >= selectorText.Length)
            {
                keyText = string.Empty;
                return false;
            }

            var escaped = selectorText[++index];
            if (escaped is not (']' or '\\' or '#'))
            {
                keyText = string.Empty;
                return false;
            }

            builder.Append(escaped);
        }

        keyText = builder.ToString();
        return true;
    }
}
