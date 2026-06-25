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

    public static IReadOnlyList<ParallelDiffEntry> GetDiffEntries<T>(this CompareResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Root is not IParallelNode root)
        {
            return Array.Empty<ParallelDiffEntry>();
        }

        var entries = new List<ParallelDiffEntry>();
        foreach (var childSet in root.GetDirectChildren())
        {
            AddChildSetDiffEntries(entries, childSet, parentPath: string.Empty, root);
        }

        return entries;
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

    private static void AddNodeDiffEntries(
        List<ParallelDiffEntry> entries,
        IParallelNode node,
        string path,
        string? parentPath,
        IParallelNode parentNode)
    {
        if (HasOwnPresenceMismatch(node))
        {
            entries.Add(CreateNodeEntry(path, parentPath, parentNode, node));
            return;
        }

        var childSets = node.GetDirectChildren();
        if (childSets.Count == 0)
        {
            if (node.HasDifferences())
            {
                entries.Add(CreateNodeEntry(path, parentPath, parentNode, node));
            }

            return;
        }

        foreach (var childSet in childSets)
        {
            AddChildSetDiffEntries(entries, childSet, path, node);
        }
    }

    private static void AddChildSetDiffEntries(
        List<ParallelDiffEntry> entries,
        ParallelChildSet childSet,
        string parentPath,
        IParallelNode parentNode)
    {
        if (!childSet.HasDifferences)
        {
            return;
        }

        if (parentNode is IParallelNodeInternal internalParent
            && internalParent.TryGetMemberNode(childSet.Name, out var memberNode))
        {
            AddNodeDiffEntries(
                entries,
                memberNode,
                CombinePath(parentPath, childSet.Name),
                ToEntryParentPath(parentPath),
                parentNode);
            return;
        }

        if (childSet.Nodes.Count == 0)
        {
            if (parentNode is IParallelNodeInternal containerParent
                && containerParent.TryGetContainerPresenceStates(childSet.Name, out var states))
            {
                entries.Add(CreateContainerPresenceEntry(
                    CombinePath(parentPath, childSet.Name),
                    ToEntryParentPath(parentPath),
                    parentNode,
                    states));
            }

            return;
        }

        for (var ordinal = 0; ordinal < childSet.Nodes.Count; ordinal++)
        {
            var childNode = childSet.Nodes[ordinal];
            var selector = childNode.KeyText is null
                ? $"#{ordinal}"
                : EscapeKeyText(childNode.KeyText);
            AddNodeDiffEntries(
                entries,
                childNode,
                CombinePath(parentPath, $"{childSet.Name}[{selector}]"),
                ToEntryParentPath(parentPath),
                parentNode);
        }
    }

    private static ParallelDiffEntry CreateNodeEntry(
        string path,
        string? parentPath,
        IParallelNode parentNode,
        IParallelNode node)
    {
        var values = new ParallelDiffValue[node.Count];
        for (var modelIndex = 0; modelIndex < node.Count; modelIndex++)
        {
            values[modelIndex] = new ParallelDiffValue
            {
                ModelIndex = modelIndex,
                Value = node.GetValue(modelIndex),
                State = node.GetState(modelIndex),
            };
        }

        return new ParallelDiffEntry
        {
            Path = path,
            ParentPath = parentPath,
            Kind = ParallelDiffEntryKind.Node,
            ParentNode = parentNode,
            Node = node,
            Values = values,
        };
    }

    private static ParallelDiffEntry CreateContainerPresenceEntry(
        string path,
        string? parentPath,
        IParallelNode parentNode,
        IReadOnlyList<NodePresenceState> states)
    {
        var values = new ParallelDiffValue[states.Count];
        for (var modelIndex = 0; modelIndex < states.Count; modelIndex++)
        {
            values[modelIndex] = new ParallelDiffValue
            {
                ModelIndex = modelIndex,
                Value = null,
                State = states[modelIndex] == NodePresenceState.Missing
                    ? ValueState.Missing
                    : ValueState.Mismatched,
            };
        }

        return new ParallelDiffEntry
        {
            Path = path,
            ParentPath = parentPath,
            Kind = ParallelDiffEntryKind.ContainerPresence,
            ParentNode = parentNode,
            Node = null,
            Values = values,
        };
    }

    private static bool HasOwnPresenceMismatch(IParallelNode node)
    {
        if (node.Count <= 1 || node is not IParallelNodeInternal internalNode)
        {
            return false;
        }

        var first = internalNode.GetPresenceState(0);
        for (var modelIndex = 1; modelIndex < node.Count; modelIndex++)
        {
            if (internalNode.GetPresenceState(modelIndex) != first)
            {
                return true;
            }
        }

        return false;
    }

    private static string CombinePath(string parentPath, string segment)
    {
        return string.IsNullOrEmpty(parentPath)
            ? segment
            : $"{parentPath}.{segment}";
    }

    private static string? ToEntryParentPath(string parentPath)
    {
        return string.IsNullOrEmpty(parentPath) ? null : parentPath;
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
