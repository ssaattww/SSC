using SSC;

namespace SSC.Unit.Tests;

public sealed class ParallelNodeUnitTests
{
    [Fact]
    public void Indexer_OutOfRange_ThrowsExecutionException()
    {
        // Intent: model slot の範囲外アクセスは必ず ModelIndexOutOfRange で失敗させる。
        var node = ParallelNode<string>.CreateLeaf(["a", "b"], [ValueState.Matched, ValueState.Matched], keyText: "k");

        var exception = Assert.Throws<CompareExecutionException>(() => _ = node[2]);

        Assert.Equal(CompareIssueCode.ModelIndexOutOfRange, exception.Code);
    }

    [Fact]
    public void PresenceFlags_FollowValueStates()
    {
        // Intent: AllPresent / AnyPresent / GetState が ValueState の定義通りに評価される。
        var node = ParallelNode<string>.CreateLeaf(["x", null, null], [ValueState.Matched, ValueState.Matched, ValueState.Missing], keyText: "k");

        Assert.False(node.AllPresent);
        Assert.True(node.AnyPresent);
        Assert.Equal(ValueState.Mismatched, node.GetState(0));
        Assert.Equal(ValueState.Mismatched, node.GetState(1));
        Assert.Equal(ValueState.Missing, node.GetState(2));
    }

    [Fact]
    public void GetState_WhenAllValuesMatch_ReturnsMatched()
    {
        // Intent: 比較対象があり、値が一致する場合は Matched を返す。
        var valueNode = ParallelNode<int>.CreateLeaf([10, 10], [ValueState.Matched, ValueState.Matched], keyText: "k");
        var nullNode = ParallelNode<string>.CreateLeaf([null, null], [ValueState.Matched, ValueState.Matched], keyText: "k");

        Assert.Equal(ValueState.Matched, valueNode.GetState(0));
        Assert.Equal(ValueState.Matched, valueNode.GetState(1));
        Assert.Equal(ValueState.Matched, nullNode.GetState(0));
        Assert.Equal(ValueState.Matched, nullNode.GetState(1));
    }

    [Fact]
    public void GetState_WhenNoComparisonTarget_ReturnsMissing()
    {
        // Intent: 自身以外に比較対象が存在しない場合は Missing を返す。
        var node = ParallelNode<string>.CreateLeaf([null, null], [ValueState.Missing, ValueState.Missing], keyText: "k");

        Assert.Equal(ValueState.Missing, node.GetState(0));
        Assert.Equal(ValueState.Missing, node.GetState(1));
    }

    [Fact]
    public void CreateLeaf_LengthMismatch_ThrowsArgumentException()
    {
        // Intent: values/states 長が一致しない node は生成させない。
        var exception = Assert.Throws<ArgumentException>(
            () => ParallelNode<string>.CreateLeaf(["a", "b"], [ValueState.Matched], keyText: "k"));

        Assert.Contains("must match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AsDynamic_WithNonCompareNode_ThrowsArgumentException()
    {
        // Intent: compare result node 以外は AsDynamic の対象外とする。
        var result = new CompareResult<string> { Root = new FakeParallel() };

        var exception = Assert.Throws<ArgumentException>(() => result.AsDynamic());

        Assert.Contains("compare result nodes", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FakeParallel : Parallel<string>
    {
        public string? this[int modelIndex] => null;

        public int Count => 1;

        public bool AllPresent => false;

        public bool AnyPresent => false;

        public ValueState GetState(int modelIndex) => ValueState.Missing;
    }
}
