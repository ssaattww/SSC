using SSC;
using SSC.Internal;

namespace SSC.Unit.Tests;

public sealed class ParallelDiffPathProjectionUnitTests
{
    [Fact]
    public void SegmentFactories_CreateMemberKeyAndOrdinalSegments()
    {
        var member = ParallelDiffPathSegment.Member("Root");
        var key = ParallelDiffPathSegment.Key("Items", "A");
        var ordinal = ParallelDiffPathSegment.Ordinal("Children", 2);

        Assert.Equal("Root", member.MemberName);
        Assert.Null(member.Selector);

        Assert.Equal("Items", key.MemberName);
        Assert.NotNull(key.Selector);
        Assert.Equal(ParallelDiffPathSelectorKind.Key, key.Selector.Value.Kind);
        Assert.Equal("A", key.Selector.Value.KeyText);
        Assert.Null(key.Selector.Value.Ordinal);

        Assert.Equal("Children", ordinal.MemberName);
        Assert.NotNull(ordinal.Selector);
        Assert.Equal(ParallelDiffPathSelectorKind.Ordinal, ordinal.Selector.Value.Kind);
        Assert.Null(ordinal.Selector.Value.KeyText);
        Assert.Equal(2, ordinal.Selector.Value.Ordinal);
    }

    [Fact]
    public void WithMemberName_PreservesKeySelector()
    {
        var standard = ParallelDiffPathSegment.Key("Items", "A");

        var projected = standard.WithMemberName("Entries");

        Assert.Equal("Entries", projected.MemberName);
        Assert.NotNull(projected.Selector);
        Assert.Equal(ParallelDiffPathSelectorKind.Key, projected.Selector.Value.Kind);
        Assert.Equal("A", projected.Selector.Value.KeyText);
    }

    [Fact]
    public void WithMemberName_PreservesOrdinalSelector()
    {
        var standard = ParallelDiffPathSegment.Ordinal("Children", 3);

        var projected = standard.WithMemberName("Child");

        Assert.Equal("Child", projected.MemberName);
        Assert.NotNull(projected.Selector);
        Assert.Equal(ParallelDiffPathSelectorKind.Ordinal, projected.Selector.Value.Kind);
        Assert.Equal(3, projected.Selector.Value.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A.B")]
    [InlineData("A[B")]
    [InlineData("A]B")]
    public void SegmentFactories_RejectMemberNamesThatCannotBeRepresented(string memberName)
    {
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Member(memberName));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Key(memberName, "A"));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Ordinal(memberName, 0));
    }

    [Fact]
    public void SegmentFactories_RejectNullMemberName()
    {
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Member(null!));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Key(null!, "A"));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Ordinal(null!, 0));
    }

    [Fact]
    public void KeyAndOrdinalFactories_RejectInvalidSelectorValues()
    {
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Key("Items", null!));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Key("Items", string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelDiffPathSegment.Ordinal("Items", -1));
    }

    [Fact]
    public void ProjectionFactories_RepresentKeepReplaceAndOmit()
    {
        var replacement = ParallelDiffPathSegment.Member("Alias");

        var keep = ParallelDiffPathSegmentProjection.KeepStandard();
        var replace = ParallelDiffPathSegmentProjection.Replace(replacement);
        var omit = ParallelDiffPathSegmentProjection.Omit();

        Assert.Equal(ParallelDiffPathSegmentProjectionKind.KeepStandard, keep.Kind);
        Assert.Null(keep.Replacement);
        Assert.Equal(ParallelDiffPathSegmentProjectionKind.Replace, replace.Kind);
        Assert.Same(replacement, replace.Replacement);
        Assert.Equal(ParallelDiffPathSegmentProjectionKind.Omit, omit.Kind);
        Assert.Null(omit.Replacement);
        Assert.Throws<ArgumentNullException>(() => ParallelDiffPathSegmentProjection.Replace(null!));
    }

    [Fact]
    public void GetDiffEntryPathProjections_ReplacesSegmentAndKeepsStandardEntryResolvable()
    {
        var result = CreateSingleItemResult();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName("Entry"))
                : ParallelDiffPathSegmentProjection.KeepStandard());

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal("Items[#0].Name", projection.Entry.Path);
        Assert.Equal("Items[#0]", projection.Entry.ParentPath);
        Assert.Equal("Entry[#0].Name", projection.ProjectedPath);
        Assert.Equal("Entry[#0]", projection.ProjectedParentPath);
        Assert.NotNull(projection.Entry.Node);
        Assert.Same(projection.Entry.Node, result.GetNodeByPath(projection.Entry.Path));

        Assert.Collection(
            projector.Contexts,
            first =>
            {
                Assert.Empty(first.Ancestors);
                Assert.Equal("Items", first.Current.StandardSegment.MemberName);
                Assert.NotNull(first.Current.Node);
                Assert.Single(first.Current.Siblings);
                Assert.Same(result.Root, first.Current.ParentNode);
            },
            second =>
            {
                var ancestor = Assert.Single(second.Ancestors);
                Assert.Equal("Items", ancestor.StandardSegment.MemberName);
                Assert.Equal("Name", second.Current.StandardSegment.MemberName);
                Assert.NotNull(second.Current.Node);
                Assert.Single(second.Current.Siblings);
            });
    }

    [Fact]
    public void GetDiffEntryPathProjections_OmitsLeafAndAllowsPathToEqualParentPath()
    {
        var result = CreateSingleItemResult();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName switch
            {
                "Items" => ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName("Entry")),
                "Name" => ParallelDiffPathSegmentProjection.Omit(),
                _ => ParallelDiffPathSegmentProjection.KeepStandard(),
            });

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal("Entry[#0]", projection.ProjectedPath);
        Assert.Equal("Entry[#0]", projection.ProjectedParentPath);
    }

    [Fact]
    public void GetDiffEntryPathProjections_RejectsAnEmptyProjectedPath()
    {
        var result = CreateSingleItemResult();
        var projector = new RecordingProjector(_ => ParallelDiffPathSegmentProjection.Omit());

        var exception = Assert.Throws<InvalidOperationException>(
            () => result.GetDiffEntryPathProjections(projector));

        Assert.Contains("Items[#0].Name", exception.Message, StringComparison.Ordinal);
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
        var projector = new RecordingProjector(_ => ParallelDiffPathSegmentProjection.KeepStandard());

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));
        var context = Assert.Single(projector.Contexts);

        Assert.Equal(ParallelDiffEntryKind.ContainerPresence, projection.Entry.Kind);
        Assert.Equal("Items", projection.ProjectedPath);
        Assert.Null(projection.ProjectedParentPath);
        Assert.Null(context.Current.Node);
        Assert.Empty(context.Current.Siblings);
        Assert.Same(root, context.Current.ParentNode);
    }

    [Theory]
    [InlineData("A]B", "Alias[A\\]B].Name")]
    [InlineData("A\\B", "Alias[A\\\\B].Name")]
    [InlineData("#0", "Alias[\\#0].Name")]
    public void GetDiffEntryPathProjections_EscapesProjectedKeyText(
        string keyText,
        string expectedPath)
    {
        var result = CreateSingleItemResult();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    ParallelDiffPathSegment.Key("Alias", keyText))
                : ParallelDiffPathSegmentProjection.KeepStandard());

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal(expectedPath, projection.ProjectedPath);
    }

    [Fact]
    public void ProjectionPathMatches_UsesProjectedPathWithoutChangingStandardMatching()
    {
        var result = CreateSingleItemResult();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName("Entry"))
                : ParallelDiffPathSegmentProjection.KeepStandard());
        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));
        var projectedPattern = ParallelDiffPathPattern.Parse("Entry[*].Name");

        Assert.True(projection.PathMatches(projectedPattern));
        Assert.False(projection.Entry.PathMatches(projectedPattern));
    }

    [Fact]
    public void GetDiffEntryPathProjections_PreservesDuplicateProjectedPaths()
    {
        var firstLeaf = new FakeNode("left-a", ValueState.Mismatched);
        var firstItem = new FakeNode(new object(), ValueState.Mismatched, keyText: "A");
        firstItem.SetMember("Name", firstLeaf);
        var secondLeaf = new FakeNode("left-b", ValueState.Mismatched);
        var secondItem = new FakeNode(new object(), ValueState.Mismatched, keyText: "B");
        secondItem.SetMember("Name", secondLeaf);
        var root = new FakeRootNode();
        root.SetChildren("Items", [firstItem, secondItem]);
        var result = new CompareResult<FakeRoot> { Root = root };
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    ParallelDiffPathSegment.Member("Item"))
                : ParallelDiffPathSegmentProjection.KeepStandard());

        var projections = result.GetDiffEntryPathProjections(projector);

        Assert.Equal(2, projections.Count);
        Assert.All(projections, projection => Assert.Equal("Item.Name", projection.ProjectedPath));
        Assert.Equal(
            ["Items[A].Name", "Items[B].Name"],
            projections.Select(projection => projection.Entry.Path).ToArray());
    }

    [Fact]
    public void GetDiffEntryPathProjections_DoesNotChangeStandardDiffEntries()
    {
        var result = CreateSingleItemResult();
        var before = result.GetDiffEntries();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName("Entry"))
                : ParallelDiffPathSegmentProjection.KeepStandard());

        var projections = result.GetDiffEntryPathProjections(projector);
        var after = result.GetDiffEntries();

        Assert.Equal(
            before.Select(ToEntrySnapshot),
            projections.Select(projection => ToEntrySnapshot(projection.Entry)));
        Assert.Equal(before.Select(ToEntrySnapshot), after.Select(ToEntrySnapshot));
    }

    [Fact]
    public void GetDiffEntryPathProjections_ValidatesArgumentsAndPropagatesProjectorExceptions()
    {
        var result = CreateSingleItemResult();
        var projectorException = new InvalidOperationException("projector failure");
        var throwingProjector = new RecordingProjector(_ => throw projectorException);

        Assert.Throws<ArgumentNullException>(
            () => ParallelPathAccessExtensions.GetDiffEntryPathProjections<FakeRoot>(null!, throwingProjector));
        Assert.Throws<ArgumentNullException>(() => result.GetDiffEntryPathProjections(null!));
        Assert.Same(
            projectorException,
            Assert.Throws<InvalidOperationException>(
                () => result.GetDiffEntryPathProjections(throwingProjector)));
    }

    [Fact]
    public void GetDiffEntryPathProjections_ReturnsEmptyWhenResultHasNoRoot()
    {
        var result = new CompareResult<FakeRoot>();
        var projector = new RecordingProjector(_ => ParallelDiffPathSegmentProjection.KeepStandard());

        Assert.Empty(result.GetDiffEntryPathProjections(projector));
        Assert.Empty(projector.Contexts);
    }

    private static CompareResult<FakeRoot> CreateSingleItemResult()
    {
        var nameNode = new FakeNode("left", ValueState.Mismatched);
        var itemNode = new FakeNode(new object(), ValueState.Mismatched);
        itemNode.SetMember("Name", nameNode);
        var root = new FakeRootNode();
        root.SetChildren("Items", [itemNode]);
        return new CompareResult<FakeRoot> { Root = root };
    }

    private static object ToEntrySnapshot(ParallelDiffEntry entry)
    {
        return new
        {
            entry.Path,
            entry.ParentPath,
            entry.Kind,
            entry.ParentNode,
            entry.Node,
            Values = entry.Values
                .Select(value => new { value.ModelIndex, value.Value, value.State })
                .ToArray(),
        };
    }

    private sealed class RecordingProjector : IParallelDiffPathProjector
    {
        private readonly Func<ParallelDiffPathProjectionContext, ParallelDiffPathSegmentProjection> _project;

        public RecordingProjector(
            Func<ParallelDiffPathProjectionContext, ParallelDiffPathSegmentProjection> project)
        {
            _project = project;
        }

        public List<ParallelDiffPathProjectionContext> Contexts { get; } = [];

        public ParallelDiffPathSegmentProjection Project(
            ParallelDiffPathProjectionContext context)
        {
            Contexts.Add(context);
            return _project(context);
        }
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

        public bool HasDifferences()
        {
            return _state == ValueState.Mismatched
                || _members.Values.Any(node => node.HasDifferences())
                || _children.Values.SelectMany(nodes => nodes).Any(node => node.HasDifferences())
                || _containerPresenceStates.Values.Any(HasPresenceMismatch);
        }

        public IReadOnlyList<ParallelChildSet> GetDirectChildren()
        {
            var sets = new List<ParallelChildSet>();
            sets.AddRange(_members.Select(pair =>
                new ParallelChildSet(pair.Key, [pair.Value], pair.Value.HasDifferences())));
            sets.AddRange(_children.Select(pair =>
                new ParallelChildSet(
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

        public bool TryGetContainerPresenceStates(
            string memberName,
            out IReadOnlyList<NodePresenceState> states)
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
