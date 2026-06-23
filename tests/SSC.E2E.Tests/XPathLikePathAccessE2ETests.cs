using SSC;

namespace SSC.E2E.Tests;

public sealed class XPathLikePathAccessE2ETests
{
    [Fact]
    public void GetNodeByPath_WithKeyedPathAndRootPrefix_ResolvesNodeValueAndState()
    {
        // Intent: root type prefix 付き path で keyed container child と scalar member を解決する。
        var result = ParallelCompareApi.Compare(CreateModels());

        var node = result.GetNodeByPath("Dataset.Groups[1].Items[100].MetricA");

        Assert.NotNull(node);
        Assert.Equal(1.0, result.GetValueByPath("Dataset.Groups[1].Items[100].MetricA", 0));
        Assert.Equal(10.0, result.GetValueByPath("Dataset.Groups[1].Items[100].MetricA", 1));
        Assert.Equal(ValueState.Mismatched, result.GetStateByPath("Dataset.Groups[1].Items[100].MetricA", 0));
        Assert.Equal(ValueState.Mismatched, result.GetStateByPath("Dataset.Groups[1].Items[100].MetricA", 1));
    }

    [Fact]
    public void GetNodeByPath_WithKeyedPathWithoutRootPrefix_ResolvesSameNode()
    {
        // Intent: root type prefix なしの root-relative path でも同じ node を解決する。
        var result = ParallelCompareApi.Compare(CreateModels());

        var prefixed = result.GetNodeByPath("Dataset.Groups[1].Items[100].MetricA");
        var relative = result.GetNodeByPath("Groups[1].Items[100].MetricA");

        Assert.NotNull(relative);
        Assert.Same(prefixed, relative);
        Assert.Equal(1.0, result.GetValueByPath("Groups[1].Items[100].MetricA", 0));
        Assert.Equal(10.0, result.GetValueByPath("Groups[1].Items[100].MetricA", 1));
    }

    [Fact]
    public void GetPathAccess_WithUnresolvedPath_ReturnsNullAndMissing()
    {
        // Intent: 未解決 path は node/value を null、state を Missing として返す。
        var result = ParallelCompareApi.Compare(CreateModels());

        Assert.Null(result.GetNodeByPath("Groups[9].Items[100].MetricA"));
        Assert.Null(result.GetValueByPath("Groups[9].Items[100].MetricA", 0));
        Assert.Equal(ValueState.Missing, result.GetStateByPath("Groups[9].Items[100].MetricA", 0));
        Assert.Null(result.GetNodeByPath("WrongRoot.Groups[1].Items[100].MetricA"));
    }

    [Fact]
    public void GetPathAccess_WithOutOfRangeModelIndex_ThrowsExecutionException()
    {
        // Intent: node 解決後の model index 範囲外は既存 node と同じ契約例外で失敗する。
        var result = ParallelCompareApi.Compare(CreateModels());

        var valueException = Assert.Throws<CompareExecutionException>(
            () => result.GetValueByPath("Groups[1].Items[100].MetricA", 2));
        var stateException = Assert.Throws<CompareExecutionException>(
            () => result.GetStateByPath("Groups[1].Items[100].MetricA", -1));

        Assert.Equal(CompareIssueCode.ModelIndexOutOfRange, valueException.Code);
        Assert.Equal(CompareIssueCode.ModelIndexOutOfRange, stateException.Code);
    }

    private static IReadOnlyList<Dataset> CreateModels()
    {
        return
        [
            new Dataset
            {
                Groups =
                [
                    new Group
                    {
                        GroupId = 1,
                        Items =
                        [
                            new Item { ItemId = 100, MetricA = 1.0 },
                            new Item { ItemId = 200, MetricA = 2.0 },
                        ],
                    },
                ],
            },
            new Dataset
            {
                Groups =
                [
                    new Group
                    {
                        GroupId = 1,
                        Items =
                        [
                            new Item { ItemId = 100, MetricA = 10.0 },
                            new Item { ItemId = 300, MetricA = 30.0 },
                        ],
                    },
                ],
            },
        ];
    }

    public sealed class Dataset
    {
        public List<Group> Groups { get; init; } = [];
    }

    public sealed class Group
    {
        [CompareKey]
        public int GroupId { get; init; }

        public List<Item> Items { get; init; } = [];
    }

    public sealed class Item
    {
        [CompareKey]
        public int ItemId { get; init; }

        public double MetricA { get; init; }
    }
}
