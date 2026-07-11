using SSC;

namespace SSC.Unit.Tests;

public sealed class ParallelDiffPathPatternUnitTests
{
    [Fact]
    public void Parse_WithExactAndWildcardSelectors_MatchesExpectedPath()
    {
        // Intent: 変動する board/file/child selector を [*] で吸収し、固定構造と属性名は厳密に比較する。
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse(
            "Boards[*].Files[*].Document.Root.Children[*].Children[*].Attribute[LastEditingTime].Value");

        bool matched = pattern.IsMatch(
            "Boards[Board-A].Files[No1/ygx].Document.Root.Children[0].Children[#2].Attribute[LastEditingTime].Value");

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_WithWildcardSelector_MatchesKeyAndOrdinalSelectors()
    {
        // Intent: [*] は key text と #ordinal のどちらにも一致する。
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Items[*].Value");

        Assert.True(pattern.IsMatch("Items[A].Value"));
        Assert.True(pattern.IsMatch("Items[0].Value"));
        Assert.True(pattern.IsMatch("Items[#0].Value"));
    }

    [Fact]
    public void IsMatch_WithExactSelectors_DistinguishesKeyFromOrdinal()
    {
        // Intent: [0] は key、[#0] は ordinal として既存 path grammar の意味を維持する。
        ParallelDiffPathPattern keyPattern = ParallelDiffPathPattern.Parse("Items[0].Value");
        ParallelDiffPathPattern ordinalPattern = ParallelDiffPathPattern.Parse("Items[#0].Value");

        Assert.True(keyPattern.IsMatch("Items[0].Value"));
        Assert.False(keyPattern.IsMatch("Items[#0].Value"));
        Assert.True(ordinalPattern.IsMatch("Items[#0].Value"));
        Assert.False(ordinalPattern.IsMatch("Items[0].Value"));
    }

    [Fact]
    public void IsMatch_WithDifferentMemberOrSegmentCount_ReturnsFalse()
    {
        // Intent: wildcard は selector だけに作用し、member 名や path 深度は曖昧化しない。
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse(
            "Boards[*].Attribute[LastEditingTime].Value");

        Assert.False(pattern.IsMatch("Boards[A].Attribute[CreatedAt].Value"));
        Assert.False(pattern.IsMatch("Boards[A].Metadata.Attribute[LastEditingTime].Value"));
        Assert.False(pattern.IsMatch("Boards.Attribute[LastEditingTime].Value"));
    }

    [Fact]
    public void IsMatch_WithEscapedKeyAndDotInsideSelector_PreservesExistingGrammar()
    {
        // Intent: bracket 内の dot と既存 escape を通常 path と同じ意味で扱う。
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse(
            "Items[A.B].Attributes[A\\]B].Value");

        Assert.True(pattern.IsMatch("Items[A.B].Attributes[A\\]B].Value"));
        Assert.False(pattern.IsMatch("Items[A].Attributes[A\\]B].Value"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".Items[*]")]
    [InlineData("Items[*].")]
    [InlineData("Items[]")]
    [InlineData("Items[A")]
    [InlineData("Items[A][B]")]
    [InlineData("Items[A\\x]")]
    public void TryParse_WithInvalidPattern_ReturnsFalse(string patternText)
    {
        // Intent: 解釈不能な pattern は例外を漏らさず TryParse=false とする。
        bool parsed = ParallelDiffPathPattern.TryParse(patternText, out ParallelDiffPathPattern? pattern);

        Assert.False(parsed);
        Assert.Null(pattern);
    }

    [Fact]
    public void Parse_WithInvalidPattern_ThrowsFormatException()
    {
        // Intent: Parse は不正構文を明示的な FormatException として通知する。
        Assert.Throws<FormatException>(() => ParallelDiffPathPattern.Parse("Items[]"));
    }

    [Fact]
    public void IsMatch_WithInvalidCandidatePath_ReturnsFalse()
    {
        // Intent: 外部から渡された不正な diff path は一致なしとして扱う。
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Items[*].Value");

        Assert.False(pattern.IsMatch("Items[A"));
    }

    [Fact]
    public void PathMatches_CanBeUsedDirectlyFromLinqWhere()
    {
        // Intent: 全差分を保持したまま、標準 LINQ の Where で対象差分だけ判定から除外できる。
        ParallelDiffEntry[] allDiffs =
        [
            new ParallelDiffEntry
            {
                Path = "Boards[A].Files[No1/ygx].Document.Root.Children[0].Children[0].Attribute[LastEditingTime].Value",
            },
            new ParallelDiffEntry
            {
                Path = "Boards[A].Files[No1/ygx].Document.Root.Children[0].Children[0].Attribute[Width].Value",
            },
        ];
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse(
            "Boards[*].Files[*].Document.Root.Children[*].Children[*].Attribute[LastEditingTime].Value");

        ParallelDiffEntry[] effectiveDiffs = allDiffs
            .Where(entry => !entry.PathMatches(pattern))
            .ToArray();

        ParallelDiffEntry remaining = Assert.Single(effectiveDiffs);
        Assert.Contains("Attribute[Width]", remaining.Path, StringComparison.Ordinal);
        Assert.Equal(2, allDiffs.Length);
    }

    [Fact]
    public void PathMatches_WithNullArguments_ThrowsArgumentNullException()
    {
        // Intent: extension の引数不正を既存 BCL 方針と同じ ArgumentNullException で通知する。
        ParallelDiffEntry entry = new() { Path = "Items[A].Value" };
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Items[*].Value");

        Assert.Throws<ArgumentNullException>(() => ParallelDiffEntryPathExtensions.PathMatches(null!, pattern));
        Assert.Throws<ArgumentNullException>(() => entry.PathMatches(null!));
    }
}
