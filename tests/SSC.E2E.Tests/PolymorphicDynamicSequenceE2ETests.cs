using SSC;

namespace SSC.E2E.Tests;

/// <summary>
/// Verifies dynamic projections for sequences whose declared element type differs from their runtime types.
/// </summary>
public sealed class PolymorphicDynamicSequenceE2ETests
{
    /// <summary>
    /// Verifies that a dynamic projection exposes runtime members for a polymorphic sequence and preserves their mismatch states.
    /// </summary>
    [Fact]
    public void Compare_DynamicProjection_RuntimeDerivedPolymorphicSequence_UsesRuntimeMembers()
    {
        var models = new[]
        {
            new DynamicRoot
            {
                Detail = new DynamicDetail
                {
                    Items =
                    [
                        new DynamicContent { Text = "left" },
                    ],
                },
            },
            new DynamicRoot
            {
                Detail = new DynamicDetail
                {
                    Items =
                    [
                        new DynamicContent { Text = "right" },
                    ],
                },
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;
        dynamic item = root.Detail.Items[0];

        Assert.Equal("left", (string?)item.Text[0]);
        Assert.Equal("right", (string?)item.Text[1]);
        Assert.Equal(ValueState.Mismatched, (ValueState)item.GetState(0));
        Assert.Equal(ValueState.Mismatched, (ValueState)item.GetState(1));
    }

    /// <summary>
    /// Represents a test root that exposes a polymorphic detail object.
    /// </summary>
    public sealed class DynamicRoot
    {
        /// <summary>
        /// Gets the polymorphic detail object projected dynamically by the test.
        /// </summary>
        public DynamicBase Detail { get; init; } = null!;
    }

    /// <summary>
    /// Defines the declared base type for the dynamic detail object.
    /// </summary>
    public abstract class DynamicBase;

    /// <summary>
    /// Represents the dynamic detail type that contains polymorphic items.
    /// </summary>
    public sealed class DynamicDetail : DynamicBase
    {
        /// <summary>
        /// Gets the polymorphic items exposed through the dynamic projection.
        /// </summary>
        public List<DynamicItem> Items { get; init; } = [];
    }

    /// <summary>
    /// Defines the declared base type for dynamic polymorphic items.
    /// </summary>
    public abstract class DynamicItem;

    /// <summary>
    /// Represents a dynamic polymorphic item with text content.
    /// </summary>
    public sealed class DynamicContent : DynamicItem
    {
        /// <summary>
        /// Gets the text exposed through the dynamic projection.
        /// </summary>
        public string Text { get; init; } = string.Empty;
    }
}
