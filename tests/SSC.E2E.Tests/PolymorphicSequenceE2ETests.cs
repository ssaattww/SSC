using SSC;

namespace SSC.E2E.Tests;

/// <summary>
/// Verifies comparison behavior for sequences whose declared element type differs from their runtime types.
/// </summary>
public sealed class PolymorphicSequenceE2ETests
{
    /// <summary>
    /// Verifies that aligned elements with the same derived type compare their runtime members while retaining the declared node type.
    /// </summary>
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

    /// <summary>
    /// Verifies that nested elements with matching derived types compare runtime members recursively.
    /// </summary>
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

    /// <summary>
    /// Verifies that aligned elements with different runtime types report an element mismatch without comparing child members.
    /// </summary>
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

    /// <summary>
    /// Verifies that null and missing polymorphic elements retain their established presence-state behavior.
    /// </summary>
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

    /// <summary>
    /// Verifies that keyed elements with matching derived types retain key alignment while comparing runtime members.
    /// </summary>
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

    /// <summary>
    /// Verifies that trace output identifies both the declared node type and the runtime comparison type.
    /// </summary>
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

    /// <summary>
    /// Represents a test root that exposes polymorphic sequence items.
    /// </summary>
    public sealed class PolymorphicRoot
    {
        /// <summary>
        /// Gets the polymorphic items compared by the sequence tests.
        /// </summary>
        public List<PolymorphicItem?> Items { get; init; } = [];
    }

    /// <summary>
    /// Defines the declared base type for polymorphic sequence items.
    /// </summary>
    public abstract class PolymorphicItem
    {
    }

    /// <summary>
    /// Represents a polymorphic item with a name and nested polymorphic children.
    /// </summary>
    public sealed class PolymorphicNode : PolymorphicItem
    {
        /// <summary>
        /// Gets the name compared for this polymorphic node.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the nested polymorphic items compared recursively.
        /// </summary>
        public List<PolymorphicItem?> Children { get; init; } = [];
    }

    /// <summary>
    /// Represents a polymorphic item with text content.
    /// </summary>
    public sealed class PolymorphicContent : PolymorphicItem
    {
        /// <summary>
        /// Gets the text compared for this polymorphic content item.
        /// </summary>
        public string Text { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents a test root that exposes keyed polymorphic sequence items.
    /// </summary>
    public sealed class KeyedPolymorphicRoot
    {
        /// <summary>
        /// Gets the keyed polymorphic items compared by the sequence tests.
        /// </summary>
        public List<KeyedPolymorphicItem> Items { get; init; } = [];
    }

    /// <summary>
    /// Defines the declared base type for keyed polymorphic sequence items.
    /// </summary>
    public abstract class KeyedPolymorphicItem
    {
        /// <summary>
        /// Gets the key used to align polymorphic items between compared models.
        /// </summary>
        [CompareKey]
        public int Id { get; init; }
    }

    /// <summary>
    /// Represents a keyed polymorphic item with a value compared after key alignment.
    /// </summary>
    public sealed class KeyedPolymorphicValue : KeyedPolymorphicItem
    {
        /// <summary>
        /// Gets the value compared for the keyed polymorphic item.
        /// </summary>
        public int Value { get; init; }
    }
}
