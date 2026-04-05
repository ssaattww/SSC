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

        var root = ParallelCompareApi.Compare(models).AsGeneratedView()!;

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
    public void Compare_GeneratedProjection_GetState_IncludesMismatchStates()
    {
        // Intent: generated value path の GetState は Matched/Mismatched/Missing を返す。
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
                            new GeneratedItem { ItemId = 100, MetricA = 1.0, Detail = new GeneratedDetail { Label = "same" } },
                            new GeneratedItem { ItemId = 200, MetricA = 2.0, Detail = new GeneratedDetail { Label = null } },
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
                            new GeneratedItem { ItemId = 100, MetricA = 1.0, Detail = new GeneratedDetail { Label = "same" } },
                            new GeneratedItem { ItemId = 200, MetricA = 20.0, Detail = new GeneratedDetail { Label = "present" } },
                        ],
                    },
                ],
            },
        };

        var root = ParallelCompareApi.Compare(models).AsGeneratedView()!;

        var matchedMetricState = root.Groups[0].Items[0].MetricA.GetState(0);
        var mismatchedMetricState = root.Groups[0].Items[1].MetricA.GetState(0);
        var mismatchedNullState = root.Groups[0].Items[1].Detail.Select(detail => detail.Label).GetState(0);

        Assert.Equal(ValueState.Matched, matchedMetricState);
        Assert.Equal(ValueState.Mismatched, mismatchedMetricState);
        Assert.Equal(ValueState.Mismatched, mismatchedNullState);
    }

    [Fact]
    public void Compare_GeneratedProjection_ListSupportsLinqSelect()
    {
        // Intent: generated list は IEnumerable として LINQ Select を利用できる。
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
                            new GeneratedItem { ItemId = 100, MetricA = 1.0, Detail = new GeneratedDetail { Label = "l-100" } },
                            new GeneratedItem { ItemId = 200, MetricA = 2.0, Detail = new GeneratedDetail { Label = "l-200" } },
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
                            new GeneratedItem { ItemId = 100, MetricA = 10.0, Detail = new GeneratedDetail { Label = "r-100" } },
                            new GeneratedItem { ItemId = 200, MetricA = 20.0, Detail = new GeneratedDetail { Label = "r-200" } },
                        ],
                    },
                ],
            },
        };

        var root = ParallelCompareApi.Compare(models).AsGeneratedView()!;

        int[] groupIds = root.Groups.Select(group => group.GroupId[0]!.Value).ToArray();
        int[] itemIds = root.Groups[0].Items.Select(item => item.ItemId[0]!.Value).ToArray();

        Assert.Equal(new[] { 1 }, groupIds);
        Assert.Equal(new[] { 100, 200 }, itemIds);
    }

    [Fact]
    public void Compare_GeneratedProjection_ListCanBeSelectedByModelIndex()
    {
        // Intent: generated list から model 単位の list を選択し、要素 index でアクセスできる。
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
                            new GeneratedItem { ItemId = 100, MetricA = 1.0, Detail = new GeneratedDetail { Label = "l-100" } },
                        ],
                    },
                    new GeneratedGroup
                    {
                        GroupId = 2,
                        Items =
                        [
                            new GeneratedItem { ItemId = 200, MetricA = 2.0, Detail = new GeneratedDetail { Label = "l-200" } },
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
                        GroupId = 2,
                        Items =
                        [
                            new GeneratedItem { ItemId = 220, MetricA = 22.0, Detail = new GeneratedDetail { Label = "r-220" } },
                        ],
                    },
                    new GeneratedGroup
                    {
                        GroupId = 3,
                        Items =
                        [
                            new GeneratedItem { ItemId = 300, MetricA = 30.0, Detail = new GeneratedDetail { Label = "r-300" } },
                        ],
                    },
                ],
            },
        };

        var root = ParallelCompareApi.Compare(models).AsGeneratedView()!;

        var leftGroups = root.Groups.SelectModel(0);
        var rightGroups = root.Groups.SelectModel(1);
        int[] leftGroupIds = leftGroups.Select(group => group.GroupId[0]!.Value).ToArray();
        int[] rightGroupIds = rightGroups.Select(group => group.GroupId[1]!.Value).ToArray();
        int[] leftItemIds = leftGroups
            .SelectMany(group => group.Items.SelectModel(0))
            .Select(item => item.ItemId[0]!.Value)
            .ToArray();
        int[] rightItemIds = rightGroups
            .SelectMany(group => group.Items.SelectModel(1))
            .Select(item => item.ItemId[1]!.Value)
            .ToArray();

        Assert.Equal(new[] { 1, 2 }, leftGroupIds);
        Assert.Equal(new[] { 2, 3 }, rightGroupIds);
        Assert.Equal(new[] { 100, 200 }, leftItemIds);
        Assert.Equal(new[] { 220, 300 }, rightItemIds);
        Assert.Equal(1, leftGroups[0].GroupId[0]);
        Assert.Equal(2, rightGroups[0].GroupId[1]);

        var listException = Assert.Throws<CompareExecutionException>(() =>
        {
            var _ = rightGroups[99];
        });
        var modelException = Assert.Throws<CompareExecutionException>(() => root.Groups.SelectModel(2));

        Assert.Equal(CompareIssueCode.ModelIndexOutOfRange, listException.Code);
        Assert.Equal(CompareIssueCode.ModelIndexOutOfRange, modelException.Code);
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

        var root = ParallelCompareApi.Compare(models).AsGeneratedView()!;

        var exception = Assert.Throws<CompareExecutionException>(() =>
        {
            var _ = root.Groups[99];
        });

        Assert.Equal(CompareIssueCode.ModelIndexOutOfRange, exception.Code);
    }

    [Fact]
    public void Compare_GeneratedProjection_WhenRootMissing_ResultExtensionReturnsNull()
    {
        // Intent: CompareResult 入口でも generated 投影を選択でき、Root 未生成時は null を返す。
        var result = ParallelCompareApi.Compare(Array.Empty<GeneratedDataset>());

        var root = result.AsGeneratedView();

        Assert.True(result.HasError);
        Assert.Null(root);
    }

    [Fact]
    public void AsGeneratedView_WithNonCompareNode_ThrowsArgumentException()
    {
        // Intent: compare result node 以外は AsGeneratedView の対象外とする。
        var result = new CompareResult<GeneratedDataset> { Root = new FakeGeneratedParallel() };

        var exception = Assert.Throws<ArgumentException>(() => result.AsGeneratedView());

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
