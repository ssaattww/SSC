using SSC;

namespace SSC.E2E.Tests;

public sealed class PolymorphicDynamicSequenceE2ETests
{
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

    public sealed class DynamicRoot
    {
        public DynamicBase Detail { get; init; } = null!;
    }

    public abstract class DynamicBase;

    public sealed class DynamicDetail : DynamicBase
    {
        public List<DynamicItem> Items { get; init; } = [];
    }

    public abstract class DynamicItem;

    public sealed class DynamicContent : DynamicItem
    {
        public string Text { get; init; } = string.Empty;
    }
}
