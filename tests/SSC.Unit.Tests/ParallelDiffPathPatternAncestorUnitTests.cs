using SSC;

namespace SSC.Unit.Tests;

/// <summary>
/// 差分 path pattern が一致した node 自身と、その配下の子孫 path をまとめて照合する契約を検証します。
/// </summary>
public sealed class ParallelDiffPathPatternAncestorUnitTests
{
    /// <summary>
    /// 祖先 pattern が完全一致する path に加え、子 node、属性、および値の子孫 path に一致することを検証します。
    /// </summary>
    [Theory]
    [InlineData("Root.A")]
    [InlineData("Root.A.B")]
    [InlineData("Root.A.B.C")]
    [InlineData("Root.A.Attribute[Width].Value")]
    [InlineData("Root.A.Value")]
    public void IsMatch_WithAncestorPattern_MatchesExactAndDescendantPaths(string path)
    {
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Root.A");

        Assert.True(pattern.IsMatch(path));
    }

    /// <summary>
    /// member 名の文字列 prefix が同じでも、path segment 境界が異なる兄弟 path には一致しないことを検証します。
    /// </summary>
    [Theory]
    [InlineData("Root.AA")]
    [InlineData("Root.AA.B")]
    [InlineData("Root.A-Other.Value")]
    public void IsMatch_WithSimilarMemberName_DoesNotMatchSiblingPath(string path)
    {
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Root.A");

        Assert.False(pattern.IsMatch(path));
    }

    /// <summary>
    /// 候補 path が pattern より浅い場合は祖先一致として扱わないことを検証します。
    /// </summary>
    [Fact]
    public void IsMatch_WithCandidateShorterThanPattern_ReturnsFalse()
    {
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Root.A.B");

        Assert.False(pattern.IsMatch("Root.A"));
    }

    /// <summary>
    /// wildcard selector を含む祖先 pattern が selector 一致後の子孫 path に一致し、selector のない別 segment には一致しないことを検証します。
    /// </summary>
    [Fact]
    public void IsMatch_WithWildcardSelectorAncestor_PreservesSelectorBoundary()
    {
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Root.Children[*]");

        Assert.True(pattern.IsMatch("Root.Children[0].Attribute[Width].Value"));
        Assert.True(pattern.IsMatch("Root.Children[#1].Value"));
        Assert.False(pattern.IsMatch("Root.Children.Value"));
        Assert.False(pattern.IsMatch("Root.ChildrenOther[0].Value"));
    }

    /// <summary>
    /// LINQ filter で祖先 pattern 配下の子 node、属性、および値の差分だけをまとめて除外できることを検証します。
    /// </summary>
    [Fact]
    public void PathMatches_WithAncestorPattern_FiltersAllDescendantDiffs()
    {
        ParallelDiffEntry[] allDiffs =
        [
            new ParallelDiffEntry { Path = "Root.A.Child.Value" },
            new ParallelDiffEntry { Path = "Root.A.Attribute[Width].Value" },
            new ParallelDiffEntry { Path = "Root.A.Value" },
            new ParallelDiffEntry { Path = "Root.AA.Value" },
        ];
        ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse("Root.A");

        ParallelDiffEntry[] effectiveDiffs = allDiffs
            .Where(entry => !entry.PathMatches(pattern))
            .ToArray();

        ParallelDiffEntry remaining = Assert.Single(effectiveDiffs);
        Assert.Equal("Root.AA.Value", remaining.Path);
        Assert.Equal(4, allDiffs.Length);
    }
}
