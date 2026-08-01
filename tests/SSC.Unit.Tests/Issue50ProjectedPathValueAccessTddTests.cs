using SSC;

namespace SSC.Unit.Tests;

/// <summary>
/// Issue #50 の投影済み差分 path からの値参照 API を検証します。
/// </summary>
public sealed class Issue50ProjectedPathValueAccessTddTests
{
    /// <summary>
    /// 投影結果から model slot の値と状態を直接参照できることを確認します。
    /// </summary>
    [Fact]
    public void Projection_ProvidesDirectModelSlotValueAndStateAccess()
    {
        var result = ParallelCompareApi.Compare(
        [
            new SampleDocument { Value = "before" },
            new SampleDocument { Value = "after" },
        ]);
        var projector = new KeepStandardProjector();

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal(2, projection.Count);
        Assert.Equal("before", projection[0]);
        Assert.Equal("after", projection[1]);
        Assert.Equal(projection.Entry.Values[0].State, projection.GetState(0));
        Assert.Equal(projection.Entry.Values[1].State, projection.GetState(1));
    }

    private sealed class KeepStandardProjector : IParallelDiffPathProjector
    {
        public ParallelDiffPathSegmentProjection Project(ParallelDiffPathProjectionContext context)
        {
            return ParallelDiffPathSegmentProjection.KeepStandard();
        }
    }

    private sealed class SampleDocument
    {
        public string? Value { get; init; }
    }
}
