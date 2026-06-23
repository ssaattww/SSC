using SSC;

namespace SSC.Unit.Tests;

public sealed class XPathLikeDiffEntriesUnitTests
{
    [Fact]
    public void GetDiffEntries_WithKeylessContainerChild_GeneratesOrdinalPath()
    {
        // Intent: KeyText が null の container child は #ordinal path を生成し、生成 path で解決できる。
        var nameNode = new FakeNode(value: "left", state: ValueState.Mismatched);
        var itemNode = new FakeNode(value: new object(), state: ValueState.Mismatched);
        itemNode.SetMember("Name", nameNode);
        var root = new FakeRootNode();
        root.SetChildren("Items", [itemNode]);
        var result = new CompareResult<FakeRoot> { Root = root };

        var entry = Assert.Single(result.GetDiffEntries());

        Assert.Equal("Items[#0].Name", entry.Path);
        Assert.Same(nameNode, entry.Node);
        Assert.Same(entry.Node, result.GetNodeByPath(entry.Path));
    }

    private sealed class FakeRoot;

    private class FakeNode : IParallelNode, IParallelNodeInternal
    {
        private readonly object? _value;
        private readonly ValueState _state;
        private readonly Dictionary<string, IParallelNode> _members = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<IParallelNode>> _children = new(StringComparer.Ordinal);

        public FakeNode(object? value, ValueState state, string? keyText = null)
        {
            _value = value;
            _state = state;
            KeyText = keyText;
        }

        public int Count => 1;

        public bool AllPresent => _state != ValueState.Missing;

        public bool AnyPresent => _state != ValueState.Missing;

        public string? KeyText { get; }

        public Type ModelType => typeof(object);

        public object? GetValue(int modelIndex)
        {
            ValidateIndex(modelIndex);
            return _state == ValueState.Missing ? null : _value;
        }

        public ValueState GetState(int modelIndex)
        {
            ValidateIndex(modelIndex);
            return _state;
        }

        public bool HasDifferences() => _state == ValueState.Mismatched;

        public IReadOnlyList<ParallelChildSet> GetDirectChildren()
        {
            var sets = new List<ParallelChildSet>();
            sets.AddRange(_members.Select(pair => new ParallelChildSet(pair.Key, [pair.Value], pair.Value.HasDifferences())));
            sets.AddRange(_children.Select(pair => new ParallelChildSet(pair.Key, pair.Value, pair.Value.Any(node => node.HasDifferences()))));
            return sets;
        }

        public bool TryGetChildren(string memberName, out IReadOnlyList<IParallelNode> nodes)
        {
            return _children.TryGetValue(memberName, out nodes!);
        }

        public bool TryGetMemberNode(string memberName, out IParallelNode node)
        {
            return _members.TryGetValue(memberName, out node!);
        }

        public NodePresenceState GetPresenceState(int modelIndex)
        {
            ValidateIndex(modelIndex);
            return _state == ValueState.Missing ? NodePresenceState.Missing : NodePresenceState.PresentValue;
        }

        public void SetMember(string name, IParallelNode node)
        {
            _members[name] = node;
        }

        public void SetChildren(string name, IReadOnlyList<IParallelNode> nodes)
        {
            _children[name] = nodes;
        }

        private static void ValidateIndex(int modelIndex)
        {
            if (modelIndex == 0)
            {
                return;
            }

            throw new CompareExecutionException(
                CompareIssueCode.ModelIndexOutOfRange,
                $"modelIndex '{modelIndex}' is out of range for count '1'.");
        }
    }

    private sealed class FakeRootNode : FakeNode, Parallel<FakeRoot>
    {
        public FakeRootNode()
            : base(new FakeRoot(), ValueState.Matched)
        {
        }

        public FakeRoot? this[int modelIndex] => (FakeRoot?)GetValue(modelIndex);
    }
}
