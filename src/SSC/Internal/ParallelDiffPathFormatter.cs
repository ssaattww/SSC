using System.Globalization;
using System.Text;

namespace SSC.Internal;

internal static class ParallelDiffPathFormatter
{
    public static string Format(IReadOnlyList<ParallelDiffPathSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return Format(segments, segments.Count);
    }

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
