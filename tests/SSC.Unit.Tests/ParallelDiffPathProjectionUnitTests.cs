using SSC;

namespace SSC.Unit.Tests;

/// <summary>
/// 差分 entry path の投影契約を検証します。
/// </summary>
public sealed class ParallelDiffPathProjectionUnitTests
{
    /// <summary>
    /// 各種 segment factory が期待どおりの選択子を生成することを確認します。
    /// </summary>
    [Fact]
    public void SegmentFactories_CreateMemberKeyAndOrdinalSegments()
    {
        var member = ParallelDiffPathSegment.Member("Root");
        var key = ParallelDiffPathSegment.Key("Items", "A");
        var ordinal = ParallelDiffPathSegment.Ordinal("Children", 2);

        Assert.Equal("Root", member.MemberName);
        Assert.Null(member.Selector);

        Assert.Equal("Items", key.MemberName);
        Assert.NotNull(key.Selector);
        Assert.Equal(ParallelDiffPathSelectorKind.Key, key.Selector.Value.Kind);
        Assert.Equal("A", key.Selector.Value.KeyText);
        Assert.Null(key.Selector.Value.Ordinal);

        Assert.Equal("Children", ordinal.MemberName);
        Assert.NotNull(ordinal.Selector);
        Assert.Equal(ParallelDiffPathSelectorKind.Ordinal, ordinal.Selector.Value.Kind);
        Assert.Null(ordinal.Selector.Value.KeyText);
        Assert.Equal(2, ordinal.Selector.Value.Ordinal);
    }

    /// <summary>
    /// member 名を変更しても選択子が維持されることを確認します。
    /// </summary>
    [Fact]
    public void WithMemberName_PreservesSelectors()
    {
        var key = ParallelDiffPathSegment.Key("Items", "A").WithMemberName("Entries");
        var ordinal = ParallelDiffPathSegment.Ordinal("Children", 3).WithMemberName("Child");

        Assert.Equal("Entries", key.MemberName);
        Assert.NotNull(key.Selector);
        Assert.Equal(ParallelDiffPathSelectorKind.Key, key.Selector.Value.Kind);
        Assert.Equal("A", key.Selector.Value.KeyText);

        Assert.Equal("Child", ordinal.MemberName);
        Assert.NotNull(ordinal.Selector);
        Assert.Equal(ParallelDiffPathSelectorKind.Ordinal, ordinal.Selector.Value.Kind);
        Assert.Equal(3, ordinal.Selector.Value.Ordinal);
    }

    /// <summary>
    /// path grammar で表現できない member 名を拒否することを確認します。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("A.B")]
    [InlineData("A[B")]
    [InlineData("A]B")]
    public void SegmentFactories_RejectMemberNamesThatCannotBeRepresented(string memberName)
    {
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Member(memberName));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Key(memberName, "A"));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Ordinal(memberName, 0));
    }

    /// <summary>
    /// null と不正な選択子値を拒否することを確認します。
    /// </summary>
    [Fact]
    public void SegmentFactories_RejectNullAndInvalidSelectorValues()
    {
        Assert.Throws<ArgumentNullException>(() => ParallelDiffPathSegment.Member(null!));
        Assert.Throws<ArgumentNullException>(() => ParallelDiffPathSegment.Key(null!, "A"));
        Assert.Throws<ArgumentNullException>(() => ParallelDiffPathSegment.Ordinal(null!, 0));
        Assert.Throws<ArgumentNullException>(() => ParallelDiffPathSegment.Key("Items", null!));
        Assert.Throws<ArgumentException>(() => ParallelDiffPathSegment.Key("Items", string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelDiffPathSegment.Ordinal("Items", -1));
    }

    /// <summary>
    /// 維持、置換、省略の投影結果を生成できることを確認します。
    /// </summary>
    [Fact]
    public void ProjectionFactories_RepresentKeepReplaceAndOmit()
    {
        var replacement = ParallelDiffPathSegment.Member("Alias");

        var keep = ParallelDiffPathSegmentProjection.KeepStandard();
        var replace = ParallelDiffPathSegmentProjection.Replace(replacement);
        var omit = ParallelDiffPathSegmentProjection.Omit();

        Assert.Equal(ParallelDiffPathSegmentProjectionKind.KeepStandard, keep.Kind);
        Assert.Null(keep.Replacement);
        Assert.Equal(ParallelDiffPathSegmentProjectionKind.Replace, replace.Kind);
        Assert.Same(replacement, replace.Replacement);
        Assert.Equal(ParallelDiffPathSegmentProjectionKind.Omit, omit.Kind);
        Assert.Null(omit.Replacement);
        Assert.Throws<ArgumentNullException>(() => ParallelDiffPathSegmentProjection.Replace(null!));
    }

    /// <summary>
    /// segment を置換し、走査文脈を投影器へ渡すことを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_ReplacesSegmentAndProvidesTraversalContext()
    {
        var result = CreateSingleOrdinalResult();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName("Entry"))
                : ParallelDiffPathSegmentProjection.KeepStandard());

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal("Items[0].Name", projection.Entry.Path);
        Assert.Equal("Items[0]", projection.Entry.ParentPath);
        Assert.Equal("Entry[0].Name", projection.ProjectedPath);
        Assert.Equal("Entry[0]", projection.ProjectedParentPath);
        Assert.NotNull(projection.Entry.Node);
        Assert.Same(projection.Entry.Node, result.GetNodeByPath(projection.Entry.Path));

        Assert.Collection(
            projector.Contexts,
            first =>
            {
                Assert.Empty(first.Ancestors);
                Assert.Equal("Items", first.Current.StandardSegment.MemberName);
                Assert.NotNull(first.Current.Node);
                Assert.Single(first.Current.Siblings);
                Assert.Same(result.Root, first.Current.ParentNode);
            },
            second =>
            {
                var ancestor = Assert.Single(second.Ancestors);
                Assert.Equal("Items", ancestor.StandardSegment.MemberName);
                Assert.Equal("Name", second.Current.StandardSegment.MemberName);
                Assert.NotNull(second.Current.Node);
                Assert.Single(second.Current.Siblings);
            });
    }

    /// <summary>
    /// 末尾 segment を省略した場合に親 path と同じ path を許容することを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_OmitsLeafAndAllowsPathToEqualParentPath()
    {
        var result = CreateSingleOrdinalResult();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName switch
            {
                "Items" => ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName("Entry")),
                "Name" => ParallelDiffPathSegmentProjection.Omit(),
                _ => ParallelDiffPathSegmentProjection.KeepStandard(),
            });

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal("Entry[0]", projection.ProjectedPath);
        Assert.Equal("Entry[0]", projection.ProjectedParentPath);
    }

    /// <summary>
    /// 標準 parent path の範囲にある全 segment を省略した場合に投影 parent path が null になることを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_ReturnsNullProjectedParentPathWhenAllParentSegmentsAreOmitted()
    {
        var result = CreateSingleOrdinalResult();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Omit()
                : ParallelDiffPathSegmentProjection.KeepStandard());

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal("Name", projection.ProjectedPath);
        Assert.Null(projection.ProjectedParentPath);
    }

    /// <summary>
    /// 全 segment を省略した空の投影 path を拒否することを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_RejectsAnEmptyProjectedPath()
    {
        var result = CreateSingleOrdinalResult();
        var projector = new RecordingProjector(_ => ParallelDiffPathSegmentProjection.Omit());

        var exception = Assert.Throws<InvalidOperationException>(
            () => result.GetDiffEntryPathProjections(projector));

        Assert.Contains("Items[0].Name", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// container presence entry の文脈に null node を渡すことを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_ProvidesNullNodeForContainerPresenceEntry()
    {
        var result = ParallelCompareApi.Compare(
        [
            new OptionalDocument { Items = [] },
            new OptionalDocument { Items = null },
        ]);
        var projector = new RecordingProjector(_ => ParallelDiffPathSegmentProjection.KeepStandard());

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));
        var context = Assert.Single(projector.Contexts);

        Assert.Equal(ParallelDiffEntryKind.ContainerPresence, projection.Entry.Kind);
        Assert.Equal("Items", projection.ProjectedPath);
        Assert.Null(projection.ProjectedParentPath);
        Assert.Null(context.Current.Node);
        Assert.Empty(context.Current.Siblings);
        Assert.Same(result.Root, context.Current.ParentNode);
    }

    /// <summary>
    /// 投影された key text を path grammar に従って escape することを確認します。
    /// </summary>
    [Theory]
    [InlineData("A]B", "Alias[A\\]B].Name")]
    [InlineData("A\\B", "Alias[A\\\\B].Name")]
    [InlineData("#0", "Alias[\\#0].Name")]
    public void GetDiffEntryPathProjections_EscapesProjectedKeyText(
        string keyText,
        string expectedPath)
    {
        var result = CreateSingleOrdinalResult();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    ParallelDiffPathSegment.Key("Alias", keyText))
                : ParallelDiffPathSegmentProjection.KeepStandard());

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal(expectedPath, projection.ProjectedPath);
    }

    /// <summary>
    /// 投影 path の照合が標準 path の照合を変更しないことを確認します。
    /// </summary>
    [Fact]
    public void ProjectionPathMatches_UsesProjectedPathWithoutChangingStandardMatching()
    {
        var result = CreateSingleOrdinalResult();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName("Entry"))
                : ParallelDiffPathSegmentProjection.KeepStandard());
        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));
        var projectedPattern = ParallelDiffPathPattern.Parse("Entry[*].Name");

        Assert.True(projection.PathMatches(projectedPattern));
        Assert.False(projection.Entry.PathMatches(projectedPattern));
    }

    /// <summary>
    /// 複数 entry による重複した投影 path を保持することを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_PreservesDuplicateProjectedPaths()
    {
        var result = ParallelCompareApi.Compare(
        [
            new KeyedDocument
            {
                Items =
                [
                    new KeyedItem { Id = "A", Name = "left-a" },
                    new KeyedItem { Id = "B", Name = "left-b" },
                ],
            },
            new KeyedDocument
            {
                Items =
                [
                    new KeyedItem { Id = "A", Name = "right-a" },
                    new KeyedItem { Id = "B", Name = "right-b" },
                ],
            },
        ]);
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    ParallelDiffPathSegment.Member("Item"))
                : ParallelDiffPathSegmentProjection.KeepStandard());

        var projections = result.GetDiffEntryPathProjections(projector);

        Assert.Equal(2, projections.Count);
        Assert.All(projections, projection => Assert.Equal("Item.Name", projection.ProjectedPath));
        Assert.Equal(
            ["Items[A].Name", "Items[B].Name"],
            projections.Select(projection => projection.Entry.Path).ToArray());
    }

    /// <summary>
    /// 投影処理が標準差分 entry を変更しないことを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_DoesNotChangeStandardDiffEntries()
    {
        var result = CreateSingleOrdinalResult();
        var before = result.GetDiffEntries();
        var projector = new RecordingProjector(context =>
            context.Current.StandardSegment.MemberName == "Items"
                ? ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName("Entry"))
                : ParallelDiffPathSegmentProjection.KeepStandard());

        var projections = result.GetDiffEntryPathProjections(projector);
        var after = result.GetDiffEntries();

        Assert.Equal(
            before.Select(ToEntrySnapshot),
            projections.Select(projection => ToEntrySnapshot(projection.Entry)));
        Assert.Equal(before.Select(ToEntrySnapshot), after.Select(ToEntrySnapshot));
    }

    /// <summary>
    /// 引数を検証し、投影器の例外をそのまま伝播することを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_ValidatesArgumentsAndPropagatesProjectorExceptions()
    {
        var result = CreateSingleOrdinalResult();
        var projectorException = new InvalidOperationException("projector failure");
        var throwingProjector = new RecordingProjector(_ => throw projectorException);

        Assert.Throws<ArgumentNullException>(
            () => ParallelPathAccessExtensions.GetDiffEntryPathProjections<OrdinalDocument>(null!, throwingProjector));
        Assert.Throws<ArgumentNullException>(() => result.GetDiffEntryPathProjections(null!));
        Assert.Same(
            projectorException,
            Assert.Throws<InvalidOperationException>(
                () => result.GetDiffEntryPathProjections(throwingProjector)));
    }

    /// <summary>
    /// root を持たない比較結果では空の投影一覧を返すことを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_ReturnsEmptyWhenResultHasNoRoot()
    {
        var result = new CompareResult<OrdinalDocument>();
        var projector = new RecordingProjector(_ => ParallelDiffPathSegmentProjection.KeepStandard());

        Assert.Empty(result.GetDiffEntryPathProjections(projector));
        Assert.Empty(projector.Contexts);
    }

    private static CompareResult<OrdinalDocument> CreateSingleOrdinalResult()
    {
        return ParallelCompareApi.Compare(
        [
            new OrdinalDocument
            {
                Items = [new OrdinalItem { Name = "left" }],
            },
            new OrdinalDocument
            {
                Items = [new OrdinalItem { Name = "right" }],
            },
        ]);
    }

    private static string ToEntrySnapshot(ParallelDiffEntry entry)
    {
        return $"{entry.Path}|{entry.ParentPath ?? "<root>"}|{entry.Kind}|{entry}";
    }

    /// <summary>
    /// 投影文脈を記録し、指定された規則で投影結果を返すテスト用投影器です。
    /// </summary>
    private sealed class RecordingProjector : IParallelDiffPathProjector
    {
        private readonly Func<ParallelDiffPathProjectionContext, ParallelDiffPathSegmentProjection> _project;

        /// <summary>
        /// 指定した投影規則を使用するテスト用投影器を生成します。
        /// </summary>
        public RecordingProjector(
            Func<ParallelDiffPathProjectionContext, ParallelDiffPathSegmentProjection> project)
        {
            _project = project;
        }

        /// <summary>
        /// 投影時に受け取った文脈を取得します。
        /// </summary>
        public List<ParallelDiffPathProjectionContext> Contexts { get; } = [];

        /// <summary>
        /// 文脈を記録してから、指定された投影規則の結果を返します。
        /// </summary>
        public ParallelDiffPathSegmentProjection Project(
            ParallelDiffPathProjectionContext context)
        {
            Contexts.Add(context);
            return _project(context);
        }
    }

    /// <summary>
    /// ordinal container を持つテスト用文書です。
    /// </summary>
    public sealed class OrdinalDocument
    {
        /// <summary>
        /// 順序で識別されるテスト用項目の一覧を取得または設定します。
        /// </summary>
        public List<OrdinalItem> Items { get; init; } = [];
    }

    /// <summary>
    /// ordinal container 内のテスト用項目です。
    /// </summary>
    public sealed class OrdinalItem
    {
        /// <summary>
        /// 差分を発生させるテスト用の名称を取得または設定します。
        /// </summary>
        public string Name { get; init; } = string.Empty;
    }

    /// <summary>
    /// optional container の存在差分を表すテスト用文書です。
    /// </summary>
    public sealed class OptionalDocument
    {
        /// <summary>
        /// container presence差分を表す任意の順序付き項目一覧を取得または設定します。
        /// </summary>
        public List<OrdinalItem>? Items { get; init; }
    }

    /// <summary>
    /// key 付き container を持つテスト用文書です。
    /// </summary>
    public sealed class KeyedDocument
    {
        /// <summary>
        /// 比較 key を持つテスト用項目の一覧を取得または設定します。
        /// </summary>
        public List<KeyedItem> Items { get; init; } = [];
    }

    /// <summary>
    /// key 付き container 内のテスト用項目です。
    /// </summary>
    public sealed class KeyedItem
    {
        /// <summary>
        /// container要素を位置合わせする比較 key を取得または設定します。
        /// </summary>
        [CompareKey]
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 差分を発生させるテスト用の名称を取得または設定します。
        /// </summary>
        public string Name { get; init; } = string.Empty;
    }
}
