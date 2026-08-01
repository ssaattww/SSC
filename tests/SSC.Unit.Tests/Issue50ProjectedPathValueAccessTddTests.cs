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

    /// <summary>
    /// Missing の model slot でも projection の値と状態が元の entry と一致することを確認します。
    /// </summary>
    [Fact]
    public void Projection_MissingSlotKeepsIndexerGetStateAndEntryValuesAligned()
    {
        var result = ParallelCompareApi.Compare(
        [
            new ListDocument
            {
                Items = [new ListItem { Name = "present" }],
            },
            new ListDocument(),
        ]);

        var projection = Assert.Single(
            result.GetDiffEntryPathProjections(new KeepStandardProjector())
                .Where(candidate => candidate.Entry.Path == "Items[0]"));

        Assert.Equal(projection.Entry.Values[0].Value, projection[0]);
        Assert.Equal(projection.Entry.Values[1].Value, projection[1]);
        Assert.Equal(projection.Entry.Values[0].State, projection.GetState(0));
        Assert.Equal(projection.Entry.Values[1].State, projection.GetState(1));
        Assert.Equal(ValueState.Missing, projection.GetState(1));
    }

    /// <summary>
    /// 値と状態の参照で範囲外の model index を拒否することを確認します。
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Projection_RejectsOutOfRangeModelIndex(int modelIndex)
    {
        var result = CreateSimpleResult();
        var projection = Assert.Single(
            result.GetDiffEntryPathProjections(new KeepStandardProjector()));

        Assert.Throws<ArgumentOutOfRangeException>(() => projection[modelIndex]);
        Assert.Throws<ArgumentOutOfRangeException>(() => projection.GetState(modelIndex));
    }

    /// <summary>
    /// 利用側定義 path の完全一致で対象 projection だけを取得できることを確認します。
    /// </summary>
    [Fact]
    public void ExactSearch_ReturnsOnlyOrdinalCaseSensitiveMatches()
    {
        var result = CreateSimpleResult();
        var projector = new RenameValueProjector();

        var matches = result.GetDiffEntryPathProjections(projector, "Alias");

        var projection = Assert.Single(matches);
        Assert.Equal("Value", projection.Entry.Path);
        Assert.Empty(result.GetDiffEntryPathProjections(projector, "alias"));
        Assert.Empty(result.GetDiffEntryPathProjections(projector, "Missing"));
    }

    /// <summary>
    /// 同じ投影済み path に集約された複数 entry を標準順序と重複を保持して返すことを確認します。
    /// </summary>
    [Fact]
    public void ExactSearch_PreservesDuplicateProjectedPathsAndOriginalOrder()
    {
        var result = ParallelCompareApi.Compare(
        [
            new ListDocument
            {
                Items =
                [
                    new ListItem { Name = "before-a" },
                    new ListItem { Name = "before-b" },
                ],
            },
            new ListDocument
            {
                Items =
                [
                    new ListItem { Name = "after-a" },
                    new ListItem { Name = "after-b" },
                ],
            },
        ]);
        var projector = new CollapseItemSelectorProjector();

        var matches = result.GetDiffEntryPathProjections(projector, "Item.Name");

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match => Assert.Equal("Item.Name", match.ProjectedPath));
        Assert.Equal(
            ["Items[0].Name", "Items[1].Name"],
            matches.Select(match => match.Entry.Path).ToArray());
    }

    /// <summary>
    /// pattern 検索で投影済み path を絞り込めることを確認します。
    /// </summary>
    [Fact]
    public void PatternSearch_FiltersProjectedPaths()
    {
        var result = ParallelCompareApi.Compare(
        [
            new ListDocument
            {
                Items =
                [
                    new ListItem { Name = "before-a" },
                    new ListItem { Name = "before-b" },
                ],
            },
            new ListDocument
            {
                Items =
                [
                    new ListItem { Name = "after-a" },
                    new ListItem { Name = "after-b" },
                ],
            },
        ]);
        var projector = new KeepStandardProjector();
        var pattern = ParallelDiffPathPattern.Parse("Items[*].Name");

        var matches = result.GetDiffEntryPathProjections(projector, pattern);

        Assert.Equal(2, matches.Count);
        Assert.Equal(
            ["Items[0].Name", "Items[1].Name"],
            matches.Select(match => match.ProjectedPath).ToArray());
    }

    /// <summary>
    /// pattern 検索が一致しない投影を除外しつつ、同じ投影 path の重複と元の順序を保持することを確認します。
    /// </summary>
    [Fact]
    public void PatternSearch_PreservesDuplicateProjectedPathsAndOriginalOrderAmongMixedMatches()
    {
        var matches = CreateMixedListResult().GetDiffEntryPathProjections(
            new CollapseItemSelectorProjector(),
            ParallelDiffPathPattern.Parse("Item.Name"));

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match => Assert.Equal("Item.Name", match.ProjectedPath));
        Assert.Equal(
            ["Items[0].Name", "Items[1].Name"],
            matches.Select(match => match.Entry.Path).ToArray());
    }

    /// <summary>
    /// pattern 検索が既存の escape 規則を使用し、一致しない場合は空一覧を返すことを確認します。
    /// </summary>
    [Fact]
    public void PatternSearch_UsesEscapedSelectorAndReturnsEmptyForNoMatch()
    {
        var result = ParallelCompareApi.Compare(
        [
            new KeyedDocument
            {
                Items =
                [
                    new KeyedListItem { Id = "*", Name = "before-asterisk" },
                    new KeyedListItem { Id = "other", Name = "before-other" },
                ],
            },
            new KeyedDocument
            {
                Items =
                [
                    new KeyedListItem { Id = "*", Name = "after-asterisk" },
                    new KeyedListItem { Id = "other", Name = "after-other" },
                ],
            },
        ]);
        var projector = new KeepStandardProjector();

        var escapedMatches = result.GetDiffEntryPathProjections(
            projector,
            ParallelDiffPathPattern.Parse("Items[\\*].Name"));

        var escapedMatch = Assert.Single(escapedMatches);
        Assert.Equal("Items[*].Name", escapedMatch.ProjectedPath);
        Assert.Empty(result.GetDiffEntryPathProjections(
            projector,
            ParallelDiffPathPattern.Parse("Items[not-present].Name")));
    }

    /// <summary>
    /// 完全一致検索と pattern 検索が引数を検証することを確認します。
    /// </summary>
    [Fact]
    public void Searches_ValidateArguments()
    {
        var result = CreateSimpleResult();
        var projector = new KeepStandardProjector();
        var pattern = ParallelDiffPathPattern.Parse("Value");

        Assert.Throws<ArgumentNullException>(() =>
            ParallelProjectedPathSearchExtensions.GetDiffEntryPathProjections<SampleDocument>(
                null!, projector, "Value"));
        Assert.Throws<ArgumentNullException>(() =>
            result.GetDiffEntryPathProjections(null!, "Value"));
        Assert.Throws<ArgumentNullException>(() =>
            result.GetDiffEntryPathProjections(projector, (string)null!));
        Assert.Throws<ArgumentException>(() =>
            result.GetDiffEntryPathProjections(projector, string.Empty));

        Assert.Throws<ArgumentNullException>(() =>
            ParallelProjectedPathSearchExtensions.GetDiffEntryPathProjections<SampleDocument>(
                null!, projector, pattern));
        Assert.Throws<ArgumentNullException>(() =>
            result.GetDiffEntryPathProjections(null!, pattern));
        Assert.Throws<ArgumentNullException>(() =>
            result.GetDiffEntryPathProjections(projector, (ParallelDiffPathPattern)null!));
    }

    private static CompareResult<SampleDocument> CreateSimpleResult()
    {
        return ParallelCompareApi.Compare(
        [
            new SampleDocument { Value = "before" },
            new SampleDocument { Value = "after" },
        ]);
    }

    private static CompareResult<ListDocument> CreateMixedListResult()
    {
        return ParallelCompareApi.Compare(
        [
            new ListDocument
            {
                Items =
                [
                    new ListItem { Name = "before-name-a", Category = "before-category-a" },
                    new ListItem { Name = "before-name-b", Category = "before-category-b" },
                ],
            },
            new ListDocument
            {
                Items =
                [
                    new ListItem { Name = "after-name-a", Category = "after-category-a" },
                    new ListItem { Name = "after-name-b", Category = "after-category-b" },
                ],
            },
        ]);
    }

    private sealed class KeepStandardProjector : IParallelDiffPathProjector
    {
        public ParallelDiffPathSegmentProjection Project(ParallelDiffPathProjectionContext context)
        {
            return ParallelDiffPathSegmentProjection.KeepStandard();
        }
    }

    private sealed class RenameValueProjector : IParallelDiffPathProjector
    {
        public ParallelDiffPathSegmentProjection Project(ParallelDiffPathProjectionContext context)
        {
            return context.Current.StandardSegment.MemberName == "Value"
                ? ParallelDiffPathSegmentProjection.Replace(
                    ParallelDiffPathSegment.Member("Alias"))
                : ParallelDiffPathSegmentProjection.KeepStandard();
        }
    }

    private sealed class CollapseItemSelectorProjector : IParallelDiffPathProjector
    {
        public ParallelDiffPathSegmentProjection Project(ParallelDiffPathProjectionContext context)
        {
            return context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    ParallelDiffPathSegment.Member("Item"))
                : ParallelDiffPathSegmentProjection.KeepStandard();
        }
    }

    private sealed class SampleDocument
    {
        public string? Value { get; init; }
    }

    private sealed class ListDocument
    {
        public IReadOnlyList<ListItem> Items { get; init; } = [];
    }

    private sealed class ListItem
    {
        public string? Name { get; init; }

        public string? Category { get; init; }
    }

    private sealed class KeyedDocument
    {
        public IReadOnlyList<KeyedListItem> Items { get; init; } = [];
    }

    private sealed class KeyedListItem
    {
        [CompareKey]
        public string Id { get; init; } = string.Empty;

        public string? Name { get; init; }
    }
}
