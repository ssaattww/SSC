using System.Globalization;
using SSC;

namespace SSC.Unit.Tests;

public sealed class ParallelDiffResultUnitTests
{
    [Fact]
    public void ParallelDiffValue_ToString_FormatsValueByContract()
    {
        // Intent: Missing/null/string/数値を人間確認用の固定形式で表示する。
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            Assert.Equal("[0]=<missing>(Missing)", new ParallelDiffValue { ModelIndex = 0, State = ValueState.Missing, Value = "ignored" }.ToString());
            Assert.Equal("[1]=null(Matched)", new ParallelDiffValue { ModelIndex = 1, State = ValueState.Matched, Value = null }.ToString());
            Assert.Equal("[2]=\"left\"(Mismatched)", new ParallelDiffValue { ModelIndex = 2, State = ValueState.Mismatched, Value = "left" }.ToString());
            Assert.Equal("[3]=1.5(Mismatched)", new ParallelDiffValue { ModelIndex = 3, State = ValueState.Mismatched, Value = 1.5m }.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ParallelDiffEntry_ToString_JoinsPathAndValues()
    {
        // Intent: path と model slot 別 value/state を 1 行にまとめる。
        var entry = new ParallelDiffEntry
        {
            Path = "Groups[1].Items[200].Name",
            Kind = ParallelDiffEntryKind.Node,
            Values =
            [
                new ParallelDiffValue { ModelIndex = 0, Value = "left", State = ValueState.Mismatched },
                new ParallelDiffValue { ModelIndex = 1, Value = null, State = ValueState.Missing },
            ],
        };

        Assert.Equal(
            "Groups[1].Items[200].Name: [0]=\"left\"(Mismatched), [1]=<missing>(Missing)",
            entry.ToString());
    }
}
