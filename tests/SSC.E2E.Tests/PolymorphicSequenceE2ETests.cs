using SSC;

namespace SSC.E2E.Tests;

public sealed class PolymorphicSequenceE2ETests
{
    [Fact]
    public void Compare_WhenAlignedElementsShareDerivedType_UsesRuntimeMembersAndPreservesDeclaredNodeType()
    {
        var models = new[]
        {
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "left" },
                ],
            },
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "right" },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        Assert.False(result.HasError);
        var root = Assert.IsType<ParallelNode<PolymorphicRoot>>(result.Root);
        var item = Assert.Single(root.GetChildren<PolymorphicItem>(nameof(PolymorphicRoot.Items)));
        Assert.IsType<PolymorphicNode>(item[0]);
        Assert.IsType<PolymorphicNode>(item[1]);
        Assert.Equal(ValueState.Mismatched, item.GetState(0));
        Assert.Equal(ValueState.Mismatched, item.GetState(1));

        var entry = Assert.Single(result.GetDiffEntries());
        Assert.Equal("Items[0].Name", entry.Path);
    }

    [Fact]
    public void Compare_WhenNestedElementsShareDerivedTypes_UsesRuntimeMembersRecursively()
    {
        var models = new[]
        {
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode
                    {
                        Name = "node",
                        Children =
                        [
                            new PolymorphicContent { Text = "left" },
                        ],
                    },
                ],
            },
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode
                    {
                        Name = "node",
                        Children =
                        [
                            new PolymorphicContent { Text = "right" },
                        ],
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        var entry = Assert.Single(result.GetDiffEntries());
        Assert.Equal("Items[0].Children[0].Text", entry.Path);
    }

    [Fact]
    public void Compare_WhenAlignedElementsHaveDifferentRuntimeTypes_ReportsElementDifferenceWithoutDescending()
    {
        var models = new[]
        {
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "same" },
                ],
            },
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicContent { Text = "same" },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        Assert.False(result.HasError);
        var root = Assert.IsType<ParallelNode<PolymorphicRoot>>(result.Root);
        var item = Assert.Single(root.GetChildren<PolymorphicItem>(nameof(PolymorphicRoot.Items)));
        Assert.Empty(item.GetDirectChildren());
        Assert.Equal(ValueState.Mismatched, item.GetState(0));
        Assert.Equal(ValueState.Mismatched, item.GetState(1));

        var entry = Assert.Single(result.GetDiffEntries());
        Assert.Equal("Items[0]", entry.Path);
        Assert.IsType<PolymorphicNode>(entry.Values[0].Value);
        Assert.IsType<PolymorphicContent>(entry.Values[1].Value);
        Assert.All(entry.Values, value => Assert.Equal(ValueState.Mismatched, value.State));
    }

    [Fact]
    public void Compare_WhenPolymorphicElementIsNullOrMissing_PreservesExistingPresenceStates()
    {
        var valueAndNull = ParallelCompareApi.Compare(
        [
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "node" },
                ],
            },
            new PolymorphicRoot
            {
                Items =
                [
                    null,
                ],
            },
        ]);

        var nullEntry = Assert.Single(valueAndNull.GetDiffEntries());
        Assert.Equal("Items[0]", nullEntry.Path);
        Assert.Equal(ValueState.Mismatched, nullEntry.Values[0].State);
        Assert.Equal(ValueState.Mismatched, nullEntry.Values[1].State);

        var valueAndMissing = ParallelCompareApi.Compare(
        [
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "node" },
                ],
            },
            new PolymorphicRoot(),
        ]);

        var missingEntry = Assert.Single(valueAndMissing.GetDiffEntries());
        Assert.Equal("Items[0]", missingEntry.Path);
        Assert.Equal(ValueState.Mismatched, missingEntry.Values[0].State);
        Assert.Equal(ValueState.Missing, missingEntry.Values[1].State);
    }

    [Fact]
    public void Compare_WhenKeyedElementsShareDerivedType_PreservesKeyAlignmentAndUsesRuntimeMembers()
    {
        var models = new[]
        {
            new KeyedPolymorphicRoot
            {
                Items =
                [
                    new KeyedPolymorphicValue { Id = 1, Value = 10 },
                ],
            },
            new KeyedPolymorphicRoot
            {
                Items =
                [
                    new KeyedPolymorphicValue { Id = 1, Value = 20 },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        var root = Assert.IsType<ParallelNode<KeyedPolymorphicRoot>>(result.Root);
        var item = Assert.Single(root.GetChildren<KeyedPolymorphicItem>(nameof(KeyedPolymorphicRoot.Items)));
        Assert.Equal("1", item.KeyText);

        var entry = Assert.Single(result.GetDiffEntries());
        Assert.Equal("Items[1].Value", entry.Path);
    }

    [Fact]
    public void Compare_WhenTraceEnabled_ReportsDeclaredNodeAndRuntimeComparisonTypes()
    {
        var logs = new List<string>();
        var configuration = new CompareConfiguration { TraceLog = logs.Add };
        var models = new[]
        {
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "left" },
                ],
            },
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "right" },
                ],
            },
        };

        _ = ParallelCompareApi.Compare(models, configuration);

        Assert.Contains(logs, line =>
            line.Contains("nodeType=SSC.E2E.Tests.PolymorphicSequenceE2ETests+PolymorphicItem", StringComparison.Ordinal)
            && line.Contains("comparisonType=SSC.E2E.Tests.PolymorphicSequenceE2ETests+PolymorphicNode", StringComparison.Ordinal));
    }

    public sealed class PolymorphicRoot
    {
        public List<PolymorphicItem?> Items { get; init; } = [];
    }

    public abstract class PolymorphicItem
    {
    }

    public sealed class PolymorphicNode : PolymorphicItem
    {
        public string Name { get; init; } = string.Empty;

        public List<PolymorphicItem?> Children { get; init; } = [];
    }

    public sealed class PolymorphicContent : PolymorphicItem
    {
        public string Text { get; init; } = string.Empty;
    }

    public sealed class KeyedPolymorphicRoot
    {
        public List<KeyedPolymorphicItem> Items { get; init; } = [];
    }

    public abstract class KeyedPolymorphicItem
    {
        [CompareKey]
        public int Id { get; init; }
    }

    public sealed class KeyedPolymorphicValue : KeyedPolymorphicItem
    {
        public int Value { get; init; }
    }
}
