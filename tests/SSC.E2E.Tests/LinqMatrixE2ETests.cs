using SSC;
using SSC.Generated;

namespace SSC.E2E.Tests;

public sealed class LinqMatrixE2ETests
{
    [Fact]
    public void Compare_NodeProjection_SupportsLinqOperatorMatrix()
    {
        ParallelNode<LinqDataset> root = BuildNodeRoot();
        IReadOnlyList<ParallelNode<LinqGroup>> groups = root.Children(model => model.Groups);
        ParallelNode<LinqItem>[] flattenedItems = groups.SelectMany(group => group.Children(model => model.Items)).ToArray();

        Assert.Equal(["1", "2"], groups.Select(group => group.KeyText).ToArray()); // ToArray + Select
        Assert.Equal(["2"], groups.Where(group => group.KeyText == "2").Select(group => group.KeyText).ToArray()); // Where + Select
        Assert.Equal(["2", "1"], groups.OrderByDescending(group => group.KeyText, StringComparer.Ordinal).Select(group => group.KeyText).ToArray()); // OrderByDescending
        Assert.Equal(["1"], groups.OrderBy(group => group.KeyText, StringComparer.Ordinal).Skip(0).Take(1).Select(group => group.KeyText).ToArray()); // OrderBy + Skip + Take
        Assert.Equal("1", groups.OrderBy(group => group.KeyText, StringComparer.Ordinal).First().KeyText); // First
        bool hasGroup1 = groups.Any(group => group.KeyText == "1"); // Any
        Assert.True(hasGroup1);
        Assert.True(groups.All(group => group.Count == 2)); // All
        Assert.Equal(["100", "300", "400", "100", "200"], flattenedItems.Select(item => item.KeyText).ToArray()); // SelectMany
    }

    [Fact]
    public void Compare_GeneratedProjection_SupportsLinqOperatorMatrix()
    {
        var root = ParallelCompareApi.Compare(BuildModels()).AsGeneratedView()!;
        var groups = root.Groups;
        var flattenedItems = groups.SelectMany(group => group.Items).ToArray();

        Assert.Equal([1, 2], groups.Select(group => group.GroupId[0]!.Value).ToArray()); // ToArray + Select
        Assert.Equal([2], groups.Where(group => group.GroupId[0] == 2).Select(group => group.GroupId[0]!.Value).ToArray()); // Where + Select
        Assert.Equal([2, 1], groups.OrderByDescending(group => group.GroupId[0] ?? int.MinValue).Select(group => group.GroupId[0]!.Value).ToArray()); // OrderByDescending
        Assert.Equal([1], groups.OrderBy(group => group.GroupId[0] ?? int.MaxValue).Skip(0).Take(1).Select(group => group.GroupId[0]!.Value).ToArray()); // OrderBy + Skip + Take
        Assert.Equal(1, groups.OrderBy(group => group.GroupId[0] ?? int.MaxValue).First().GroupId[0]!.Value); // First
        bool hasGroup1 = groups.Any(group => group.GroupId[0] == 1); // Any
        Assert.True(hasGroup1);
        Assert.True(groups.All(group => group.NodeMeta.Count == 2)); // All
        Assert.Equal([100, 300, 400, 100, 200], flattenedItems.Select(item => item.ItemId[0] ?? item.ItemId[1] ?? -1).ToArray()); // SelectMany
    }

    [Fact]
    public void Compare_DynamicProjection_SupportsLinqOperatorMatrix()
    {
        dynamic root = BuildDynamicRoot();
        IEnumerable<object?> groups = (IEnumerable<object?>)root.Groups;
        object?[] flattenedItems = groups.SelectMany(group => (IEnumerable<object?>)((dynamic)group!).Items).ToArray();

        Assert.Equal([1, 2], groups.Select(group => (int?)((dynamic)group!).GroupId[0] ?? -1).ToArray()); // ToArray + Select
        Assert.Equal([2], groups.Where(group => (int?)((dynamic)group!).GroupId[0] == 2).Select(group => (int?)((dynamic)group!).GroupId[0] ?? -1).ToArray()); // Where + Select
        Assert.Equal([2, 1], groups.OrderByDescending(group => (int?)((dynamic)group!).GroupId[0] ?? int.MinValue).Select(group => (int?)((dynamic)group!).GroupId[0] ?? -1).ToArray()); // OrderByDescending
        Assert.Equal([1], groups.OrderBy(group => (int?)((dynamic)group!).GroupId[0] ?? int.MaxValue).Skip(0).Take(1).Select(group => (int?)((dynamic)group!).GroupId[0] ?? -1).ToArray()); // OrderBy + Skip + Take
        Assert.Equal(1, (int?)((dynamic)groups.OrderBy(group => (int?)((dynamic)group!).GroupId[0] ?? int.MaxValue).First()!).GroupId[0]); // First
        bool hasGroup1 = groups.Any(group => (int?)((dynamic)group!).GroupId[0] == 1); // Any
        Assert.True(hasGroup1);
        Assert.True(groups.All(group => (int)((dynamic)group!).NodeCount == 2)); // All
        Assert.Equal([100, 300, 400, 100, 200], flattenedItems.Select(item => (int?)((dynamic)item!).ItemId[0] ?? (int?)((dynamic)item!).ItemId[1] ?? -1).ToArray()); // SelectMany
    }

    private static ParallelNode<LinqDataset> BuildNodeRoot()
    {
        CompareResult<LinqDataset> result = ParallelCompareApi.Compare(BuildModels());
        return Assert.IsType<ParallelNode<LinqDataset>>(result.Root);
    }

    private static dynamic BuildDynamicRoot()
    {
        CompareResult<LinqDataset> result = ParallelCompareApi.Compare(BuildModels());
        return result.AsDynamic()!;
    }

    private static LinqDataset[] BuildModels()
    {
        return
        [
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
        ];
    }
}
