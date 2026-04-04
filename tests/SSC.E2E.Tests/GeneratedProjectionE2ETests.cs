using SSC;
using SSC.Generated;

namespace SSC.E2E.Tests;

public sealed class GeneratedProjectionE2ETests
{
    [Fact]
    public void Compare_GeneratedProjection_AllowsContainerScalarAndDictionaryAccess()
    {
        // Intent: Source Generator 生成 view で list/dictionary と scalar value path を型付きで辿れる。
        var models = new[]
        {
            new GeneratedDataset
            {
                Groups =
                [
                    new GeneratedGroup
                    {
                        GroupId = 1,
                        Items =
                        [
                            new GeneratedItem
                            {
                                ItemId = 100,
                                MetricA = 1.0,
                                Detail = new GeneratedDetail { Label = "left-100" },
                            },
                            new GeneratedItem
                            {
                                ItemId = 200,
                                MetricA = 2.0,
                                Detail = new GeneratedDetail { Label = "left-200" },
                            },
                        ],
                    },
                ],
                Scores = new Dictionary<string, int>
                {
                    ["A"] = 10,
                    ["B"] = 11,
                },
            },
            new GeneratedDataset
            {
                Groups =
                [
                    new GeneratedGroup
                    {
                        GroupId = 1,
                        Items =
                        [
                            new GeneratedItem
                            {
                                ItemId = 100,
                                MetricA = 10.0,
                                Detail = new GeneratedDetail { Label = "right-100" },
                            },
                            new GeneratedItem
                            {
                                ItemId = 300,
                                MetricA = 30.0,
                                Detail = new GeneratedDetail { Label = "right-300" },
                            },
                        ],
                    },
                ],
                Scores = new Dictionary<string, int>
                {
                    ["A"] = 20,
                    ["C"] = 21,
                },
            },
        };

        var result = ParallelCompareApi.Compare(models);
        var root = result.Root!.AsGeneratedView();

        var leftMetricAt100 = root.Groups[0].Items[0].MetricA[0];
        var leftMetricAt200 = root.Groups[0].Items[1].MetricA[0];
        var rightMetricAt100 = root.Groups[0].Items[0].MetricA[1];
        var rightMetricAt200 = root.Groups[0].Items[1].MetricA[1];
        var rightStateAt200 = root.Groups[0].Items[1].MetricA.GetState(1);
        var leftDetailLabel = root.Groups[0].Items[0].Detail.Select(detail => detail.Label)[0];
        var rightMissingDetailState = root.Groups[0].Items[1].Detail.Select(detail => detail.Label).GetState(1);
        var firstItemKey = root.Groups[0].Items[0].NodeMeta.KeyText;

        Assert.Equal(1.0, leftMetricAt100);
        Assert.Equal(2.0, leftMetricAt200);
        Assert.Equal(10.0, rightMetricAt100);
        Assert.Null(rightMetricAt200);
        Assert.Equal(ValueState.Missing, rightStateAt200);
        Assert.Equal("left-100", leftDetailLabel);
        Assert.Equal(ValueState.Missing, rightMissingDetailState);
        Assert.Equal("100", firstItemKey);

        Assert.Equal(10, root.Scores[0][0]);
        Assert.Equal(20, root.Scores[0][1]);
    }

    [Fact]
    public void Compare_GeneratedProjection_ListIndexOutOfRange_ThrowsExecutionException()
    {
        // Intent: generated list index 範囲外アクセスは ModelIndexOutOfRange で失敗する。
        var models = new[]
        {
            new GeneratedDataset
            {
                Groups =
                [
                    new GeneratedGroup
                    {
                        GroupId = 1,
                        Items =
                        [
                            new GeneratedItem { ItemId = 100, MetricA = 1.0, Detail = new GeneratedDetail { Label = "l" } },
                        ],
                    },
                ],
            },
            new GeneratedDataset
            {
                Groups =
                [
                    new GeneratedGroup
                    {
                        GroupId = 1,
                        Items =
                        [
                            new GeneratedItem { ItemId = 100, MetricA = 10.0, Detail = new GeneratedDetail { Label = "r" } },
                        ],
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        var root = result.Root!.AsGeneratedView();

        var exception = Assert.Throws<CompareExecutionException>(() =>
        {
            var _ = root.Groups[99];
        });

        Assert.Equal(CompareIssueCode.ModelIndexOutOfRange, exception.Code);
    }

    [Fact]
    public void AsGeneratedView_WithNonCompareNode_ThrowsArgumentException()
    {
        // Intent: compare result node 以外は AsGeneratedView の対象外とする。
        Parallel<GeneratedDataset> node = new FakeGeneratedParallel();

        var exception = Assert.Throws<ArgumentException>(() => node.AsGeneratedView());

        Assert.Contains("compare result nodes", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FakeGeneratedParallel : Parallel<GeneratedDataset>
    {
        public GeneratedDataset? this[int modelIndex] => null;

        public int Count => 1;

        public bool AllPresent => false;

        public bool AnyPresent => false;

        public ValueState GetState(int modelIndex) => ValueState.Missing;
    }
}

[GenerateParallelView]
public sealed class GeneratedDataset
{
    public List<GeneratedGroup> Groups { get; init; } = [];

    public Dictionary<string, int> Scores { get; init; } = new(StringComparer.Ordinal);
}

public sealed class GeneratedGroup
{
    [CompareKey]
    public int GroupId { get; init; }

    public List<GeneratedItem> Items { get; init; } = [];
}

public sealed class GeneratedItem
{
    [CompareKey]
    public int ItemId { get; init; }

    public double MetricA { get; init; }

    public GeneratedDetail Detail { get; init; } = new();
}

public sealed class GeneratedDetail
{
    public string? Label { get; init; }
}
