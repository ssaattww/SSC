using System.Globalization;
using System.Text;

namespace SSC.Internal;

/// <summary>
/// 標準差分 path と利用側定義 path を共通の grammar で文字列化します。
/// </summary>
internal static class ParallelDiffPathFormatter
{
    /// <summary>
    /// すべての segment を連結して path 文字列を生成します。
    /// </summary>
    /// <param name="segments">文字列化する segment。</param>
    /// <returns>生成した path 文字列。</returns>
    public static string Format(IReadOnlyList<ParallelDiffPathSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return Format(segments, segments.Count);
    }

    /// <summary>
    /// 先頭から指定件数の segment を連結して path 文字列を生成します。
    /// </summary>
    /// <param name="segments">文字列化する segment。</param>
    /// <param name="count">文字列化する先頭 segment の件数。</param>
    /// <returns>生成した path 文字列。</returns>
    public static string Format(
        IReadOnlyList<ParallelDiffPathSegment> segments,
        int count)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (count < 0 || count > segments.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < count; index++)
        {
            if (index > 0)
            {
                builder.Append('.');
            }

            AppendSegment(builder, segments[index]);
        }

        return builder.ToString();
    }

    private static void AppendSegment(
        StringBuilder builder,
        ParallelDiffPathSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        builder.Append(segment.MemberName);

        if (segment.Selector is not ParallelDiffPathSelector selector)
        {
            return;
        }

        builder.Append('[');
        switch (selector.Kind)
        {
            case ParallelDiffPathSelectorKind.Key:
                if (selector.KeyText is null)
                {
                    throw new InvalidOperationException("Key selector must contain key text.");
                }

                builder.Append(EscapeKeyText(selector.KeyText));
                break;
            case ParallelDiffPathSelectorKind.Ordinal:
                if (selector.Ordinal is not int ordinal)
                {
                    throw new InvalidOperationException("Ordinal selector must contain an ordinal.");
                }

                builder.Append('#');
                builder.Append(ordinal.ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown diff path selector kind '{selector.Kind}'.");
        }

        builder.Append(']');
    }

    private static string EscapeKeyText(string keyText)
    {
        var escaped = keyText.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);

        if (escaped.Length > 0 && escaped[0] == '#')
        {
            return $"\\{escaped}";
        }

        return escaped;
    }
}
