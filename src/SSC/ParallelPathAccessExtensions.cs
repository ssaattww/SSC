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

        var collector = new DiffEntryCollector(projector: null);
        collector.Collect(root);
        return collector.Entries;
    }

    /// <summary>
    /// 標準差分 entry と、指定した投影器で生成した利用側定義 path の組を返します。
    /// </summary>
    /// <typeparam name="T">比較対象 model の型。</typeparam>
    /// <param name="result">比較結果。</param>
    /// <param name="projector">標準 path segment の扱いを決定する投影器。</param>
    /// <returns>標準差分 entry と利用側定義 path の一覧。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/> または <paramref name="projector"/> が <see langword="null"/> の場合。
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// 1件の差分 entry に含まれるすべての segment が省略され、利用側定義 path が空になる場合。
    /// </exception>
    public static IReadOnlyList<ParallelDiffEntryPathProjection> GetDiffEntryPathProjections<T>(
        this CompareResult<T> result,
        IParallelDiffPathProjector projector)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(projector);

        if (result.Root is not IParallelNode root)
        {
            return Array.Empty<ParallelDiffEntryPathProjection>();
        }

        var collector = new DiffEntryCollector(projector);
        collector.Collect(root);
        return collector.Projections;
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

    private sealed class DiffEntryCollector
    {
        private readonly IParallelDiffPathProjector? _projector;
        private readonly List<DiffPathFrame> _frames = [];
        private readonly List<ParallelDiffEntry> _entries = [];
        private readonly List<ParallelDiffEntryPathProjection> _projections = [];

        public DiffEntryCollector(IParallelDiffPathProjector? projector)
        {
            _projector = projector;
        }

        public IReadOnlyList<ParallelDiffEntry> Entries => _entries;

        public IReadOnlyList<ParallelDiffEntryPathProjection> Projections => _projections;

        public void Collect(IParallelNode root)
        {
            foreach (var childSet in root.GetDirectChildren())
            {
                AddChildSetDiffEntries(childSet, root);
            }
        }

        private void AddNodeDiffEntries(IParallelNode node)
        {
            if (HasOwnPresenceMismatch(node))
            {
                AddEntry(CreateNodeEntry(node));
                return;
            }

            var childSets = node.GetDirectChildren();
            if (childSets.Count == 0)
            {
                if (node.HasDifferences())
                {
                    AddEntry(CreateNodeEntry(node));
                }

                return;
            }

            foreach (var childSet in childSets)
            {
                AddChildSetDiffEntries(childSet, node);
            }
        }

        private void AddChildSetDiffEntries(
            ParallelChildSet childSet,
            IParallelNode parentNode)
        {
            if (!childSet.HasDifferences)
            {
                return;
            }

            if (parentNode is IParallelNodeInternal internalParent
                && internalParent.TryGetMemberNode(childSet.Name, out var memberNode))
            {
                PushFrame(
                    ParallelDiffPathSegment.Member(childSet.Name),
                    parentNode,
                    memberNode,
                    childSet.Nodes);
                try
                {
                    AddNodeDiffEntries(memberNode);
                }
                finally
                {
                    PopFrame();
                }

                return;
            }

            if (childSet.Nodes.Count == 0)
            {
                if (parentNode is IParallelNodeInternal containerParent
                    && containerParent.TryGetContainerPresenceStates(childSet.Name, out var states))
                {
                    PushFrame(
                        ParallelDiffPathSegment.Member(childSet.Name),
                        parentNode,
                        node: null,
                        Array.Empty<IParallelNode>());
                    try
                    {
                        AddEntry(CreateContainerPresenceEntry(parentNode, states));
                    }
                    finally
                    {
                        PopFrame();
                    }
                }

                return;
            }

            for (var ordinal = 0; ordinal < childSet.Nodes.Count; ordinal++)
            {
                var childNode = childSet.Nodes[ordinal];
                var segment = childNode.KeyText is null
                    ? ParallelDiffPathSegment.Ordinal(childSet.Name, ordinal)
                    : ParallelDiffPathSegment.Key(childSet.Name, childNode.KeyText);

                PushFrame(segment, parentNode, childNode, childSet.Nodes);
                try
                {
                    AddNodeDiffEntries(childNode);
                }
                finally
                {
                    PopFrame();
                }
            }
        }

        private void PushFrame(
            ParallelDiffPathSegment standardSegment,
            IParallelNode parentNode,
            IParallelNode? node,
            IReadOnlyList<IParallelNode> siblings)
        {
            _frames.Add(new DiffPathFrame(
                standardSegment,
                parentNode,
                node,
                siblings));
        }

        private void PopFrame()
        {
            _frames.RemoveAt(_frames.Count - 1);
        }

        private void AddEntry(ParallelDiffEntry entry)
        {
            if (_projector is null)
            {
                _entries.Add(entry);
                return;
            }

            _projections.Add(CreateProjection(entry, _projector));
        }

        private ParallelDiffEntryPathProjection CreateProjection(
            ParallelDiffEntry entry,
            IParallelDiffPathProjector projector)
        {
            var nodeContexts = _frames
                .Select(frame => new ParallelDiffPathNodeContext(
                    frame.StandardSegment,
                    frame.ParentNode,
                    frame.Node,
                    frame.Siblings))
                .ToArray();
            var projectedSegments = new List<ParallelDiffPathSegment>(_frames.Count);
            var projectedParentSegmentCount = 0;

            for (var index = 0; index < nodeContexts.Length; index++)
            {
                IReadOnlyList<ParallelDiffPathNodeContext> ancestors = index == 0
                    ? Array.Empty<ParallelDiffPathNodeContext>()
                    : nodeContexts[..index];
                var context = new ParallelDiffPathProjectionContext(
                    ancestors,
                    nodeContexts[index]);
                var projection = projector.Project(context);

                switch (projection.Kind)
                {
                    case ParallelDiffPathSegmentProjectionKind.KeepStandard:
                        projectedSegments.Add(nodeContexts[index].StandardSegment);
                        break;
                    case ParallelDiffPathSegmentProjectionKind.Replace:
                        projectedSegments.Add(projection.Replacement
                            ?? throw new InvalidOperationException(
                                "Replace projection must contain a replacement segment."));
                        break;
                    case ParallelDiffPathSegmentProjectionKind.Omit:
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown diff path segment projection kind '{projection.Kind}'.");
                }

                if (index == nodeContexts.Length - 2)
                {
                    projectedParentSegmentCount = projectedSegments.Count;
                }
            }

            if (projectedSegments.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Projected diff path for standard path '{entry.Path}' cannot be empty.");
            }

            var projectedPath = ParallelDiffPathFormatter.Format(projectedSegments);
            var projectedParentPath = nodeContexts.Length <= 1
                || projectedParentSegmentCount == 0
                    ? null
                    : ParallelDiffPathFormatter.Format(
                        projectedSegments,
                        projectedParentSegmentCount);

            return new ParallelDiffEntryPathProjection(
                entry,
                projectedPath,
                projectedParentPath);
        }

        private ParallelDiffEntry CreateNodeEntry(IParallelNode node)
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
                Path = CreateStandardPath(),
                ParentPath = CreateStandardParentPath(),
                Kind = ParallelDiffEntryKind.Node,
                ParentNode = _frames[^1].ParentNode,
                Node = node,
                Values = values,
            };
        }

        private ParallelDiffEntry CreateContainerPresenceEntry(
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
                Path = CreateStandardPath(),
                ParentPath = CreateStandardParentPath(),
                Kind = ParallelDiffEntryKind.ContainerPresence,
                ParentNode = parentNode,
                Node = null,
                Values = values,
            };
        }

        private string CreateStandardPath()
        {
            var segments = _frames
                .Select(frame => frame.StandardSegment)
                .ToArray();
            return ParallelDiffPathFormatter.Format(segments);
        }

        private string? CreateStandardParentPath()
        {
            if (_frames.Count <= 1)
            {
                return null;
            }

            var segments = _frames
                .Select(frame => frame.StandardSegment)
                .ToArray();
            return ParallelDiffPathFormatter.Format(segments, segments.Length - 1);
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
    }

    private sealed class DiffPathFrame
    {
        public DiffPathFrame(
            ParallelDiffPathSegment standardSegment,
            IParallelNode parentNode,
            IParallelNode? node,
            IReadOnlyList<IParallelNode> siblings)
        {
            StandardSegment = standardSegment;
            ParentNode = parentNode;
            Node = node;
            Siblings = siblings;
        }

        public ParallelDiffPathSegment StandardSegment { get; }

        public IParallelNode ParentNode { get; }

        public IParallelNode? Node { get; }

        public IReadOnlyList<IParallelNode> Siblings { get; }
    }
}
