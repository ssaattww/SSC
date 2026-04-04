using SSC;
using SSC.Generated;

namespace SSC.E2E.Tests;

public sealed class LinqCompatibilityE2ETests
{
    [Fact]
    public void Compare_NodeChildren_SupportsCoreLinqOperators()
    {
        // Intent: Children(...) が返す list に対し、主要 LINQ 演算を連鎖利用できる。
        LinqDataset[] models =
        {
            new LinqDataset
            {
                Groups =
                [
                    new LinqGroup
                    {
                        GroupId = 2,
                        Items =
                        [
                            new LinqItem { ItemId = 200, MetricA = 2.0 },
                            new LinqItem { ItemId = 100, MetricA = 1.0 },
                        ],
                    },
                    new LinqGroup
                    {
                        GroupId = 1,
                        Items =
                        [
                            new LinqItem { ItemId = 300, MetricA = 3.0 },
                        ],
                    },
                ],
            },
            new LinqDataset
            {
                Groups =
                [
                    new LinqGroup
                    {
                        GroupId = 1,
                        Items =
                        [
                            new LinqItem { ItemId = 100, MetricA = 10.0 },
                            new LinqItem { ItemId = 400, MetricA = 40.0 },
                        ],
                    },
                ],
            },
        };

        CompareResult<LinqDataset> result = ParallelCompareApi.Compare(models);
        ParallelNode<LinqDataset> root = Assert.IsType<ParallelNode<LinqDataset>>(result.Root);

        IReadOnlyList<ParallelNode<LinqGroup>> groups = root.Children(model => model.Groups);
        ParallelNode<LinqItem>[] flattenedItems = groups
            .Where(group => group.AnyPresent)
            .OrderBy(group => group.KeyText, StringComparer.Ordinal)
            .SelectMany(group => group.Children(model => model.Items))
            .ToArray();

        Assert.Equal(["1", "2"], groups.Select(group => group.KeyText).ToArray());
        Assert.Equal(["100", "300", "400", "100", "200"], flattenedItems.Select(item => item.KeyText).ToArray());
        Assert.Contains(flattenedItems, item => item.KeyText == "400");
        Assert.True(flattenedItems.All(item => item.Count == 2));

        string firstKey = flattenedItems.First().KeyText!;
        string thirdKey = flattenedItems.Skip(2).First().KeyText!;
        string[] top3Descending = flattenedItems
            .Select(item => item.KeyText!)
            .OrderByDescending(key => key, StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        Assert.Equal("100", firstKey);
        Assert.Equal("400", thirdKey);
        Assert.Equal(["400", "300", "200"], top3Descending);
    }

    [Fact]
    public void Compare_GeneratedProjectionList_SupportsCoreLinqOperators()
    {
        // Intent: generated projection list が IEnumerable として主要 LINQ 演算を利用できる。
        LinqDataset[] models =
        {
            new LinqDataset
            {
                Groups =
                [
                    new LinqGroup
                    {
                        GroupId = 2,
                        Items =
                        [
                            new LinqItem { ItemId = 200, MetricA = 2.0 },
                            new LinqItem { ItemId = 100, MetricA = 1.0 },
                        ],
                    },
                    new LinqGroup
                    {
                        GroupId = 1,
                        Items =
                        [
                            new LinqItem { ItemId = 300, MetricA = 3.0 },
                        ],
                    },
                ],
            },
            new LinqDataset
            {
                Groups =
                [
                    new LinqGroup
                    {
                        GroupId = 1,
                        Items =
                        [
                            new LinqItem { ItemId = 100, MetricA = 10.0 },
                            new LinqItem { ItemId = 400, MetricA = 40.0 },
                        ],
                    },
                ],
            },
        };

        var root = ParallelCompareApi.Compare(models).AsGeneratedView();
        Assert.NotNull(root);

        int[] groupIds = root.Groups
            .Select(group => group.GroupId[0]!.Value)
            .ToArray();

        var flattenedItems = root.Groups
            .Where(group => group.NodeMeta.Count == 2)
            .OrderBy(group => group.GroupId[0] ?? int.MaxValue)
            .SelectMany(group => group.Items)
            .ToArray();

        int[] leftPresentItemIds = flattenedItems
            .Where(item => item.ItemId.GetState(0) == ValueState.PresentValue)
            .Select(item => item.ItemId[0]!.Value)
            .OrderBy(id => id)
            .Take(4)
            .ToArray();

        Assert.Equal([1, 2], groupIds);
        Assert.Equal([100, 300, 400, 100, 200], flattenedItems.Select(item => item.ItemId[0] ?? item.ItemId[1] ?? -1).ToArray());
        Assert.Contains(flattenedItems, item => item.ItemId[1] == 400);
        Assert.True(flattenedItems.All(item => item.NodeMeta.Count == 2));
        Assert.Equal([100, 200, 300], leftPresentItemIds);
    }
}

[GenerateParallelView]
public sealed class LinqDataset
{
    public List<LinqGroup> Groups { get; init; } = [];
}

public sealed class LinqGroup
{
    [CompareKey]
    public int GroupId { get; init; }

    public List<LinqItem> Items { get; init; } = [];
}

public sealed class LinqItem
{
    [CompareKey]
    public int ItemId { get; init; }

    public double MetricA { get; init; }
}
