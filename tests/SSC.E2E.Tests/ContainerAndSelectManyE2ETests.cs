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
        dynamic root = result.AsDynamic()!;

        double? leftMetricAt100 = root.Groups[0].Items[0].MetricA[0];
        double? leftMetricAt200 = root.Groups[0].Items[1].MetricA[0];
        double? rightMetricAt100 = root.Groups[0].Items[0].MetricA[1];
        double? rightMetricAt200 = root.Groups[0].Items[1].MetricA[1];
        var leftItemAt100 = (Item?)root.Groups[0].Items[0][0];
        var rightItemAt200 = (Item?)root.Groups[0].Items[1][1];
        var rightStateAt200 = (ValueState)root.Groups[0].Items[1].MetricA.GetState(1);
        var rightNodeStateAt200 = (ValueState)root.Groups[0].Items[1].GetState(1);

        Assert.Equal(1.0, leftMetricAt100);
        Assert.Equal(2.0, leftMetricAt200);
        Assert.Equal(10.0, rightMetricAt100);
        Assert.Null(rightMetricAt200);
        Assert.Equal(100, leftItemAt100?.ItemId);
        Assert.Null(rightItemAt200);
        Assert.Equal(ValueState.Missing, rightStateAt200);
        Assert.Equal(ValueState.Missing, rightNodeStateAt200);
    }

    [Fact]
    public void Compare_DynamicProjection_ValuePathGetState_ReflectsMemberState()
    {
        // Intent: 値パスの GetState は親nodeではなく最終メンバー値の状態を返す。
        var models = new[]
        {
            new NullableDataset
            {
                Items =
                [
                    new NullableItem
                    {
                        ItemId = 100,
                        Detail = new NullableDetail
                        {
                            Label = null,
                        },
                    },
                ],
            },
            new NullableDataset
            {
                Items =
                [
                    new NullableItem
                    {
                        ItemId = 200,
                        Detail = new NullableDetail
                        {
                            Label = "present",
                        },
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;

        var leftLabel = (string?)root.Items[0].Detail.Label[0];
        var leftLabelState = (ValueState)root.Items[0].Detail.Label.GetState(0);
        var rightLabelState = (ValueState)root.Items[0].Detail.Label.GetState(1);
        var rightLabel = (string?)root.Items[1].Detail.Label[1];
        var rightPresentState = (ValueState)root.Items[1].Detail.Label.GetState(1);

        Assert.Null(leftLabel);
        Assert.Equal(ValueState.Mismatched, leftLabelState);
        Assert.Equal(ValueState.Missing, rightLabelState);
        Assert.Equal("present", rightLabel);
        Assert.Equal(ValueState.Mismatched, rightPresentState);
    }

    [Fact]
    public void Compare_DynamicProjection_NestedValuePathGetState_DoesNotInvokeGetterDuringCall()
    {
        // Intent: dynamic nested value path の GetState は state lookup 中に getter を再実行しない。
        var leftCounter = new GetterInvocationCounter();
        var rightCounter = new GetterInvocationCounter();
        var models = new[]
        {
            new SideEffectDataset
            {
                Items =
                [
                    new SideEffectItem
                    {
                        ItemId = 100,
                        Detail = new SideEffectDetail(leftCounter, "left"),
                    },
                ],
            },
            new SideEffectDataset
            {
                Items =
                [
                    new SideEffectItem
                    {
                        ItemId = 100,
                        Detail = new SideEffectDetail(rightCounter, "right"),
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;
        var invocationCountBeforeGetState = leftCounter.Count + rightCounter.Count;

        var state = (ValueState)root.Items[0].Detail.Label.GetState(0);

        Assert.Equal(ValueState.Mismatched, state);
        Assert.Equal(invocationCountBeforeGetState, leftCounter.Count + rightCounter.Count);
    }

    [Fact]
    public void Compare_DynamicProjection_NestedValuePathGetState_DoesNotRethrowGetterException()
    {
        // Intent: getter 例外は compare 側へ閉じ込め、GetState 呼び出しで再送出しない。
        var models = new[]
        {
            new ExceptionDataset
            {
                Items =
                [
                    new ExceptionItem
                    {
                        ItemId = 100,
                        Detail = new ExceptionDetail("boom-left"),
                    },
                ],
            },
            new ExceptionDataset
            {
                Items =
                [
                    new ExceptionItem
                    {
                        ItemId = 100,
                        Detail = new ExceptionDetail("boom-right"),
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;

        var exception = Record.Exception(() => _ = (ValueState)root.Items[0].Detail.Label.GetState(0));

        Assert.Null(exception);
    }

    [Fact]
    public void Compare_DynamicProjection_NestedValuePathIndexer_StillInvokesGetterOnAccess()
    {
        // Intent: value indexer 読み取りは T-042 の対象外であり、アクセス時に getter を評価する。
        var leftCounter = new GetterInvocationCounter();
        var rightCounter = new GetterInvocationCounter();
        var models = new[]
        {
            new SideEffectDataset
            {
                Items =
                [
                    new SideEffectItem
                    {
                        ItemId = 100,
                        Detail = new SideEffectDetail(leftCounter, "left"),
                    },
                ],
            },
            new SideEffectDataset
            {
                Items =
                [
                    new SideEffectItem
                    {
                        ItemId = 100,
                        Detail = new SideEffectDetail(rightCounter, "right"),
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;
        var invocationCountBeforeAccess = leftCounter.Count + rightCounter.Count;

        var value = (string?)root.Items[0].Detail.Label[0];

        Assert.Equal("left", value);
        Assert.True(leftCounter.Count + rightCounter.Count > invocationCountBeforeAccess);
    }

    [Fact]
    public void Compare_DynamicProjection_NestedValuePath_AllowsRuntimeDerivedMemberAccess()
    {
        // Intent: declared type に無い runtime member でも dynamic nested access は継続利用できる。
        var models = new[]
        {
            new DerivedDetailDataset
            {
                Items =
                [
                    new DerivedDetailItem
                    {
                        ItemId = 100,
                        Detail = new DerivedDetailLeaf { Label = "left" },
                    },
                ],
            },
            new DerivedDetailDataset
            {
                Items =
                [
                    new DerivedDetailItem
                    {
                        ItemId = 100,
                        Detail = new DerivedDetailLeaf { Label = "right" },
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;

        var state = (ValueState)root.Items[0].Detail.Label.GetState(0);

        Assert.Equal(ValueState.Mismatched, state);
    }

    [Fact]
    public void Compare_DynamicProjection_RuntimeDerivedContainerMember_AllowsForeachAndIndexAccess()
    {
        // Intent: declared type に無い runtime-derived container member でも dynamic list view として辿れる。
        var models = new[]
        {
            new DerivedDetailDataset
            {
                Items =
                [
                    new DerivedDetailItem
                    {
                        ItemId = 100,
                        Detail = new DerivedDetailWithChildren
                        {
                            Children =
                            [
                                new DerivedRuntimeChild { ChildId = 1, Label = "left-1" },
                                new DerivedRuntimeChild { ChildId = 2, Label = "left-2" },
                            ],
                        },
                    },
                ],
            },
            new DerivedDetailDataset
            {
                Items =
                [
                    new DerivedDetailItem
                    {
                        ItemId = 100,
                        Detail = new DerivedDetailWithChildren
                        {
                            Children =
                            [
                                new DerivedRuntimeChild { ChildId = 1, Label = "right-1" },
                                new DerivedRuntimeChild { ChildId = 2, Label = "right-2" },
                            ],
                        },
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;

        var children = ((IEnumerable<object?>)root.Items[0].Detail.Children).Cast<dynamic>().ToArray();
        dynamic first = root.Items[0].Detail.Children[0];

        Assert.Equal(2, children.Length);
        Assert.Equal(1, (int)first.ChildId[0]);
        Assert.Equal("left-1", (string?)children[0].Label[0]);
        Assert.Equal("right-2", (string?)children[1].Label[1]);
    }

    [Fact]
    public void Compare_DynamicProjection_RuntimeDerivedContainerMember_ThrowsWhenSequenceElementHasNoCompareKey()
    {
        // Intent: runtime-derived container の正規化前提を満たさない場合は silent に握り潰さず例外で可視化する。
        var models = new[]
        {
            new DerivedDetailDataset
            {
                Items =
                [
                    new DerivedDetailItem
                    {
                        ItemId = 100,
                        Detail = new DerivedDetailWithNonKeyedChildren
                        {
                            Children =
                            [
                                new DerivedRuntimeChildWithoutKey { Label = "left-1" },
                            ],
                        },
                    },
                ],
            },
            new DerivedDetailDataset
            {
                Items =
                [
                    new DerivedDetailItem
                    {
                        ItemId = 100,
                        Detail = new DerivedDetailWithNonKeyedChildren
                        {
                            Children =
                            [
                                new DerivedRuntimeChildWithoutKey { Label = "right-1" },
                            ],
                        },
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;

        var exception = Assert.Throws<CompareExecutionException>(
            () => _ = ((IEnumerable<object?>)root.Items[0].Detail.Children).Cast<dynamic>().ToArray());

        Assert.Equal(CompareIssueCode.CompareKeyNotFoundOnSequenceElement, exception.Code);
    }

    [Fact]
    public void Compare_DynamicProjection_PrefersModelMember_WhenNameCollidesWithNodeMeta()
    {
        // Intent: モデル側に Count/KeyText がある場合は、そちらの値アクセスを優先する。
        var models = new[]
        {
            new CollisionDataset
            {
                Items =
                [
                    new CollisionItem { ItemId = 100, Count = 10, KeyText = "left-key" },
                ],
            },
            new CollisionDataset
            {
                Items =
                [
                    new CollisionItem { ItemId = 100, Count = 20, KeyText = "right-key" },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;

        var leftCount = (int?)root.Items[0].Count[0];
        var rightCount = (int?)root.Items[0].Count[1];
        var leftKeyText = (string?)root.Items[0].KeyText[0];
        var rightKeyText = (string?)root.Items[0].KeyText[1];
        var nodeCount = (int)root.Items[0].NodeCount;
        var nodeKeyText = (string?)root.Items[0].NodeKeyText;

        Assert.Equal(10, leftCount);
        Assert.Equal(20, rightCount);
        Assert.Equal("left-key", leftKeyText);
        Assert.Equal("right-key", rightKeyText);
        Assert.Equal(2, nodeCount);
        Assert.Equal("100", nodeKeyText);
    }

    [Fact]
    public void Compare_DynamicProjection_ListIndexOutOfRange_ThrowsExecutionException()
    {
        // Intent: dynamic list index 範囲外は契約例外で失敗する。
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
                        ],
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;

        var exception = Assert.Throws<CompareExecutionException>(() =>
        {
            var _ = root.Groups[99];
        });
        var negativeException = Assert.Throws<CompareExecutionException>(() =>
        {
            var _ = root.Groups[-1];
        });

        Assert.Equal(CompareIssueCode.ModelIndexOutOfRange, exception.Code);
        Assert.Equal(CompareIssueCode.ModelIndexOutOfRange, negativeException.Code);
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

    public sealed class NullableDataset
    {
        public List<NullableItem> Items { get; init; } = [];
    }

    public sealed class NullableItem
    {
        [CompareKey]
        public int ItemId { get; init; }

        public NullableDetail? Detail { get; init; }
    }

    public sealed class NullableDetail
    {
        public string? Label { get; init; }
    }

    public sealed class SideEffectDataset
    {
        public List<SideEffectItem> Items { get; init; } = [];
    }

    public sealed class SideEffectItem
    {
        [CompareKey]
        public int ItemId { get; init; }

        public SideEffectDetail Detail { get; init; } = null!;
    }

    public sealed class SideEffectDetail(GetterInvocationCounter counter, string? label)
    {
        public string? Label => counter.Record(label);
    }

    public sealed class ExceptionDataset
    {
        public List<ExceptionItem> Items { get; init; } = [];
    }

    public sealed class ExceptionItem
    {
        [CompareKey]
        public int ItemId { get; init; }

        public ExceptionDetail Detail { get; init; } = null!;
    }

    public sealed class ExceptionDetail(string message)
    {
        public string? Label => throw new InvalidOperationException(message);
    }

    public sealed class GetterInvocationCounter
    {
        public int Count { get; private set; }

        public string? Record(string? value)
        {
            Count++;
            return value;
        }
    }

    public sealed class DerivedDetailDataset
    {
        public List<DerivedDetailItem> Items { get; init; } = [];
    }

    public sealed class DerivedDetailItem
    {
        [CompareKey]
        public int ItemId { get; init; }

        public DetailBase Detail { get; init; } = null!;
    }

    public abstract class DetailBase;

    public sealed class DerivedDetailLeaf : DetailBase
    {
        public string? Label { get; init; }
    }

    public sealed class DerivedDetailWithChildren : DetailBase
    {
        public List<DerivedRuntimeChild> Children { get; init; } = [];
    }

    public sealed class DerivedDetailWithNonKeyedChildren : DetailBase
    {
        public List<DerivedRuntimeChildWithoutKey> Children { get; init; } = [];
    }

    public sealed class DerivedRuntimeChild
    {
        [CompareKey]
        public int ChildId { get; init; }

        public string? Label { get; init; }
    }

    public sealed class DerivedRuntimeChildWithoutKey
    {
        public string? Label { get; init; }
    }

    public sealed class CollisionDataset
    {
        public List<CollisionItem> Items { get; init; } = [];
    }

    public sealed class CollisionItem
    {
        [CompareKey]
        public int ItemId { get; init; }

        public int Count { get; init; }

        public string? KeyText { get; init; }
    }
}
