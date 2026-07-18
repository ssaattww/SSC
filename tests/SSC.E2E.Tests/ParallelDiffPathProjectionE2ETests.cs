using SSC;

namespace SSC.E2E.Tests;

/// <summary>
/// 再帰 model に対する差分 entry path 投影の end-to-end 契約を検証します。
/// </summary>
public sealed class ParallelDiffPathProjectionE2ETests
{
    /// <summary>
    /// runtime 名で再帰 path を投影し、pattern と照合できることを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_UsesRuntimeNamesForRecursiveModelAndMatchesPattern()
    {
        var result = ParallelCompareApi.Compare(CreateNamedDocuments("0", "1"));
        var projector = new CommonNamePathProjector();

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));

        Assert.Equal(
            "Root.Children[0].Children[0].Fields[0].Value",
            projection.Entry.Path);
        Assert.Equal(
            "Root.Children[0].Children[0].Fields[0]",
            projection.Entry.ParentPath);
        Assert.Equal(
            "Root.Child1[0].Child2[0].Attribute1[0].Value",
            projection.ProjectedPath);
        Assert.Equal(
            "Root.Child1[0].Child2[0].Attribute1[0]",
            projection.ProjectedParentPath);

        Assert.NotNull(projection.Entry.Node);
        Assert.Same(projection.Entry.Node, result.GetNodeByPath(projection.Entry.Path));
        Assert.Null(result.GetNodeByPath(projection.ProjectedPath));

        var pattern = ParallelDiffPathPattern.Parse(
            "Root.Child1[*].Child2[*].Attribute1[*].Value");
        Assert.True(projection.PathMatches(pattern));
        Assert.False(projection.Entry.PathMatches(pattern));

        var finalContext = projector.Contexts[^1];
        Assert.Equal("Value", finalContext.Current.StandardSegment.MemberName);
        Assert.Equal(
            ["Root", "Children", "Children", "Fields"],
            finalContext.Ancestors
                .Select(context => context.StandardSegment.MemberName)
                .ToArray());
    }

    /// <summary>
    /// runtime 名が一致しない場合は標準 segment を維持することを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_FallsBackToStandardSegmentWhenRuntimeNamesDiffer()
    {
        var left = CreateNamedDocument("0", secondNodeName: "Child2");
        var right = CreateNamedDocument("1", secondNodeName: "ChildX");
        var result = ParallelCompareApi.Compare([left, right]);
        var projector = new CommonNamePathProjector();

        var projection = Assert.Single(
            result.GetDiffEntryPathProjections(projector),
            candidate => candidate.Entry.Path.EndsWith(".Value", StringComparison.Ordinal));

        Assert.Equal(
            "Root.Children[0].Children[0].Fields[0].Value",
            projection.Entry.Path);
        Assert.Equal(
            "Root.Child1[0].Children[0].Attribute1[0].Value",
            projection.ProjectedPath);
    }

    /// <summary>
    /// key 付き container の名前変更で key 選択子を維持することを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_PreservesKeySelectorWhenRenamingKeyedContainer()
    {
        var result = ParallelCompareApi.Compare(
        [
            new KeyedDocument
            {
                Items =
                [
                    new KeyedItem { Id = 100, Name = "Temperature", Value = 1 },
                ],
            },
            new KeyedDocument
            {
                Items =
                [
                    new KeyedItem { Id = 100, Name = "Temperature", Value = 2 },
                ],
            },
        ]);

        var projection = Assert.Single(
            result.GetDiffEntryPathProjections(new CommonNamePathProjector()));

        Assert.Equal("Items[100].Value", projection.Entry.Path);
        Assert.Equal("Temperature[100].Value", projection.ProjectedPath);
        Assert.Equal("Temperature[100]", projection.ProjectedParentPath);
    }

    /// <summary>
    /// 要素 node を持たない container presence の文脈を投影器へ渡すことを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_ProvidesContainerPresenceContextWithoutElementNode()
    {
        var result = ParallelCompareApi.Compare(
        [
            new OptionalDocument { Items = [] },
            new OptionalDocument { Items = null },
        ]);
        var projector = new CommonNamePathProjector();

        var projection = Assert.Single(result.GetDiffEntryPathProjections(projector));
        var context = Assert.Single(projector.Contexts);

        Assert.Equal(ParallelDiffEntryKind.ContainerPresence, projection.Entry.Kind);
        Assert.Equal("Items", projection.Entry.Path);
        Assert.Equal("Items", projection.ProjectedPath);
        Assert.Null(projection.ProjectedParentPath);
        Assert.Null(context.Current.Node);
        Assert.Empty(context.Current.Siblings);
        Assert.Empty(context.Ancestors);
    }

    /// <summary>
    /// 投影処理が標準 entry と比較結果を変更しないことを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_DoesNotChangeStandardEntriesOrComparisonResult()
    {
        var result = ParallelCompareApi.Compare(CreateNamedDocuments("0", "1"));
        var before = result.GetDiffEntries();
        var originalRoot = result.Root;
        var originalIssues = result.Issues;
        var originalHasError = result.HasError;

        var projections = result.GetDiffEntryPathProjections(new CommonNamePathProjector());
        var after = result.GetDiffEntries();

        Assert.Equal(
            before.Select(ToEntrySnapshot),
            projections.Select(projection => ToEntrySnapshot(projection.Entry)));
        Assert.Equal(before.Select(ToEntrySnapshot), after.Select(ToEntrySnapshot));
        Assert.Same(originalRoot, result.Root);
        Assert.Same(originalIssues, result.Issues);
        Assert.Equal(originalHasError, result.HasError);
        Assert.True(Assert.IsAssignableFrom<IParallelNode>(result.Root).HasDifferences());
    }

    /// <summary>
    /// 等価な model では投影 entry を返さないことを確認します。
    /// </summary>
    [Fact]
    public void GetDiffEntryPathProjections_ReturnsEmptyForEqualModels()
    {
        var model = CreateNamedDocument("0", secondNodeName: "Child2");
        var result = ParallelCompareApi.Compare([model, model]);
        var projector = new CommonNamePathProjector();

        Assert.Empty(result.GetDiffEntryPathProjections(projector));
        Assert.Empty(projector.Contexts);
    }

    private static IReadOnlyList<NamedDocument> CreateNamedDocuments(
        string leftValue,
        string rightValue)
    {
        return
        [
            CreateNamedDocument(leftValue, secondNodeName: "Child2"),
            CreateNamedDocument(rightValue, secondNodeName: "Child2"),
        ];
    }

    private static NamedDocument CreateNamedDocument(
        string value,
        string secondNodeName)
    {
        return new NamedDocument
        {
            Root = new NamedNode
            {
                Name = "Root",
                Children =
                [
                    new NamedNode
                    {
                        Name = "Child1",
                        Children =
                        [
                            new NamedNode
                            {
                                Name = secondNodeName,
                                Fields =
                                [
                                    new NamedValue
                                    {
                                        Name = "Attribute1",
                                        Value = value,
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };
    }

    private static string ToEntrySnapshot(ParallelDiffEntry entry)
    {
        return $"{entry.Path}|{entry.ParentPath ?? "<root>"}|{entry.Kind}|{entry}";
    }

    /// <summary>
    /// model slot 間で共通する runtime 名を path segment 名へ使用するテスト用投影器です。
    /// </summary>
    private sealed class CommonNamePathProjector : IParallelDiffPathProjector
    {
        /// <summary>
        /// 投影時に受け取った文脈を取得します。
        /// </summary>
        public List<ParallelDiffPathProjectionContext> Contexts { get; } = [];

        /// <summary>
        /// 現在の node 文脈から全model slotで共通する名称を取得し、対応する標準 segment を置換します。
        /// </summary>
        public ParallelDiffPathSegmentProjection Project(
            ParallelDiffPathProjectionContext context)
        {
            Contexts.Add(context);

            return context.Current.StandardSegment.MemberName switch
            {
                "Children" => ProjectName<NamedNode>(context, node => node.Name),
                "Fields" => ProjectName<NamedValue>(context, field => field.Name),
                "Items" => ProjectName<KeyedItem>(context, item => item.Name),
                _ => ParallelDiffPathSegmentProjection.KeepStandard(),
            };
        }

        private static ParallelDiffPathSegmentProjection ProjectName<T>(
            ParallelDiffPathProjectionContext context,
            Func<T, string> getName)
        {
            if (context.Current.Node is not IParallelNode node)
            {
                return ParallelDiffPathSegmentProjection.KeepStandard();
            }

            var name = TryGetCommonName(node, getName);
            return name is null
                ? ParallelDiffPathSegmentProjection.KeepStandard()
                : ParallelDiffPathSegmentProjection.Replace(
                    context.Current.StandardSegment.WithMemberName(name));
        }

        private static string? TryGetCommonName<T>(
            IParallelNode node,
            Func<T, string> getName)
        {
            string? commonName = null;
            var found = false;

            for (var modelIndex = 0; modelIndex < node.Count; modelIndex++)
            {
                if (node.GetState(modelIndex) == ValueState.Missing)
                {
                    continue;
                }

                if (node.GetValue(modelIndex) is not T value)
                {
                    return null;
                }

                var candidate = getName(value);
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return null;
                }

                if (!found)
                {
                    commonName = candidate;
                    found = true;
                    continue;
                }

                if (!string.Equals(commonName, candidate, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return found ? commonName : null;
        }
    }

    /// <summary>
    /// 再帰的な node 名を利用側定義 path へ投影するためのテスト用文書です。
    /// </summary>
    public sealed class NamedDocument
    {
        /// <summary>
        /// 再帰的に投影するルート node を取得または設定します。
        /// </summary>
        public NamedNode Root { get; init; } = new();
    }

    /// <summary>
    /// 子 node と属性値を再帰的に保持するテスト用 node です。
    /// </summary>
    public sealed class NamedNode
    {
        /// <summary>
        /// 投影時にchild segment名として使用する node 名を取得または設定します。
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 再帰的に比較する子 node 一覧を取得または設定します。
        /// </summary>
        public List<NamedNode> Children { get; init; } = [];

        /// <summary>
        /// 投影時に属性segment名として使用する値一覧を取得または設定します。
        /// </summary>
        public List<NamedValue> Fields { get; init; } = [];
    }

    /// <summary>
    /// 利用側定義 path の属性名と差分値を保持するテスト用値です。
    /// </summary>
    public sealed class NamedValue
    {
        /// <summary>
        /// 投影時にfield segment名として使用する値の名称を取得または設定します。
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 差分を発生させるテスト用の値を取得または設定します。
        /// </summary>
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>
    /// 比較 key を持つcontainer投影を検証するテスト用文書です。
    /// </summary>
    public sealed class KeyedDocument
    {
        /// <summary>
        /// 比較 key を持つテスト用項目の一覧を取得または設定します。
        /// </summary>
        public List<KeyedItem> Items { get; init; } = [];
    }

    /// <summary>
    /// 比較 key、投影名、および差分値を保持するテスト用項目です。
    /// </summary>
    public sealed class KeyedItem
    {
        /// <summary>
        /// container要素を位置合わせする比較 key を取得または設定します。
        /// </summary>
        [CompareKey]
        public int Id { get; init; }

        /// <summary>
        /// 投影時にcontainer segment名として使用する項目名を取得または設定します。
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 差分を発生させるテスト用の数値を取得または設定します。
        /// </summary>
        public int Value { get; init; }
    }

    /// <summary>
    /// container presence差分の投影文脈を検証するテスト用文書です。
    /// </summary>
    public sealed class OptionalDocument
    {
        /// <summary>
        /// container presence差分を表す任意の項目一覧を取得または設定します。
        /// </summary>
        public List<KeyedItem>? Items { get; init; }
    }
}
