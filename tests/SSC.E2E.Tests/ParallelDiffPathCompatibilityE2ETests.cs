using SSC;

namespace SSC.E2E.Tests;

public sealed class ParallelDiffPathCompatibilityE2ETests
{
    [Fact]
    public void GetDiffEntries_PreservesExistingEmptyCompareKeyPath()
    {
        var result = ParallelCompareApi.Compare(
        [
            new Dataset
            {
                Items =
                [
                    new Item { Id = string.Empty, Value = "left" },
                ],
            },
            new Dataset
            {
                Items =
                [
                    new Item { Id = string.Empty, Value = "right" },
                ],
            },
        ]);

        var entry = Assert.Single(result.GetDiffEntries());
        var projection = Assert.Single(
            result.GetDiffEntryPathProjections(new KeepStandardProjector()));

        Assert.Equal("Items[].Value", entry.Path);
        Assert.Equal(entry.Path, projection.Entry.Path);
        Assert.Equal("Items[].Value", projection.ProjectedPath);
        Assert.Equal("Items[]", projection.ProjectedParentPath);
        Assert.Equal(ParallelDiffPathSelectorKind.Key, Assert.Single(
            new[]
            {
                GetItemSelector(projection),
            }).Kind);
        Assert.Equal(string.Empty, GetItemSelector(projection).KeyText);
    }

    private static ParallelDiffPathSelector GetItemSelector(
        ParallelDiffEntryPathProjection projection)
    {
        var projector = new CapturingProjector();
        _ = ParallelCompareApi.Compare(
        [
            new Dataset
            {
                Items =
                [
                    new Item { Id = string.Empty, Value = "left" },
                ],
            },
            new Dataset
            {
                Items =
                [
                    new Item { Id = string.Empty, Value = "right" },
                ],
            },
        ]).GetDiffEntryPathProjections(projector);

        Assert.NotNull(projector.ItemSelector);
        return projector.ItemSelector.Value;
    }

    private sealed class KeepStandardProjector : IParallelDiffPathProjector
    {
        public ParallelDiffPathSegmentProjection Project(
            ParallelDiffPathProjectionContext context)
        {
            return ParallelDiffPathSegmentProjection.KeepStandard();
        }
    }

    private sealed class CapturingProjector : IParallelDiffPathProjector
    {
        public ParallelDiffPathSelector? ItemSelector { get; private set; }

        public ParallelDiffPathSegmentProjection Project(
            ParallelDiffPathProjectionContext context)
        {
            if (context.Current.StandardSegment.MemberName == "Items")
            {
                ItemSelector = context.Current.StandardSegment.Selector;
            }

            return ParallelDiffPathSegmentProjection.KeepStandard();
        }
    }

    public sealed class Dataset
    {
        public List<Item> Items { get; init; } = [];
    }

    public sealed class Item
    {
        [CompareKey]
        public string Id { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;
    }
}
