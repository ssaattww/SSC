using SSC;

namespace SSC.Unit.Tests;

/// <summary>
/// 差分 path pattern の構文、照合、および比較結果を変更しない絞り込み契約を検証します。
/// </summary>
public sealed class ParallelDiffPathPatternUnitTests
{
    /// <summary>
    /// 固定構造と属性名を比較しながら、変動する selector を <c>[*]</c> で吸収できることを検証します。
    /// </summary>
    [Fact]
    public void Parse_WithExactAndWildcardSelectors_MatchesExpectedPath()
    {
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse(
            "Boards[*].Files[*].Document.Root.Children[*].Children[*].Attribute[LastEditingTime].Value");

        bool matched = pattern.IsMatch(
            "Boards[Board-A].Files[No1/ygx].Document.Root.Children[0].Children[#2].Attribute[LastEditingTime].Value");

        Assert.True(matched);
    }

    /// <summary>
    /// <c>[*]</c> が key selector と ordinal selector の両方に一致することを検証します。
    /// </summary>
    [Fact]
    public void IsMatch_WithWildcardSelector_MatchesKeyAndOrdinalSelectors()
    {
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Items[*].Value");

        Assert.True(pattern.IsMatch("Items[A].Value"));
        Assert.True(pattern.IsMatch("Items[0].Value"));
        Assert.True(pattern.IsMatch("Items[#0].Value"));
    }

    /// <summary>
    /// key の <c>[0]</c> と ordinal の <c>[#0]</c> を既存 path grammar に従って区別することを検証します。
    /// </summary>
    [Fact]
    public void IsMatch_WithExactSelectors_DistinguishesKeyFromOrdinal()
    {
        ParallelDiffPathPattern keyPattern = ParallelDiffPathPattern.Parse("Items[0].Value");
        ParallelDiffPathPattern ordinalPattern = ParallelDiffPathPattern.Parse("Items[#0].Value");

        Assert.True(keyPattern.IsMatch("Items[0].Value"));
        Assert.False(keyPattern.IsMatch("Items[#0].Value"));
        Assert.True(ordinalPattern.IsMatch("Items[#0].Value"));
        Assert.False(ordinalPattern.IsMatch("Items[0].Value"));
    }

    /// <summary>
    /// wildcard が selector だけに作用し、member 名と path 深度を曖昧にしないことを検証します。
    /// </summary>
    [Fact]
    public void IsMatch_WithDifferentMemberOrSegmentCount_ReturnsFalse()
    {
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse(
            "Boards[*].Attribute[LastEditingTime].Value");

        Assert.False(pattern.IsMatch("Boards[A].Attribute[CreatedAt].Value"));
        Assert.False(pattern.IsMatch("Boards[A].Metadata.Attribute[LastEditingTime].Value"));
        Assert.False(pattern.IsMatch("Boards.Attribute[LastEditingTime].Value"));
    }

    /// <summary>
    /// bracket 内の dot、閉じ bracket、backslash、および <c>#</c> の既存 escape を通常 path と同じ意味で扱うことを検証します。
    /// </summary>
    [Fact]
    public void IsMatch_WithEscapedKeyAndDotInsideSelector_PreservesExistingGrammar()
    {
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse(
            "Items[A.B].Attributes[A\\]B].EscapedBackslash[A\\\\B].EscapedHash[\\#0].Value");

        Assert.True(pattern.IsMatch("Items[A.B].Attributes[A\\]B].EscapedBackslash[A\\\\B].EscapedHash[\\#0].Value"));
        Assert.False(pattern.IsMatch("Items[A].Attributes[A\\]B].EscapedBackslash[A\\\\B].EscapedHash[\\#0].Value"));
        Assert.False(pattern.IsMatch("Items[A.B].Attributes[A\\]B].EscapedBackslash[A\\\\B].EscapedHash[#0].Value"));
    }

    /// <summary>
    /// <c>[\\*]</c> が <c>*</c> をエスケープして通常文字の key として扱い、<c>[*]</c> の wildcard と区別することを検証します。
    /// </summary>
    [Fact]
    public void IsMatch_WithEscapedAsteriskSelector_TreatsAsteriskAsRegularKeyCharacter()
    {
        ParallelDiffPathPattern escapedPattern = ParallelDiffPathPattern.Parse("Items[\\*].Value");
        ParallelDiffPathPattern wildcardPattern = ParallelDiffPathPattern.Parse("Items[*].Value");

        Assert.True(escapedPattern.IsMatch("Items[*].Value"));
        Assert.False(escapedPattern.IsMatch("Items[other].Value"));
        Assert.True(wildcardPattern.IsMatch("Items[*].Value"));
        Assert.True(wildcardPattern.IsMatch("Items[other].Value"));
    }

    /// <summary>
    /// 解釈不能な non-null pattern を例外を漏らさず <see langword="false"/> として扱うことを検証します。
    /// </summary>
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
        bool parsed = ParallelDiffPathPattern.TryParse(patternText, out ParallelDiffPathPattern? pattern);

        Assert.False(parsed);
        Assert.Null(pattern);
    }

    /// <summary>
    /// <see langword="null"/> の pattern を TryParse が失敗として扱い、解析結果を返さないことを検証します。
    /// </summary>
    [Fact]
    public void TryParse_WithNullPattern_ReturnsFalse()
    {
        bool parsed = ParallelDiffPathPattern.TryParse(null, out ParallelDiffPathPattern? pattern);

        Assert.False(parsed);
        Assert.Null(pattern);
    }

    /// <summary>
    /// Parse が不正な non-null pattern を <see cref="FormatException"/> として通知することを検証します。
    /// </summary>
    [Fact]
    public void Parse_WithInvalidPattern_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ParallelDiffPathPattern.Parse("Items[]"));
    }

    /// <summary>
    /// Parse が <see langword="null"/> の pattern を <see cref="ArgumentNullException"/> として通知することを検証します。
    /// </summary>
    [Fact]
    public void Parse_WithNullPattern_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ParallelDiffPathPattern.Parse(null!));
    }

    /// <summary>
    /// 外部から渡された不正な diff path を一致なしとして扱うことを検証します。
    /// </summary>
    [Fact]
    public void IsMatch_WithInvalidCandidatePath_ReturnsFalse()
    {
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Items[*].Value");

        Assert.False(pattern.IsMatch("Items[A"));
    }

    /// <summary>
    /// IsMatch が <see langword="null"/> の path を <see cref="ArgumentNullException"/> として通知することを検証します。
    /// </summary>
    [Fact]
    public void IsMatch_WithNullPath_ThrowsArgumentNullException()
    {
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Items[*].Value");

        Assert.Throws<ArgumentNullException>(() => pattern.IsMatch(null!));
    }

    /// <summary>
    /// 全差分を保持したまま、標準 LINQ の Where で一致する差分だけを除外できることを検証します。
    /// </summary>
    [Fact]
    public void PathMatches_CanBeUsedDirectlyFromLinqWhere()
    {
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

    /// <summary>
    /// path filter が比較済み result の Issues、Root の差分状態、および CompareIgnore による比較対象除外を変更しないことを検証します。
    /// </summary>
    [Fact]
    public void PathMatches_FilteringDoesNotChangeResultStateOrCompareIgnoreBehavior()
    {
        PathFilterRegressionModel[] models =
        [
            new PathFilterRegressionModel { Included = "left", Ignored = "left ignored" },
            new PathFilterRegressionModel { Included = "right", Ignored = "right ignored" },
        ];
        CompareResult<PathFilterRegressionModel> result = ParallelCompareApi.Compare(models);
        IReadOnlyList<CompareIssue> issuesBeforeFiltering = result.Issues;
        bool hasDifferencesBeforeFiltering = ((IParallelNode)result.Root!).HasDifferences();
        ParallelDiffEntry[] allDiffs = result.GetDiffEntries().ToArray();
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Included");

        ParallelDiffEntry[] filteredDiffs = allDiffs.Where(entry => !entry.PathMatches(pattern)).ToArray();

        Assert.Empty(result.Issues);
        Assert.Same(issuesBeforeFiltering, result.Issues);
        Assert.True(hasDifferencesBeforeFiltering);
        Assert.True(((IParallelNode)result.Root!).HasDifferences());
        Assert.Single(allDiffs);
        Assert.Equal("Included", allDiffs[0].Path);
        Assert.Empty(filteredDiffs);
    }

    /// <summary>
    /// PathMatches が null の entry と pattern を <see cref="ArgumentNullException"/> として通知することを検証します。
    /// </summary>
    [Fact]
    public void PathMatches_WithNullArguments_ThrowsArgumentNullException()
    {
        ParallelDiffEntry entry = new() { Path = "Items[A].Value" };
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Items[*].Value");

        Assert.Throws<ArgumentNullException>(() => ParallelDiffEntryPathExtensions.PathMatches(null!, pattern));
        Assert.Throws<ArgumentNullException>(() => entry.PathMatches(null!));
    }

    private sealed class PathFilterRegressionModel
    {
        public string? Included { get; init; }

        [CompareIgnore]
        public string? Ignored { get; init; }
    }
}
