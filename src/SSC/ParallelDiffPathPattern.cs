using SSC.Internal;

namespace SSC;

/// <summary>
/// <see cref="ParallelDiffEntry.Path"/> と照合する XPath-like path pattern を表します。
/// </summary>
public sealed class ParallelDiffPathPattern
{
    private readonly IReadOnlyList<PatternSegment> _segments;

    private ParallelDiffPathPattern(IReadOnlyList<PatternSegment> segments)
    {
        _segments = segments;
    }

    /// <summary>
    /// 指定した文字列を差分 path pattern として解析します。
    /// </summary>
    /// <param name="pattern">解析する root-relative path pattern。<see langword="null"/> の場合は解析に失敗します。</param>
    /// <returns>解析済み pattern。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> が <see langword="null"/> の場合。</exception>
    /// <exception cref="FormatException"><paramref name="pattern"/> が pattern grammar に適合しない場合。</exception>
    public static ParallelDiffPathPattern Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if (!TryParse(pattern, out var parsedPattern) || parsedPattern is null)
        {
            throw new FormatException($"Diff path pattern '{pattern}' is invalid.");
        }

        return parsedPattern;
    }

    /// <summary>
    /// 指定した文字列を差分 path pattern として解析します。
    /// </summary>
    /// <param name="pattern">解析する root-relative path pattern。</param>
    /// <param name="parsedPattern">解析に成功した場合の pattern。</param>
    /// <returns>解析に成功した場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
    public static bool TryParse(string? pattern, out ParallelDiffPathPattern? parsedPattern)
    {
        parsedPattern = null;
        if (string.IsNullOrEmpty(pattern)
            || !TrySplitSegments(pattern, out var rawSegments))
        {
            return false;
        }

        var segments = new List<PatternSegment>(rawSegments.Count);
        foreach (var rawSegment in rawSegments)
        {
            if (!TryParsePatternSegment(rawSegment, out var segment))
            {
                return false;
            }

            segments.Add(segment);
        }

        parsedPattern = new ParallelDiffPathPattern(segments);
        return true;
    }

    /// <summary>
    /// 指定した XPath-like path がこの pattern に一致するか判定します。
    /// </summary>
    /// <param name="path">照合する <see cref="ParallelDiffEntry.Path"/>。</param>
    /// <returns>path 全体が一致する場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> が <see langword="null"/> の場合。</exception>
    public bool IsMatch(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!XPathLikePathParser.TryParse(path, out var parsedPath)
            || parsedPath is null
            || parsedPath.Segments.Count != _segments.Count)
        {
            return false;
        }

        for (var index = 0; index < _segments.Count; index++)
        {
            if (!_segments[index].IsMatch(parsedPath.Segments[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 一つの pattern segment を既存 path grammar または selector wildcard grammar として解析します。
    /// </summary>
    /// <remarks>
    /// <c>[*]</c> は任意 selector を表し、<c>[\*]</c> は <c>*</c> をエスケープして通常文字の key として扱います。
    /// </remarks>
    private static bool TryParsePatternSegment(string rawSegment, out PatternSegment segment)
    {
        segment = null!;
        var selectorStart = rawSegment.IndexOf('[', StringComparison.Ordinal);
        if (selectorStart < 0)
        {
            if (!XPathLikePathParser.TryParse(rawSegment, out var parsedPath)
                || parsedPath is null
                || parsedPath.Segments.Count != 1)
            {
                return false;
            }

            var parsedSegment = parsedPath.Segments[0];
            if (parsedSegment.Selector is not null)
            {
                return false;
            }

            segment = new PatternSegment(parsedSegment.MemberName, selector: null);
            return true;
        }

        if (selectorStart == 0 || !rawSegment.EndsWith(']'))
        {
            return false;
        }

        var selectorText = rawSegment[(selectorStart + 1)..^1];
        if (selectorText == "*")
        {
            var memberName = rawSegment[..selectorStart];
            if (memberName.Length == 0
                || memberName.Contains('[', StringComparison.Ordinal)
                || memberName.Contains(']', StringComparison.Ordinal))
            {
                return false;
            }

            segment = new PatternSegment(memberName, PatternSelector.Any);
            return true;
        }

        var exactSegmentText = selectorText == "\\*"
            ? string.Concat(rawSegment.AsSpan(0, selectorStart + 1), "*]")
            : rawSegment;
        if (!XPathLikePathParser.TryParse(exactSegmentText, out var exactPath)
            || exactPath is null
            || exactPath.Segments.Count != 1)
        {
            return false;
        }

        var exactSegment = exactPath.Segments[0];
        if (exactSegment.Selector is null)
        {
            return false;
        }

        segment = new PatternSegment(
            exactSegment.MemberName,
            PatternSelector.Exact(exactSegment.Selector));
        return true;
    }

    /// <summary>
    /// selector 内の escape を考慮して root-relative pattern を segment 文字列へ分割します。
    /// </summary>
    /// <remarks>
    /// selector が閉じた後の追加 selector や未閉じ selector を拒否し、既存 XPath-like path の境界規則を維持します。
    /// </remarks>
    private static bool TrySplitSegments(string pattern, out List<string> segments)
    {
        segments = [];
        var segmentStart = 0;
        var inSelector = false;
        var escaping = false;
        var selectorClosedInSegment = false;

        for (var index = 0; index < pattern.Length; index++)
        {
            var current = pattern[index];
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

            segments.Add(pattern[segmentStart..index]);
            segmentStart = index + 1;
            selectorClosedInSegment = false;
        }

        if (inSelector || escaping || segmentStart == pattern.Length)
        {
            return false;
        }

        segments.Add(pattern[segmentStart..]);
        return true;
    }

    private sealed class PatternSegment
    {
        public PatternSegment(string memberName, PatternSelector? selector)
        {
            MemberName = memberName;
            Selector = selector;
        }

        public string MemberName { get; }

        public PatternSelector? Selector { get; }

        /// <summary>
        /// member 名と selector の両方を比較して、候補 segment がこの pattern segment に一致するか判定します。
        /// </summary>
        public bool IsMatch(XPathLikePathSegment candidate)
        {
            if (!string.Equals(MemberName, candidate.MemberName, StringComparison.Ordinal))
            {
                return false;
            }

            if (Selector is null)
            {
                return candidate.Selector is null;
            }

            return candidate.Selector is not null && Selector.IsMatch(candidate.Selector);
        }
    }

    private sealed class PatternSelector
    {
        private PatternSelector(bool matchesAny, XPathLikePathSelector? exactSelector)
        {
            MatchesAny = matchesAny;
            ExactSelector = exactSelector;
        }

        public static PatternSelector Any { get; } = new(matchesAny: true, exactSelector: null);

        public bool MatchesAny { get; }

        public XPathLikePathSelector? ExactSelector { get; }

        /// <summary>
        /// 指定した selector を保持し、wildcard を使わない exact selector pattern を生成します。
        /// </summary>
        /// <param name="selector">生成した pattern が照合時に比較する selector。</param>
        /// <returns>指定した <paramref name="selector"/> を exact selector として保持する pattern。</returns>
        public static PatternSelector Exact(XPathLikePathSelector selector)
        {
            return new PatternSelector(matchesAny: false, selector);
        }

        /// <summary>
        /// wildcard または exact selector の規則で候補 selector が一致するか判定します。
        /// </summary>
        public bool IsMatch(XPathLikePathSelector candidate)
        {
            if (MatchesAny)
            {
                return true;
            }

            if (ExactSelector is null || ExactSelector.Kind != candidate.Kind)
            {
                return false;
            }

            return ExactSelector.Kind switch
            {
                XPathLikePathSelectorKind.Key => string.Equals(
                    ExactSelector.KeyText,
                    candidate.KeyText,
                    StringComparison.Ordinal),
                XPathLikePathSelectorKind.Ordinal => ExactSelector.Ordinal == candidate.Ordinal,
                _ => false,
            };
        }
    }
}

/// <summary>
/// <see cref="ParallelDiffEntry"/> の path pattern 判定を提供します。
/// </summary>
public static class ParallelDiffEntryPathExtensions
{
    /// <summary>
    /// 差分 entry の path が指定 pattern に一致するか判定します。
    /// </summary>
    /// <param name="entry">判定する差分 entry。</param>
    /// <param name="pattern">照合する path pattern。</param>
    /// <returns>一致する場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> または <paramref name="pattern"/> が <see langword="null"/> の場合。</exception>
    public static bool PathMatches(
        this ParallelDiffEntry entry,
        ParallelDiffPathPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(pattern);

        return pattern.IsMatch(entry.Path);
    }
}
