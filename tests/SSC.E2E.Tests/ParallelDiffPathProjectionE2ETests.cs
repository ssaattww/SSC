using SSC;

namespace SSC.E2E.Tests;

public sealed class ParallelDiffPathProjectionE2ETests
{
    [Fact]
    public void GetDiffEntryPathProjections_ProjectsRecursiveRuntimeNamesAndFiltersResult()
    {
        var result = ParallelCompareApi.Compare(
        [
            CreateDocument("left"),
            CreateDocument("right"),
        ]);
        var standardEntriesBeforeProjection = result.GetDiffEntries();

        var projections = result.GetDiffEntryPathProjections(new NamedTreePathProjector());

        var projection = Assert.Single(projections);
        Assert.Equal(
            "Root.Children[#0].Children[#0].Fields[#0].Value",
            projection.Entry.Path);
        Assert.Equal(
            "Root.Children[#0].Children[#0].Fields[#0]",
            projection.Entry.ParentPath);
        Assert.Equal(
            "Root.Child1[#0].Child2[#0].Attribute1[#0].Value",
            projection.ProjectedPath);
        Assert.Equal(
            "Root.Child1[#0].Child2[#0].Attribute1[#0]",
            projection.ProjectedParentPath);
        Assert.Same(projection.Entry.Node, result.GetNodeByPath(projection.Entry.Path));
        Assert.Same(
            projection.Entry.ParentNode,
            result.GetNodeByPath(projection.Entry.ParentPath!));

        var pattern = ParallelDiffPathPattern.Parse(
            "Root.Child1[*].Child2[*].Attribute1[*].Value");
        Assert.True(projection.PathMatches(pattern));
        Assert.False(projection.Entry.PathMatches(pattern));

        var standardEntriesAfterProjection = result.GetDiffEntries();
        Assert.Equal(
            standardEntriesBeforeProjection.Select(entry => entry.Path),
            standardEntriesAfterProjection.Select(entry => entry.Path));
        Assert.Equal(
            standardEntriesBeforeProjection.Select(entry => entry.ParentPath),
            standardEntriesAfterProjection.Select(entry => entry.ParentPath));
    }

    private static Document CreateDocument(string value)
    {
        return new Document
        {
            Root = new TreeNode
            {
                Name = "Root",
                Children =
                [
                    new TreeNode
                    {
                        Name = "Child1",
                        Children =
                        [
                            new TreeNode
                            {
                                Name = "Child2",
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

    private sealed class NamedTreePathProjector : IParallelDiffPathProjector
    {
        public ParallelDiffPathSegmentProjection Project(
            ParallelDiffPathProjectionContext context)
        {
            var standard = context.Current.StandardSegment;
            if (context.Current.Node is null)
            {
                return ParallelDiffPathSegmentProjection.KeepStandard();
            }

            if (standard.MemberName == "Children")
            {
                return TryGetCommonName<TreeNode>(
                    context.Current.Node,
                    node => node.Name) is string childName
                        ? ParallelDiffPathSegmentProjection.Replace(
                            standard.WithMemberName(childName))
                        : ParallelDiffPathSegmentProjection.KeepStandard();
            }

            if (standard.MemberName == "Fields")
            {
                return TryGetCommonName<NamedValue>(
                    context.Current.Node,
                    value => value.Name) is string fieldName
                        ? ParallelDiffPathSegmentProjection.Replace(
                            standard.WithMemberName(fieldName))
                        : ParallelDiffPathSegmentProjection.KeepStandard();
            }

            return ParallelDiffPathSegmentProjection.KeepStandard();
        }

        private static string? TryGetCommonName<TValue>(
            IParallelNode node,
            Func<TValue, string> getName)
        {
            string? commonName = null;
            for (var modelIndex = 0; modelIndex < node.Count; modelIndex++)
            {
                if (node.GetState(modelIndex) == ValueState.Missing)
                {
                    continue;
                }

                if (node.GetValue(modelIndex) is not TValue value)
                {
                    return null;
                }

                var name = getName(value);
                if (string.IsNullOrEmpty(name))
                {
                    return null;
                }

                if (commonName is null)
                {
                    commonName = name;
                    continue;
                }

                if (!string.Equals(commonName, name, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return commonName;
        }
    }

    public sealed class Document
    {
        public TreeNode Root { get; init; } = new();
    }

    public sealed class TreeNode
    {
        public string Name { get; init; } = string.Empty;

        public List<TreeNode> Children { get; init; } = [];

        public List<NamedValue> Fields { get; init; } = [];
    }

    public sealed class NamedValue
    {
        public string Name { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;
    }
}
