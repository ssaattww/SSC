using SSC;

namespace SSC.E2E.Tests;

public sealed class ParallelDiffPathProjectionE2ETests
{
    [Fact]
    public void GetDiffEntryPathProjections_UsesRuntimeNamesForRecursiveModelAndMatchesPattern()
    {
        var result = ParallelCompareApi.Compare(CreateNamedDocuments("0", "1"));
        var projector = new CommonNamePathProjector();

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal(
            "Root.Children[0].Children[0].Fields[0].Value",
            projection.Entry.Path);
        Assert.Equal(
            "Root.Children[0].Children[0].Fields[0]",
            projection.Entry.ParentPath);
        Assert.Equal(
            "Root.Child1[0].Child2[0].Attribute1[0].Value",
            projection.ProjectedPath);
        Assert.Equal(
            "Root.Child1[0].Child2[0].Attribute1[0]",
            projection.ProjectedParentPath);

        Assert.NotNull(projection.Entry.Node);
        Assert.Same(projection.Entry.Node, result.GetNodeByPath(projection.Entry.Path));
        Assert.Null(result.GetNodeByPath(projection.ProjectedPath));

        var pattern = ParallelDiffPathPattern.Parse(
            "Root.Child1[*].Child2[*].Attribute1[*].Value");
        Assert.True(projection.PathMatches(pattern));
        Assert.False(projection.Entry.PathMatches(pattern));

        var finalContext = projector.Contexts[^1];
        Assert.Equal("Value", finalContext.Current.StandardSegment.MemberName);
        Assert.Equal(
            ["Root", "Children", "Children", "Fields"],
            finalContext.Ancestors
                .Select(context => context.StandardSegment.MemberName)
                .ToArray());
    }

    [Fact]
    public void GetDiffEntryPathProjections_FallsBackToStandardSegmentWhenRuntimeNamesDiffer()
    {
        var left = CreateNamedDocument("0", secondNodeName: "Child2");
        var right = CreateNamedDocument("1", secondNodeName: "ChildX");
        var result = ParallelCompareApi.Compare([left, right]);
        var projector = new CommonNamePathProjector();

        var projection = Assert.Single(
            result.GetDiffEntryPathProjections(projector),
            candidate => candidate.Entry.Path.EndsWith(".Value", StringComparison.Ordinal));

        Assert.Equal(
            "Root.Children[0].Children[0].Fields[0].Value",
            projection.Entry.Path);
        Assert.Equal(
            "Root.Child1[0].Children[0].Attribute1[0].Value",
            projection.ProjectedPath);
    }

    [Fact]
    public void GetDiffEntryPathProjections_PreservesKeySelectorWhenRenamingKeyedContainer()
    {
        var result = ParallelCompareApi.Compare(
        [
            new KeyedDocument
            {
                Items =
                [
                    new KeyedItem { Id = 100, Name = "Temperature", Value = 1 },
                ],
            },
            new KeyedDocument
            {
                Items =
                [
                    new KeyedItem { Id = 100, Name = "Temperature", Value = 2 },
                ],
            },
        ]);

        var projection = Assert.Single(
            result.GetDiffEntryPathProjections(new CommonNamePathProjector()));

        Assert.Equal("Items[100].Value", projection.Entry.Path);
        Assert.Equal("Temperature[100].Value", projection.ProjectedPath);
        Assert.Equal("Temperature[100]", projection.ProjectedParentPath);
    }

    [Fact]
    public void GetDiffEntryPathProjections_ProvidesContainerPresenceContextWithoutElementNode()
    {
        var result = ParallelCompareApi.Compare(
        [
            new OptionalDocument { Items = [] },
            new OptionalDocument { Items = null },
        ]);
        var projector = new CommonNamePathProjector();

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));
        var context = Assert.Single(projector.Contexts);

        Assert.Equal(ParallelDiffEntryKind.ContainerPresence, projection.Entry.Kind);
        Assert.Equal("Items", projection.Entry.Path);
        Assert.Equal("Items", projection.ProjectedPath);
        Assert.Null(projection.ProjectedParentPath);
        Assert.Null(context.Current.Node);
        Assert.Empty(context.Current.Siblings);
        Assert.Empty(context.Ancestors);
    }

    [Fact]
    public void GetDiffEntryPathProjections_DoesNotChangeStandardEntriesOrComparisonResult()
    {
        var result = ParallelCompareApi.Compare(CreateNamedDocuments("0", "1"));
        var before = result.GetDiffEntries();
        var originalRoot = result.Root;
        var originalIssues = result.Issues;
        var originalHasError = result.HasError;

        var projections = result.GetDiffEntryPathProjections(new CommonNamePathProjector());
        var after = result.GetDiffEntries();

        Assert.Equal(
            before.Select(ToEntrySnapshot),
            projections.Select(projection => ToEntrySnapshot(projection.Entry)));
        Assert.Equal(before.Select(ToEntrySnapshot), after.Select(ToEntrySnapshot));
        Assert.Same(originalRoot, result.Root);
        Assert.Same(originalIssues, result.Issues);
        Assert.Equal(originalHasError, result.HasError);
        Assert.True(Assert.IsAssignableFrom<IParallelNode>(result.Root).HasDifferences());
    }

    [Fact]
    public void GetDiffEntryPathProjections_ReturnsEmptyForEqualModels()
    {
        var model = CreateNamedDocument("0", secondNodeName: "Child2");
        var result = ParallelCompareApi.Compare([model, model]);
        var projector = new CommonNamePathProjector();

        Assert.Empty(result.GetDiffEntryPathProjections(projector));
        Assert.Empty(projector.Contexts);
    }

    private static IReadOnlyList<NamedDocument> CreateNamedDocuments(
        string leftValue,
        string rightValue)
    {
        return
        [
            CreateNamedDocument(leftValue, secondNodeName: "Child2"),
            CreateNamedDocument(rightValue, secondNodeName: "Child2"),
        ];
    }

    private static NamedDocument CreateNamedDocument(
        string value,
        string secondNodeName)
    {
        return new NamedDocument
        {
            Root = new NamedNode
            {
                Name = "Root",
                Children =
                [
                    new NamedNode
                    {
                        Name = "Child1",
                        Children =
                        [
                            new NamedNode
                            {
                                Name = secondNodeName,
                                Fields =
                                [
                                    new NamedValue
                                    {
                                        Name = "Attribute1",
                                        Value = value,
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };
    }

    private static string ToEntrySnapshot(ParallelDiffEntry entry)
    {
        return $"{entry.Path}|{entry.ParentPath ?? "<root>"}|{entry.Kind}|{entry}";
    }

    private sealed class CommonNamePathProjector : IParallelDiffPathProjector
    {
        public List<ParallelDiffPathProjectionContext> Contexts { get; } = [];

        public ParallelDiffPathSegmentProjection Project(
            ParallelDiffPathProjectionContext context)
        {
            Contexts.Add(context);

            return context.Current.StandardSegment.MemberName switch
            {
                "Children" => ProjectName<NamedNode>(context, node => node.Name),
                "Fields" => ProjectName<NamedValue>(context, field => field.Name),
                "Items" => ProjectName<KeyedItem>(context, item => item.Name),
                _ => ParallelDiffPathSegmentProjection.KeepStandard(),
            };
        }

        private static ParallelDiffPathSegmentProjection ProjectName<T>(
            ParallelDiffPathProjectionContext context,
            Func<T, string> getName)
        {
            if (context.Current.Node is not IParallelNode node)
            {
                return ParallelDiffPathSegmentProjection.KeepStandard();
            }

            var name = TryGetCommonName(node, getName);
            return name is null
                ? ParallelDiffPathSegmentProjection.KeepStandard()
                : ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName(name));
        }

        private static string? TryGetCommonName<T>(
            IParallelNode node,
            Func<T, string> getName)
        {
            string? commonName = null;
            var found = false;

            for (var modelIndex = 0; modelIndex < node.Count; modelIndex++)
            {
                if (node.GetState(modelIndex) == ValueState.Missing)
                {
                    continue;
                }

                if (node.GetValue(modelIndex) is not T value)
                {
                    return null;
                }

                var candidate = getName(value);
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return null;
                }

                if (!found)
                {
                    commonName = candidate;
                    found = true;
                    continue;
                }

                if (!string.Equals(commonName, candidate, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return found ? commonName : null;
        }
    }

    public sealed class NamedDocument
    {
        public NamedNode Root { get; init; } = new();
    }

    public sealed class NamedNode
    {
        public string Name { get; init; } = string.Empty;

        public List<NamedNode> Children { get; init; } = [];

        public List<NamedValue> Fields { get; init; } = [];
    }

    public sealed class NamedValue
    {
        public string Name { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;
    }

    public sealed class KeyedDocument
    {
        public List<KeyedItem> Items { get; init; } = [];
    }

    public sealed class KeyedItem
    {
        [CompareKey]
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public int Value { get; init; }
    }

    public sealed class OptionalDocument
    {
        public List<KeyedItem>? Items { get; init; }
    }
}
