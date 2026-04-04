using SSC;

namespace SSC.E2E.Tests;

public sealed class ContainerAndSelectManyE2ETests
{
    [Fact]
    public void Compare_NormalizesDictionaryByUnionedKeys()
    {
        // Intent: Dictionary は全 model の key union で正規化し、欠損は Missing として表現する。
        var models = new[]
        {
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["A"] = 80,
                    ["B"] = 70,
                },
            },
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["A"] = 90,
                    ["C"] = 60,
                },
            },
        };

        var result = ParallelCompareApi.Compare(models);
        var root = Assert.IsType<ParallelNode<ScoreRoot>>(result.Root);
        var scores = root.Children(model => model.Scores);

        Assert.Equal(["A", "B", "C"], scores.Select(node => node.KeyText).ToArray());

        Assert.Equal(80, scores[0][0]);
        Assert.Equal(90, scores[0][1]);

        Assert.Equal(70, scores[1][0]);
        Assert.Equal(ValueState.Missing, scores[1].GetState(1));

        Assert.Equal(ValueState.Missing, scores[2].GetState(0));
        Assert.Equal(60, scores[2][1]);
    }

    [Fact]
    public void SelectMany_PreservesModelSlotsAfterFlatten()
    {
        // Intent: SelectMany 後も要素は model slot を維持し、欠損側は Missing で表現する。
        var models = new[]
        {
            new Dataset
            {
                Groups =
                [
                    new Group
                    {
                        GroupId = 1,
                        Items =
                        [
                            new Item { ItemId = 100, MetricA = 1.0, InternalMemo = "left-a" },
                            new Item { ItemId = 200, MetricA = 2.0, InternalMemo = "left-b" },
                        ],
                    },
                    new Group
                    {
                        GroupId = 2,
                        Items =
                        [
                            new Item { ItemId = 300, MetricA = 3.0, InternalMemo = "left-c" },
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
                            new Item { ItemId = 100, MetricA = 10.0, InternalMemo = "right-a" },
                            new Item { ItemId = 400, MetricA = 40.0, InternalMemo = "right-d" },
                        ],
                    },
                    new Group
                    {
                        GroupId = 3,
                        Items =
                        [
                            new Item { ItemId = 500, MetricA = 50.0, InternalMemo = "right-e" },
                        ],
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        var root = Assert.IsType<ParallelNode<Dataset>>(result.Root);
        var groups = root.Children(model => model.Groups);
        var items = groups.SelectMany(group => group.Children(model => model.Items)).ToList();

        Assert.Equal(["1", "2", "3"], groups.Select(group => group.KeyText).ToArray());
        Assert.Equal(5, items.Count);

        Assert.Equal("100", items[0].KeyText);
        Assert.Equal(100, items[0][0]?.ItemId);
        Assert.Equal(100, items[0][1]?.ItemId);

        Assert.Equal("200", items[1].KeyText);
        Assert.Equal(200, items[1][0]?.ItemId);
        Assert.Equal(ValueState.Missing, items[1].GetState(1));

        Assert.Equal("400", items[2].KeyText);
        Assert.Equal(ValueState.Missing, items[2].GetState(0));
        Assert.Equal(400, items[2][1]?.ItemId);

        Assert.Equal("300", items[3].KeyText);
        Assert.Equal(300, items[3][0]?.ItemId);
        Assert.Equal(ValueState.Missing, items[3].GetState(1));

        Assert.Equal("500", items[4].KeyText);
        Assert.Equal(ValueState.Missing, items[4].GetState(0));
        Assert.Equal(500, items[4][1]?.ItemId);
    }

    [Fact]
    public void Compare_DynamicProjection_AllowsListIndexThenModelIndexAccess()
    {
        // Intent: AsDynamic で root.Groups[0].Items[0].MetricA[0] 形式のアクセスを可能にする。
        var models = new[]
        {
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
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = Assert.IsType<ParallelNode<Dataset>>(result.Root).AsDynamic();

        double? leftMetricAt100 = root.Groups[0].Items[0].MetricA[0];
        double? leftMetricAt200 = root.Groups[0].Items[1].MetricA[0];
        double? rightMetricAt100 = root.Groups[0].Items[0].MetricA[1];
        double? rightMetricAt200 = root.Groups[0].Items[1].MetricA[1];
        var rightStateAt200 = (ValueState)root.Groups[0].Items[1].MetricA.GetState(1);

        Assert.Equal(1.0, leftMetricAt100);
        Assert.Equal(2.0, leftMetricAt200);
        Assert.Equal(10.0, rightMetricAt100);
        Assert.Null(rightMetricAt200);
        Assert.Equal(ValueState.Missing, rightStateAt200);
    }

    [Fact]
    public void Compare_IgnoresCompareIgnoreMemberInResultPath()
    {
        // Intent: [CompareIgnore] メンバーは比較対象に含めず、Issue の Path にも現れない。
        var models = new[]
        {
            new Dataset
            {
                Groups =
                [
                    new Group
                    {
                        GroupId = 1,
                        Items =
                        [
                            new Item { ItemId = 100, MetricA = 1.0, InternalMemo = "memo-a" },
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
                            new Item { ItemId = 100, MetricA = 2.0, InternalMemo = "memo-b" },
                        ],
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        Assert.DoesNotContain(result.Issues, issue => issue.Path.Contains(nameof(Item.InternalMemo), StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_StringDictionaryUsesOrdinalByDefault()
    {
        // Intent: 既定の StringComparison.Ordinal では "A" と "a" を別キーとして扱う。
        var models = new[]
        {
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["a"] = 10,
                },
            },
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["A"] = 20,
                },
            },
        };

        var result = ParallelCompareApi.Compare(models);
        var root = Assert.IsType<ParallelNode<ScoreRoot>>(result.Root);
        var scores = root.Children(model => model.Scores);

        Assert.Equal(["A", "a"], scores.Select(node => node.KeyText).ToArray());
        Assert.Equal(20, scores[0][1]);
        Assert.Equal(ValueState.Missing, scores[0].GetState(0));
        Assert.Equal(10, scores[1][0]);
        Assert.Equal(ValueState.Missing, scores[1].GetState(1));
    }

    [Fact]
    public void Compare_StringDictionaryRespectsOrdinalIgnoreCaseConfiguration()
    {
        // Intent: StringComparison.OrdinalIgnoreCase 指定時は同値キーを統合し、KeyText も規則的に正規化する。
        var models = new[]
        {
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["a"] = 10,
                },
            },
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["A"] = 20,
                },
            },
        };
        var configuration = new CompareConfiguration
        {
            StringKeyComparison = StringComparison.OrdinalIgnoreCase,
        };

        var result = ParallelCompareApi.Compare(models, configuration);
        var root = Assert.IsType<ParallelNode<ScoreRoot>>(result.Root);
        var scores = root.Children(model => model.Scores);

        var key = Assert.Single(scores);
        Assert.Equal("A", key.KeyText);
        Assert.Equal(10, key[0]);
        Assert.Equal(20, key[1]);
    }

    [Fact]
    public void Compare_StringDictionaryDuplicateInSameModel_WithOrdinalIgnoreCase_RecordsDuplicateIssue()
    {
        // Intent: OrdinalIgnoreCase 同値衝突の重複 Issue では KeyText を正規化済み表記で返す。
        var models = new[]
        {
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["a"] = 10,
                    ["A"] = 11,
                },
            },
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["base"] = 20,
                },
            },
        };
        var configuration = new CompareConfiguration
        {
            StringKeyComparison = StringComparison.OrdinalIgnoreCase,
        };

        var result = ParallelCompareApi.Compare(models, configuration);

        var issue = Assert.Single(result.Issues.Where(item => item.Code == CompareIssueCode.DuplicateCompareKeyDetected));
        Assert.Equal("ScoreRoot.Scores", issue.Path);
        Assert.Equal(0, issue.ModelIndex);
        Assert.Equal("A", issue.KeyText);
    }

    [Fact]
    public void Compare_StringDictionaryDuplicateInSameModel_WithOrdinalIgnoreCase_ThrowsInStrictMode()
    {
        // Intent: OrdinalIgnoreCase 同値衝突は strict=true で DuplicateCompareKeyDetected を送出する。
        var models = new[]
        {
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["a"] = 10,
                    ["A"] = 11,
                },
            },
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["base"] = 20,
                },
            },
        };
        var configuration = new CompareConfiguration
        {
            StrictMode = true,
            StringKeyComparison = StringComparison.OrdinalIgnoreCase,
        };

        var exception = Assert.Throws<CompareExecutionException>(() => ParallelCompareApi.Compare(models, configuration));
        Assert.Equal(CompareIssueCode.DuplicateCompareKeyDetected, exception.Code);
    }

    [Fact]
    public void Compare_WithThreeModels_BuildsUnionAndPreservesEachModelSlot()
    {
        // Intent: 3 model 入力でも key union 順を維持し、各 node の slot が model index と一致する。
        var models = new[]
        {
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["A"] = 10,
                    ["B"] = 11,
                },
            },
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["B"] = 20,
                    ["C"] = 21,
                },
            },
            new ScoreRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["A"] = 30,
                    ["C"] = 31,
                    ["D"] = 32,
                },
            },
        };

        var result = ParallelCompareApi.Compare(models);
        var root = Assert.IsType<ParallelNode<ScoreRoot>>(result.Root);
        var scores = root.Children(model => model.Scores);

        Assert.Equal(["A", "B", "C", "D"], scores.Select(node => node.KeyText).ToArray());

        Assert.Equal(10, scores[0][0]);
        Assert.Equal(ValueState.Missing, scores[0].GetState(1));
        Assert.Equal(30, scores[0][2]);

        Assert.Equal(11, scores[1][0]);
        Assert.Equal(20, scores[1][1]);
        Assert.Equal(ValueState.Missing, scores[1].GetState(2));

        Assert.Equal(ValueState.Missing, scores[2].GetState(0));
        Assert.Equal(21, scores[2][1]);
        Assert.Equal(31, scores[2][2]);

        Assert.Equal(ValueState.Missing, scores[3].GetState(0));
        Assert.Equal(ValueState.Missing, scores[3].GetState(1));
        Assert.Equal(32, scores[3][2]);
    }

    public sealed class ScoreRoot
    {
        public Dictionary<string, int> Scores { get; init; } = new(StringComparer.Ordinal);
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

        [CompareIgnore]
        public string? InternalMemo { get; init; }
    }
}
