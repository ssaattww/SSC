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

    [Fact]
    public void GetDiffEntries_WithContainerPresenceMissingState_DistinguishesMissingFromNullValue()
    {
        // Intent: ContainerPresence では Value は null 固定でも、State で Missing と present 側を区別する。
        var root = new FakeRootNode();
        root.SetChildren("Items", [], [NodePresenceState.PresentValue, NodePresenceState.Missing]);
        var result = new CompareResult<FakeRoot> { Root = root };

        var entry = Assert.Single(result.GetDiffEntries());

        Assert.Equal("Items", entry.Path);
        Assert.Equal(ParallelDiffEntryKind.ContainerPresence, entry.Kind);
        Assert.Null(entry.Node);
        Assert.Null(result.GetNodeByPath(entry.Path));
        Assert.Equal(ValueState.Mismatched, entry.Values[0].State);
        Assert.Null(entry.Values[0].Value);
        Assert.Equal(ValueState.Missing, entry.Values[1].State);
        Assert.Null(entry.Values[1].Value);
        Assert.Equal("Items: [0]=null(Mismatched), [1]=<missing>(Missing)", entry.ToString());
    }

    private sealed class FakeRoot;

    private class FakeNode : IParallelNode, IParallelNodeInternal
    {
        private readonly object? _value;
        private readonly ValueState _state;
        private readonly Dictionary<string, IParallelNode> _members = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<IParallelNode>> _children = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<NodePresenceState>> _containerPresenceStates = new(StringComparer.Ordinal);

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
            sets.AddRange(_children.Select(pair => new ParallelChildSet(
                pair.Key,
                pair.Value,
                HasPresenceMismatch(_containerPresenceStates[pair.Key]) || pair.Value.Any(node => node.HasDifferences()))));
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

        public bool TryGetContainerPresenceStates(string memberName, out IReadOnlyList<NodePresenceState> states)
        {
            if (_containerPresenceStates.TryGetValue(memberName, out var containerStates))
            {
                states = containerStates;
                return true;
            }

            states = Array.Empty<NodePresenceState>();
            return false;
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
            SetChildren(name, nodes, [NodePresenceState.PresentValue]);
        }

        public void SetChildren(string name, IReadOnlyList<IParallelNode> nodes, IReadOnlyList<NodePresenceState> states)
        {
            _children[name] = nodes;
            _containerPresenceStates[name] = states;
        }

        private static bool HasPresenceMismatch(IReadOnlyList<NodePresenceState> states)
        {
            if (states.Count <= 1)
            {
                return false;
            }

            var first = states[0];
            for (var index = 1; index < states.Count; index++)
            {
                if (states[index] != first)
                {
                    return true;
                }
            }

            return false;
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
