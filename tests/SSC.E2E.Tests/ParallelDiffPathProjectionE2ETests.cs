using SSC;

namespace SSC.E2E.Tests;

public sealed class ParallelDiffPathProjectionE2ETests
{
    [Fact]
    public void GetDiffEntryPathProjections_ProjectsRecursiveRuntimeNames()
    {
        var result = ParallelCompareApi.Compare(
        [
            CreateDocument("left"),
            CreateDocument("right"),
        ]);

        var projections = result.GetDiffEntryPathProjections(new NamedTreePathProjector());

        var projection = Assert.Single(projections);
        Assert.NotNull(projection.Entry);
        Assert.NotEmpty(projection.ProjectedPath);
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
