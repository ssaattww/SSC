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

        AssertResolvablePath(entries, result, "Items[A\\]B].Label");
        AssertResolvablePath(entries, result, "Items[A\\\\B].Label");
        AssertResolvablePath(entries, result, "Items[\\#0].Label");
    }

    [Fact]
    public void GetDiffEntries_DoesNotReturnContainerPresenceForEmptyContainerMismatch()
    {
        // Intent: child node を持たない empty container presence mismatch は T-080 対象なので T-079 では列挙しない。
        var result = ParallelCompareApi.Compare(
        [
            new Dataset
            {
                Groups =
                [
                    new Group { GroupId = 1, Items = [] },
                ],
            },
            new Dataset
            {
                Groups =
                [
                    new Group { GroupId = 1, Items = [new Item { ItemId = 100, MetricA = 10.0 }] },
                ],
            },
        ]);

        var entries = result.GetDiffEntries();

        Assert.DoesNotContain(entries, entry => entry.Kind == ParallelDiffEntryKind.ContainerPresence);
        Assert.DoesNotContain(entries, entry => entry.Path == "Groups[1].Items");
    }

    private static void AssertResolvablePath<T>(
        IReadOnlyList<ParallelDiffEntry> entries,
        CompareResult<T> result,
        string path)
    {
        var entry = Assert.Single(entries, candidate => candidate.Path == path);
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
}
