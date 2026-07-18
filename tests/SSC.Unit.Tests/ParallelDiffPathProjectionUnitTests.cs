using SSC;

namespace SSC.Unit.Tests;

public sealed class ParallelDiffPathProjectionUnitTests
{
    [Fact]
    public void SegmentFactories_CreateConcreteSelectorsAndPreserveSelectorWhenRenamed()
    {
        var member = ParallelDiffPathSegment.Member("Root");
        var key = ParallelDiffPathSegment.Key("Items", "A]B");
        var ordinal = ParallelDiffPathSegment.Ordinal("Children", 2);
        var renamed = ordinal.WithMemberName("Child");

        Assert.Equal("Root", member.MemberName);
        Assert.Null(member.Selector);

        Assert.Equal("Items", key.MemberName);
        Assert.Equal(ParallelDiffPathSelectorKind.Key, key.Selector!.Value.Kind);
        Assert.Equal("A]B", key.Selector.Value.KeyText);
        Assert.Null(key.Selector.Value.Ordinal);

        Assert.Equal("Children", ordinal.MemberName);
        Assert.Equal(ParallelDiffPathSelectorKind.Ordinal, ordinal.Selector!.Value.Kind);
        Assert.Equal(2, ordinal.Selector.Value.Ordinal);
        Assert.Null(ordinal.Selector.Value.KeyText);

        Assert.Equal("Child", renamed.MemberName);
        Assert.Equal(ParallelDiffPathSelectorKind.Ordinal, renamed.Selector!.Value.Kind);
        Assert.Equal(2, renamed.Selector.Value.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Child.Name")]
    [InlineData("Child[0")]
    [InlineData("Child]")]
    public void SegmentFactories_RejectInvalidMemberNames(string memberName)
    {
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Member(memberName));
    }

    [Fact]
    public void SegmentFactories_RejectNullMemberEmptyKeyAndNegativeOrdinal()
    {
        Assert.Throws<ArgumentNullException>(() => ParallelDiffPathSegment.Member(null!));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Key("Items", string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelDiffPathSegment.Ordinal("Items", -1));
    }

    [Fact]
    public void GetDiffEntryPathProjections_ReplacesAndOmitsSegmentsWhilePreservingStandardEntry()
    {
        var valueNode = new FakeNode(value: "left", state: ValueState.Mismatched);
        var itemNode = new FakeNode(value: new NamedNode("ItemA"), state: ValueState.Matched);
        itemNode.SetMember("Value", valueNode);
        var wrapperNode = new FakeNode(value: new object(), state: ValueState.Matched);
        wrapperNode.SetChildren("Items", [itemNode]);
        var root = new FakeRootNode();
        root.SetMember("Wrapper", wrapperNode);
        var result = new CompareResult<FakeRoot> { Root = root };
        var contexts = new List<ParallelDiffPathProjectionContext>();
        var projector = new DelegateProjector(context =>
        {
            contexts.Add(context);
            if (context.Current.StandardSegment.MemberName == "Wrapper")
            {
                return ParallelDiffPathSegmentProjection.Omit();
            }

            if (context.Current.StandardSegment.MemberName == "Items"
                && context.Current.Node?.GetValue(0) is NamedNode named)
            {
                return ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName(named.Name));
            }

            return ParallelDiffPathSegmentProjection.KeepStandard();
        });

        var standard = Assert.Single(result.GetDiffEntries());
        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal("Wrapper.Items[#0].Value", standard.Path);
        Assert.Equal("Wrapper.Items[#0]", standard.ParentPath);
        Assert.Equal(standard.Path, projection.Entry.Path);
        Assert.Equal(standard.ParentPath, projection.Entry.ParentPath);
        Assert.Same(standard.Node, projection.Entry.Node);
        Assert.Same(standard.ParentNode, projection.Entry.ParentNode);
        Assert.Equal("ItemA[#0].Value", projection.ProjectedPath);
        Assert.Equal("ItemA[#0]", projection.ProjectedParentPath);

        Assert.Collection(
            contexts,
            context =>
            {
                Assert.Empty(context.Ancestors);
                Assert.Equal("Wrapper", context.Current.StandardSegment.MemberName);
                Assert.Same(root, context.Current.ParentNode);
                Assert.Same(wrapperNode, context.Current.Node);
                Assert.Single(context.Current.Siblings, wrapperNode);
            },
            context =>
            {
                Assert.Single(context.Ancestors);
                Assert.Equal("Wrapper", context.Ancestors[0].StandardSegment.MemberName);
                Assert.Equal("Items", context.Current.StandardSegment.MemberName);
                Assert.Equal(ParallelDiffPathSelectorKind.Ordinal, context.Current.StandardSegment.Selector!.Value.Kind);
                Assert.Equal(0, context.Current.StandardSegment.Selector.Value.Ordinal);
                Assert.Same(wrapperNode, context.Current.ParentNode);
                Assert.Same(itemNode, context.Current.Node);
                Assert.Single(context.Current.Siblings, itemNode);
            },
            context =>
            {
                Assert.Equal(2, context.Ancestors.Count);
                Assert.Equal("Wrapper", context.Ancestors[0].StandardSegment.MemberName);
                Assert.Equal("Items", context.Ancestors[1].StandardSegment.MemberName);
                Assert.Equal("Value", context.Current.StandardSegment.MemberName);
                Assert.Same(itemNode, context.Current.ParentNode);
                Assert.Same(valueNode, context.Current.Node);
                Assert.Single(context.Current.Siblings, valueNode);
            });
    }

    [Fact]
    public void GetDiffEntryPathProjections_ProvidesAllContainerSiblings()
    {
        var first = new FakeNode(value: "first", state: ValueState.Matched);
        var secondValue = new FakeNode(value: "different", state: ValueState.Mismatched);
        var second = new FakeNode(value: "second", state: ValueState.Matched);
        second.SetMember("Value", secondValue);
        var root = new FakeRootNode();
        root.SetChildren("Items", [first, second]);
        var result = new CompareResult<FakeRoot> { Root = root };
        ParallelDiffPathProjectionContext? itemContext = null;
        var projector = new DelegateProjector(context =>
        {
            if (context.Current.StandardSegment.MemberName == "Items")
            {
                itemContext = context;
            }

            return ParallelDiffPathSegmentProjection.KeepStandard();
        });

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal("Items[#1].Value", projection.ProjectedPath);
        Assert.NotNull(itemContext);
        Assert.Empty(itemContext.Ancestors);
        Assert.Equal(2, itemContext.Current.Siblings.Count);
        Assert.Same(first, itemContext.Current.Siblings[0]);
        Assert.Same(second, itemContext.Current.Siblings[1]);
        Assert.Same(second, itemContext.Current.Node);
    }

    [Fact]
    public void GetDiffEntryPathProjections_EscapesKeySelectorAndMatchesProjectedPattern()
    {
        var valueNode = new FakeNode(value: "different", state: ValueState.Mismatched);
        var itemNode = new FakeNode(value: new object(), state: ValueState.Matched, keyText: "A]B");
        itemNode.SetMember("Value", valueNode);
        var root = new FakeRootNode();
        root.SetChildren("Items", [itemNode]);
        var result = new CompareResult<FakeRoot> { Root = root };
        var projector = new DelegateProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName("NamedItems"))
                : ParallelDiffPathSegmentProjection.KeepStandard());

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));
        var pattern = ParallelDiffPathPattern.Parse("NamedItems[*].Value");

        Assert.Equal("Items[A\\]B].Value", projection.Entry.Path);
        Assert.Equal("NamedItems[A\\]B].Value", projection.ProjectedPath);
        Assert.True(projection.PathMatches(pattern));
        Assert.False(projection.Entry.PathMatches(pattern));
    }

    [Fact]
    public void GetDiffEntryPathProjections_ProvidesNullNodeForContainerPresenceEntry()
    {
        var root = new FakeRootNode();
        root.SetChildren(
            "Items",
            [],
            [NodePresenceState.PresentValue, NodePresenceState.Missing]);
        var result = new CompareResult<FakeRoot> { Root = root };
        ParallelDiffPathProjectionContext? captured = null;
        var projector = new DelegateProjector(context =>
        {
            captured = context;
            return ParallelDiffPathSegmentProjection.KeepStandard();
        });

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal(ParallelDiffEntryKind.ContainerPresence, projection.Entry.Kind);
        Assert.Equal("Items", projection.Entry.Path);
        Assert.Equal("Items", projection.ProjectedPath);
        Assert.Null(projection.ProjectedParentPath);
        Assert.NotNull(captured);
        Assert.Empty(captured.Ancestors);
        Assert.Same(root, captured.Current.ParentNode);
        Assert.Null(captured.Current.Node);
        Assert.Empty(captured.Current.Siblings);
    }

    [Fact]
    public void GetDiffEntryPathProjections_RejectsEmptyProjectedPath()
    {
        var valueNode = new FakeNode(value: "different", state: ValueState.Mismatched);
        var root = new FakeRootNode();
        root.SetMember("Value", valueNode);
        var result = new CompareResult<FakeRoot> { Root = root };
        var projector = new DelegateProjector(_ => ParallelDiffPathSegmentProjection.Omit());

        var exception = Assert.Throws<InvalidOperationException>(
            () => result.GetDiffEntryPathProjections(projector));

        Assert.Contains("Value", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDiffEntryPathProjections_PropagatesProjectorException()
    {
        var valueNode = new FakeNode(value: "different", state: ValueState.Mismatched);
        var root = new FakeRootNode();
        root.SetMember("Value", valueNode);
        var result = new CompareResult<FakeRoot> { Root = root };
        var expected = new TestProjectorException();
        var projector = new DelegateProjector(_ => throw expected);

        var actual = Assert.Throws<TestProjectorException>(
            () => result.GetDiffEntryPathProjections(projector));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void GetDiffEntryPathProjections_ValidatesArgumentsAndEmptyRoot()
    {
        CompareResult<FakeRoot> nullResult = null!;
        var projector = new DelegateProjector(_ => ParallelDiffPathSegmentProjection.KeepStandard());

        Assert.Throws<ArgumentNullException>(
            () => nullResult.GetDiffEntryPathProjections(projector));

        var emptyResult = new CompareResult<FakeRoot>();
        Assert.Throws<ArgumentNullException>(
            () => emptyResult.GetDiffEntryPathProjections(null!));
        Assert.Empty(emptyResult.GetDiffEntryPathProjections(projector));
    }

    private sealed record NamedNode(string Name);

    private sealed class DelegateProjector : IParallelDiffPathProjector
    {
        private readonly Func<ParallelDiffPathProjectionContext, ParallelDiffPathSegmentProjection> _project;

        public DelegateProjector(
            Func<ParallelDiffPathProjectionContext, ParallelDiffPathSegmentProjection> project)
        {
            _project = project;
        }

        public ParallelDiffPathSegmentProjection Project(
            ParallelDiffPathProjectionContext context)
        {
            return _project(context);
        }
    }

    private sealed class TestProjectorException : Exception;

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

        public bool HasDifferences()
        {
            if (_state == ValueState.Mismatched)
            {
                return true;
            }

            return GetDirectChildren().Any(childSet => childSet.HasDifferences);
        }

        public IReadOnlyList<ParallelChildSet> GetDirectChildren()
        {
            var sets = new List<ParallelChildSet>();
            sets.AddRange(_members.Select(pair => new ParallelChildSet(
                pair.Key,
                [pair.Value],
                pair.Value.HasDifferences())));
            sets.AddRange(_children.Select(pair => new ParallelChildSet(
                pair.Key,
                pair.Value,
                HasPresenceMismatch(_containerPresenceStates[pair.Key])
                    || pair.Value.Any(node => node.HasDifferences()))));
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
            return _state == ValueState.Missing
                ? NodePresenceState.Missing
                : NodePresenceState.PresentValue;
        }

        public void SetMember(string name, IParallelNode node)
        {
            _members[name] = node;
        }

        public void SetChildren(string name, IReadOnlyList<IParallelNode> nodes)
        {
            SetChildren(name, nodes, [NodePresenceState.PresentValue]);
        }

        public void SetChildren(
            string name,
            IReadOnlyList<IParallelNode> nodes,
            IReadOnlyList<NodePresenceState> states)
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
