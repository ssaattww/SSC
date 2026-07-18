using SSC;

namespace SSC.E2E.Tests;

public sealed class XPathLikeDiffEntriesE2ETests
{
    [Fact]
    public void GetDiffEntries_ReturnsStructuredNodeEntriesWithResolvablePaths()
    {
        // Intent: 通常 node 差分を構造化 entry として返し、生成 path が同じ node に解決できる。
        var result = ParallelCompareApi.Compare(CreateModels());

        var entries = result.GetDiffEntries();
        var metric = Assert.Single(entries, entry => entry.Path == "Groups[1].Items[100].MetricA");

        Assert.Equal(ParallelDiffEntryKind.Node, metric.Kind);
        Assert.Equal("Groups[1].Items[100]", metric.ParentPath);
        Assert.NotNull(metric.ParentPath);
        Assert.NotNull(metric.ParentNode);
        Assert.Same(metric.ParentNode, result.GetNodeByPath(metric.ParentPath));
        Assert.NotNull(metric.Node);
        Assert.Same(metric.Node, result.GetNodeByPath(metric.Path));
        Assert.Equal(2, metric.Values.Count);
        Assert.Equal(0, metric.Values[0].ModelIndex);
        Assert.Equal(1.0, metric.Values[0].Value);
        Assert.Equal(ValueState.Mismatched, metric.Values[0].State);
        Assert.Equal(1, metric.Values[1].ModelIndex);
        Assert.Equal(10.0, metric.Values[1].Value);
        Assert.Equal(ValueState.Mismatched, metric.Values[1].State);
        Assert.Equal("Groups[1].Items[100].MetricA: [0]=1(Mismatched), [1]=10(Mismatched)", metric.ToString());
    }

    [Fact]
    public void GetDiffEntries_ReturnsObjectPresenceMismatchWithoutDuplicatingChildren()
    {
        // Intent: container child object 自身の presence mismatch は child leaf を重複列挙しない。
        var result = ParallelCompareApi.Compare(CreateModels());

        var entries = result.GetDiffEntries();
        var missingItem = Assert.Single(entries, entry => entry.Path == "Groups[1].Items[200]");

        Assert.Equal(ParallelDiffEntryKind.Node, missingItem.Kind);
        Assert.Equal("Groups[1]", missingItem.ParentPath);
        Assert.NotNull(missingItem.ParentPath);
        Assert.NotNull(missingItem.ParentNode);
        Assert.Same(missingItem.ParentNode, result.GetNodeByPath(missingItem.ParentPath));
        Assert.NotNull(missingItem.Node);
        Assert.Same(missingItem.Node, result.GetNodeByPath(missingItem.Path));
        Assert.Equal(200, ((Item?)missingItem.Values[0].Value)?.ItemId);
        Assert.Equal(ValueState.Mismatched, missingItem.Values[0].State);
        Assert.Null(missingItem.Values[1].Value);
        Assert.Equal(ValueState.Missing, missingItem.Values[1].State);
        Assert.DoesNotContain(entries, entry => entry.Path.StartsWith("Groups[1].Items[200].", StringComparison.Ordinal));
    }

    [Fact]
    public void GetDiffEntries_EscapesKeyTextAndGeneratedPathResolves()
    {
        // Intent: path 生成時に key text の ], \\, 先頭 #digits を escape し、生成 path で解決できる。
        var result = ParallelCompareApi.Compare(
        [
            new EscapedKeyDataset
            {
                Items =
                [
                    new EscapedKeyItem { ItemId = "A]B", Label = "left-bracket" },
                    new EscapedKeyItem { ItemId = "A\\B", Label = "left-slash" },
                    new EscapedKeyItem { ItemId = "#0", Label = "left-hash" },
                ],
            },
            new EscapedKeyDataset
            {
                Items =
                [
                    new EscapedKeyItem { ItemId = "A]B", Label = "right-bracket" },
                    new EscapedKeyItem { ItemId = "A\\B", Label = "right-slash" },
                    new EscapedKeyItem { ItemId = "#0", Label = "right-hash" },
                ],
            },
        ]);

        var entries = result.GetDiffEntries();

        AssertResolvablePath(entries, result, "Items[A\\]B].Label", "Items[A\\]B]");
        AssertResolvablePath(entries, result, "Items[A\\\\B].Label", "Items[A\\\\B]");
        AssertResolvablePath(entries, result, "Items[\\#0].Label", "Items[\\#0]");
    }

    /// <summary>
    /// 空文字列の比較 key を持つ要素が標準 path と値を返し、そのlegacy pathのnode lookupを保証しないことを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntries_ReturnsEntryForEmptyCompareKey()
    {
        var result = ParallelCompareApi.Compare(
        [
            new EscapedKeyDataset
            {
                Items = [new EscapedKeyItem { ItemId = string.Empty, Label = "left" }],
            },
            new EscapedKeyDataset
            {
                Items = [new EscapedKeyItem { ItemId = string.Empty, Label = "right" }],
            },
        ]);

        var entry = Assert.Single(result.GetDiffEntries());

        Assert.Equal("Items[].Label", entry.Path);
        Assert.Equal("Items[]", entry.ParentPath);
        Assert.Null(result.GetNodeByPath(entry.Path));
        Assert.NotNull(entry.ParentPath);
        Assert.Null(result.GetNodeByPath(entry.ParentPath!));
        Assert.Equal("left", entry.Values[0].Value);
        Assert.Equal("right", entry.Values[1].Value);
    }

    [Fact]
    public void GetDiffEntries_ReturnsContainerPresenceForEmptyListMissingOnOneSide()
    {
        // Intent: child node を持たない empty list presence mismatch を ContainerPresence entry として返す。
        var result = ParallelCompareApi.Compare(
        [
            new OptionalContainerDataset
            {
                Items = [],
            },
            new OptionalContainerDataset
            {
                Items = null,
            },
        ]);

        var entries = result.GetDiffEntries();
        var entry = Assert.Single(entries, candidate => candidate.Path == "Items");

        Assert.Equal(ParallelDiffEntryKind.ContainerPresence, entry.Kind);
        Assert.Null(entry.ParentPath);
        Assert.Same(result.Root, entry.ParentNode);
        Assert.Null(entry.Node);
        Assert.Null(result.GetNodeByPath(entry.Path));
        Assert.Equal(2, entry.Values.Count);
        Assert.Equal(0, entry.Values[0].ModelIndex);
        Assert.Null(entry.Values[0].Value);
        Assert.Equal(ValueState.Mismatched, entry.Values[0].State);
        Assert.Equal(1, entry.Values[1].ModelIndex);
        Assert.Null(entry.Values[1].Value);
        Assert.Equal(ValueState.Mismatched, entry.Values[1].State);
        Assert.Equal("Items: [0]=null(Mismatched), [1]=null(Mismatched)", entry.ToString());
    }

    [Fact]
    public void GetDiffEntries_ReturnsContainerPresenceForEmptyDictionaryMissingOnOneSide()
    {
        // Intent: child node を持たない empty dictionary presence mismatch を ContainerPresence entry として返す。
        var result = ParallelCompareApi.Compare(
        [
            new OptionalDictionaryDataset
            {
                Scores = [],
            },
            new OptionalDictionaryDataset
            {
                Scores = null,
            },
        ]);

        var entries = result.GetDiffEntries();
        var entry = Assert.Single(entries, candidate => candidate.Path == "Scores");

        Assert.Equal(ParallelDiffEntryKind.ContainerPresence, entry.Kind);
        Assert.Null(entry.ParentPath);
        Assert.Same(result.Root, entry.ParentNode);
        Assert.Null(entry.Node);
        Assert.Null(result.GetNodeByPath(entry.Path));
        Assert.Equal(2, entry.Values.Count);
        Assert.Null(entry.Values[0].Value);
        Assert.Equal(ValueState.Mismatched, entry.Values[0].State);
        Assert.Null(entry.Values[1].Value);
        Assert.Equal(ValueState.Mismatched, entry.Values[1].State);
        Assert.Equal("Scores: [0]=null(Mismatched), [1]=null(Mismatched)", entry.ToString());
    }

    [Fact]
    public void GetDiffEntries_ReturnsNestedContainerPresenceWithResolvableParent()
    {
        // Intent: nested ContainerPresence entry でも ParentPath/ParentNode が所有 node を直接指す。
        var result = ParallelCompareApi.Compare(
        [
            new OptionalNestedContainerDataset
            {
                Groups =
                [
                    new OptionalNestedContainerGroup
                    {
                        GroupId = 1,
                        Items = [],
                    },
                ],
            },
            new OptionalNestedContainerDataset
            {
                Groups =
                [
                    new OptionalNestedContainerGroup
                    {
                        GroupId = 1,
                        Items = null,
                    },
                ],
            },
        ]);

        var entries = result.GetDiffEntries();
        var entry = Assert.Single(entries, candidate => candidate.Path == "Groups[1].Items");

        Assert.Equal(ParallelDiffEntryKind.ContainerPresence, entry.Kind);
        Assert.Equal("Groups[1]", entry.ParentPath);
        Assert.NotNull(entry.ParentPath);
        Assert.NotNull(entry.ParentNode);
        Assert.Same(entry.ParentNode, result.GetNodeByPath(entry.ParentPath));
        Assert.Null(entry.Node);
    }

    private static void AssertResolvablePath<T>(
        IReadOnlyList<ParallelDiffEntry> entries,
        CompareResult<T> result,
        string path,
        string parentPath)
    {
        var entry = Assert.Single(entries, candidate => candidate.Path == path);
        Assert.Equal(parentPath, entry.ParentPath);
        Assert.NotNull(entry.ParentNode);
        Assert.Same(entry.ParentNode, result.GetNodeByPath(parentPath));
        Assert.Same(entry.Node, result.GetNodeByPath(path));
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

    public sealed class EscapedKeyDataset
    {
        public List<EscapedKeyItem> Items { get; init; } = [];
    }

    public sealed class EscapedKeyItem
    {
        [CompareKey]
        public string ItemId { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;
    }

    public sealed class ScoreDataset
    {
        public List<ScoreGroup> Groups { get; init; } = [];
    }

    public sealed class ScoreGroup
    {
        [CompareKey]
        public int GroupId { get; init; }

        public Dictionary<string, int> Scores { get; init; } = [];
    }

    public sealed class OptionalContainerDataset
    {
        public List<Item>? Items { get; init; }
    }

    public sealed class OptionalDictionaryDataset
    {
        public Dictionary<string, int>? Scores { get; init; }
    }

    public sealed class OptionalNestedContainerDataset
    {
        public List<OptionalNestedContainerGroup> Groups { get; init; } = [];
    }

    public sealed class OptionalNestedContainerGroup
    {
        [CompareKey]
        public int GroupId { get; init; }

        public List<Item>? Items { get; init; }
    }
}
