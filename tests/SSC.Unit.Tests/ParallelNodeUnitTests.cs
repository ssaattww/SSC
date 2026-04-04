using SSC;

namespace SSC.Unit.Tests;

public sealed class ParallelNodeUnitTests
{
    [Fact]
    public void Indexer_OutOfRange_ThrowsExecutionException()
    {
        // Intent: model slot の範囲外アクセスは必ず ModelIndexOutOfRange で失敗させる。
        var node = ParallelNode<string>.CreateLeaf(["a", "b"], [ValueState.PresentValue, ValueState.PresentValue], keyText: "k");

        var exception = Assert.Throws<CompareExecutionException>(() => _ = node[2]);

        Assert.Equal(CompareIssueCode.ModelIndexOutOfRange, exception.Code);
    }

    [Fact]
    public void PresenceFlags_FollowValueStates()
    {
        // Intent: AllPresent / AnyPresent / GetState が ValueState の定義通りに評価される。
        var node = ParallelNode<string>.CreateLeaf(["x", null, null], [ValueState.PresentValue, ValueState.PresentNull, ValueState.Missing], keyText: "k");

        Assert.False(node.AllPresent);
        Assert.True(node.AnyPresent);
        Assert.Equal(ValueState.PresentValue, node.GetState(0));
        Assert.Equal(ValueState.PresentNull, node.GetState(1));
        Assert.Equal(ValueState.Missing, node.GetState(2));
    }

    [Fact]
    public void CreateLeaf_LengthMismatch_ThrowsArgumentException()
    {
        // Intent: values/states 長が一致しない node は生成させない。
        var exception = Assert.Throws<ArgumentException>(
            () => ParallelNode<string>.CreateLeaf(["a", "b"], [ValueState.PresentValue], keyText: "k"));

        Assert.Contains("must match", exception.Message, StringComparison.Ordinal);
    }
}
