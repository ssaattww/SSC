using SSC;

namespace SSC.Unit.Tests;

/// <summary>
/// 利用側定義 path に対する祖先 pattern の照合契約を検証します。
/// </summary>
public sealed class ParallelDiffPathProjectionAncestorUnitTests
{
    /// <summary>
    /// 利用側定義 path の祖先 pattern が子孫 path に一致し、segment 境界の異なる sibling と標準 path には一致しないことを検証します。
    /// </summary>
    [Fact]
    public void PathMatches_WithProjectedAncestorPattern_MatchesDescendantAtSegmentBoundaryOnly()
    {
        CompareResult<ProjectionDocument> result = ParallelCompareApi.Compare(
        [
            new ProjectionDocument
            {
                Items = [new ProjectionItem { Name = "left" }],
            },
            new ProjectionDocument
            {
                Items = [new ProjectionItem { Name = "right" }],
            },
        ]);
        ParallelDiffPathPattern projectedAncestorPattern =
            ParallelDiffPathPattern.Parse("Entry[*]");
        ParallelDiffPathPattern standardAncestorPattern =
            ParallelDiffPathPattern.Parse("Items[*]");

        ParallelDiffEntryPathProjection projection = Assert.Single(
            result.GetDiffEntryPathProjections(new RenameItemsProjector("Entry")));
        ParallelDiffEntryPathProjection siblingProjection = Assert.Single(
            result.GetDiffEntryPathProjections(new RenameItemsProjector("EntryOther")));

        Assert.Equal("Entry[0].Name", projection.ProjectedPath);
        Assert.True(projection.PathMatches(projectedAncestorPattern));
        Assert.False(projection.Entry.PathMatches(projectedAncestorPattern));

        Assert.True(projection.Entry.PathMatches(standardAncestorPattern));
        Assert.False(projection.PathMatches(standardAncestorPattern));

        Assert.Equal("EntryOther[0].Name", siblingProjection.ProjectedPath);
        Assert.False(siblingProjection.PathMatches(projectedAncestorPattern));
    }

    /// <summary>
    /// Items segment の member 名を指定された利用側定義名へ置き換えるテスト用投影器です。
    /// </summary>
    private sealed class RenameItemsProjector : IParallelDiffPathProjector
    {
        private readonly string _projectedMemberName;

        /// <summary>
        /// Items segment の置換先 member 名を指定して投影器を生成します。
        /// </summary>
        /// <param name="projectedMemberName">利用側定義 path で使用する member 名。</param>
        public RenameItemsProjector(string projectedMemberName)
        {
            _projectedMemberName = projectedMemberName;
        }

        /// <inheritdoc />
        public ParallelDiffPathSegmentProjection Project(
            ParallelDiffPathProjectionContext context)
        {
            return context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName(_projectedMemberName))
                : ParallelDiffPathSegmentProjection.KeepStandard();
        }
    }

    /// <summary>
    /// 利用側定義 path 投影テストで比較する document です。
    /// </summary>
    private sealed class ProjectionDocument
    {
        /// <summary>
        /// ordinal alignment される要素一覧を取得します。
        /// </summary>
        public List<ProjectionItem> Items { get; init; } = [];
    }

    /// <summary>
    /// 利用側定義 path 投影テストで差分を生成する要素です。
    /// </summary>
    private sealed class ProjectionItem
    {
        /// <summary>
        /// 差分となる名称を取得します。
        /// </summary>
        public string Name { get; init; } = string.Empty;
    }
}
