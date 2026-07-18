using SSC;

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
    public void WithMemberName_PreservesSelectors()
    {
        var key = ParallelDiffPathSegment.Key("Items", "A").WithMemberName("Entries");
        var ordinal = ParallelDiffPathSegment.Ordinal("Children", 3).WithMemberName("Child");

        Assert.Equal("Entries", key.MemberName);
        Assert.NotNull(key.Selector);
        Assert.Equal(ParallelDiffPathSelectorKind.Key, key.Selector.Value.Kind);
        Assert.Equal("A", key.Selector.Value.KeyText);

        Assert.Equal("Child", ordinal.MemberName);
        Assert.NotNull(ordinal.Selector);
        Assert.Equal(ParallelDiffPathSelectorKind.Ordinal, ordinal.Selector.Value.Kind);
        Assert.Equal(3, ordinal.Selector.Value.Ordinal);
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
    public void SegmentFactories_RejectNullAndInvalidSelectorValues()
    {
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Member(null!));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Key(null!, "A"));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Ordinal(null!, 0));
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
    public void GetDiffEntryPathProjections_ReplacesSegmentAndProvidesTraversalContext()
    {
        var result = CreateSingleOrdinalResult();
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
        var result = CreateSingleOrdinalResult();
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
        var result = CreateSingleOrdinalResult();
        var projector = new RecordingProjector(_ => ParallelDiffPathSegmentProjection.Omit());

        var exception = Assert.Throws<InvalidOperationException>(
            () => result.GetDiffEntryPathProjections(projector));

        Assert.Contains("Items[#0].Name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDiffEntryPathProjections_ProvidesNullNodeForContainerPresenceEntry()
    {
        var result = ParallelCompareApi.Compare(
        [
            new OptionalDocument { Items = [] },
            new OptionalDocument { Items = null },
        ]);
        var projector = new RecordingProjector(_ => ParallelDiffPathSegmentProjection.KeepStandard());

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));
        var context = Assert.Single(projector.Contexts);

        Assert.Equal(ParallelDiffEntryKind.ContainerPresence, projection.Entry.Kind);
        Assert.Equal("Items", projection.ProjectedPath);
        Assert.Null(projection.ProjectedParentPath);
        Assert.Null(context.Current.Node);
        Assert.Empty(context.Current.Siblings);
        Assert.Same(result.Root, context.Current.ParentNode);
    }

    [Theory]
    [InlineData("A]B", "Alias[A\\]B].Name")]
    [InlineData("A\\B", "Alias[A\\\\B].Name")]
    [InlineData("#0", "Alias[\\#0].Name")]
    public void GetDiffEntryPathProjections_EscapesProjectedKeyText(
        string keyText,
        string expectedPath)
    {
        var result = CreateSingleOrdinalResult();
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
        var result = CreateSingleOrdinalResult();
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
        var result = ParallelCompareApi.Compare(
        [
            new KeyedDocument
            {
                Items =
                [
                    new KeyedItem { Id = "A", Name = "left-a" },
                    new KeyedItem { Id = "B", Name = "left-b" },
                ],
            },
            new KeyedDocument
            {
                Items =
                [
                    new KeyedItem { Id = "A", Name = "right-a" },
                    new KeyedItem { Id = "B", Name = "right-b" },
                ],
            },
        ]);
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
        var result = CreateSingleOrdinalResult();
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
        var result = CreateSingleOrdinalResult();
        var projectorException = new InvalidOperationException("projector failure");
        var throwingProjector = new RecordingProjector(_ => throw projectorException);

        Assert.Throws<ArgumentNullException>(
            () => ParallelPathAccessExtensions.GetDiffEntryPathProjections<OrdinalDocument>(null!, throwingProjector));
        Assert.Throws<ArgumentNullException>(() => result.GetDiffEntryPathProjections(null!));
        Assert.Same(
            projectorException,
            Assert.Throws<InvalidOperationException>(
                () => result.GetDiffEntryPathProjections(throwingProjector)));
    }

    [Fact]
    public void GetDiffEntryPathProjections_ReturnsEmptyWhenResultHasNoRoot()
    {
        var result = new CompareResult<OrdinalDocument>();
        var projector = new RecordingProjector(_ => ParallelDiffPathSegmentProjection.KeepStandard());

        Assert.Empty(result.GetDiffEntryPathProjections(projector));
        Assert.Empty(projector.Contexts);
    }

    private static CompareResult<OrdinalDocument> CreateSingleOrdinalResult()
    {
        return ParallelCompareApi.Compare(
        [
            new OrdinalDocument
            {
                Items = [new OrdinalItem { Name = "left" }],
            },
            new OrdinalDocument
            {
                Items = [new OrdinalItem { Name = "right" }],
            },
        ]);
    }

    private static string ToEntrySnapshot(ParallelDiffEntry entry)
    {
        return $"{entry.Path}|{entry.ParentPath ?? "<root>"}|{entry.Kind}|{entry}";
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

    public sealed class OrdinalDocument
    {
        public List<OrdinalItem> Items { get; init; } = [];
    }

    public sealed class OrdinalItem
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class OptionalDocument
    {
        public List<OrdinalItem>? Items { get; init; }
    }

    public sealed class KeyedDocument
    {
        public List<KeyedItem> Items { get; init; } = [];
    }

    public sealed class KeyedItem
    {
        [CompareKey]
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;
    }
}
