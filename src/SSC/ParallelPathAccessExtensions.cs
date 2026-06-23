using SSC.Internal;

namespace SSC;

public static class ParallelPathAccessExtensions
{
    public static IParallelNode? GetNodeByPath<T>(this CompareResult<T> result, string path)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(path);

        if (result.Root is not IParallelNode current)
        {
            return null;
        }

        if (!XPathLikePathParser.TryParse(path, typeof(T).Name, out var parsedPath) || parsedPath is null)
        {
            return null;
        }

        foreach (var segment in parsedPath.Segments)
        {
            if (!TryResolveSegment(current, segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    public static object? GetValueByPath<T>(this CompareResult<T> result, string path, int modelIndex)
    {
        var node = result.GetNodeByPath(path);
        return node?.GetValue(modelIndex);
    }

    public static ValueState GetStateByPath<T>(this CompareResult<T> result, string path, int modelIndex)
    {
        var node = result.GetNodeByPath(path);
        return node?.GetState(modelIndex) ?? ValueState.Missing;
    }

    private static bool TryResolveSegment(IParallelNode current, XPathLikePathSegment segment, out IParallelNode next)
    {
        if (current is not IParallelNodeInternal internalNode)
        {
            next = null!;
            return false;
        }

        if (segment.Selector is null)
        {
            return internalNode.TryGetMemberNode(segment.MemberName, out next!);
        }

        if (!internalNode.TryGetChildren(segment.MemberName, out var childNodes))
        {
            next = null!;
            return false;
        }

        return TrySelectChild(childNodes, segment.Selector, out next);
    }

    private static bool TrySelectChild(
        IReadOnlyList<IParallelNode> childNodes,
        XPathLikePathSelector selector,
        out IParallelNode next)
    {
        if (selector.Kind == XPathLikePathSelectorKind.Ordinal)
        {
            var ordinal = selector.Ordinal!.Value;
            if (ordinal < 0 || ordinal >= childNodes.Count)
            {
                next = null!;
                return false;
            }

            next = childNodes[ordinal];
            return next.KeyText is null;
        }

        next = childNodes.FirstOrDefault(child =>
            string.Equals(child.KeyText, selector.KeyText, StringComparison.Ordinal))!;
        return next is not null;
    }
}
