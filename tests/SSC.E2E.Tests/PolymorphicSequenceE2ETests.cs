using SSC;

namespace SSC.E2E.Tests;

/// <summary>
/// 宣言された要素型と実行時型が異なるシーケンスの比較動作を検証します。
/// </summary>
public sealed class PolymorphicSequenceE2ETests
{
    /// <summary>
    /// 同じ派生型を持つ位置合わせ済み要素が、宣言されたノード型を保持しながら実行時メンバーを比較することを検証します。
    /// </summary>
    [Fact]
    public void Compare_WhenAlignedElementsShareDerivedType_UsesRuntimeMembersAndPreservesDeclaredNodeType()
    {
        var models = new[]
        {
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "left" },
                ],
            },
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "right" },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        Assert.False(result.HasError);
        var root = Assert.IsType<ParallelNode<PolymorphicRoot>>(result.Root);
        var item = Assert.Single(root.GetChildren<PolymorphicItem>(nameof(PolymorphicRoot.Items)));
        Assert.IsType<PolymorphicNode>(item[0]);
        Assert.IsType<PolymorphicNode>(item[1]);
        Assert.Equal(ValueState.Mismatched, item.GetState(0));
        Assert.Equal(ValueState.Mismatched, item.GetState(1));

        var entry = Assert.Single(result.GetDiffEntries());
        Assert.Equal("Items[0].Name", entry.Path);
    }

    /// <summary>
    /// 一致する派生型を持つ入れ子要素が実行時メンバーを再帰的に比較することを検証します。
    /// </summary>
    [Fact]
    public void Compare_WhenNestedElementsShareDerivedTypes_UsesRuntimeMembersRecursively()
    {
        var models = new[]
        {
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode
                    {
                        Name = "node",
                        Children =
                        [
                            new PolymorphicContent { Text = "left" },
                        ],
                    },
                ],
            },
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode
                    {
                        Name = "node",
                        Children =
                        [
                            new PolymorphicContent { Text = "right" },
                        ],
                    },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        var entry = Assert.Single(result.GetDiffEntries());
        Assert.Equal("Items[0].Children[0].Text", entry.Path);
    }

    /// <summary>
    /// 実行時型が異なる位置合わせ済み要素が、子メンバーを比較せず要素の不一致を報告することを検証します。
    /// </summary>
    [Fact]
    public void Compare_WhenAlignedElementsHaveDifferentRuntimeTypes_ReportsElementDifferenceWithoutDescending()
    {
        var models = new[]
        {
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "same" },
                ],
            },
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicContent { Text = "same" },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        Assert.False(result.HasError);
        var root = Assert.IsType<ParallelNode<PolymorphicRoot>>(result.Root);
        var item = Assert.Single(root.GetChildren<PolymorphicItem>(nameof(PolymorphicRoot.Items)));
        Assert.Empty(item.GetDirectChildren());
        Assert.Equal(ValueState.Mismatched, item.GetState(0));
        Assert.Equal(ValueState.Mismatched, item.GetState(1));

        var entry = Assert.Single(result.GetDiffEntries());
        Assert.Equal("Items[0]", entry.Path);
        Assert.IsType<PolymorphicNode>(entry.Values[0].Value);
        Assert.IsType<PolymorphicContent>(entry.Values[1].Value);
        Assert.All(entry.Values, value => Assert.Equal(ValueState.Mismatched, value.State));
    }

    /// <summary>
    /// nullおよび欠損のポリモーフィック要素が既存の存在状態の動作を維持することを検証します。
    /// </summary>
    [Fact]
    public void Compare_WhenPolymorphicElementIsNullOrMissing_PreservesExistingPresenceStates()
    {
        var valueAndNull = ParallelCompareApi.Compare(
        [
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "node" },
                ],
            },
            new PolymorphicRoot
            {
                Items =
                [
                    null,
                ],
            },
        ]);

        var nullEntry = Assert.Single(valueAndNull.GetDiffEntries());
        Assert.Equal("Items[0]", nullEntry.Path);
        Assert.Equal(ValueState.Mismatched, nullEntry.Values[0].State);
        Assert.Equal(ValueState.Mismatched, nullEntry.Values[1].State);

        var valueAndMissing = ParallelCompareApi.Compare(
        [
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "node" },
                ],
            },
            new PolymorphicRoot(),
        ]);

        var missingEntry = Assert.Single(valueAndMissing.GetDiffEntries());
        Assert.Equal("Items[0]", missingEntry.Path);
        Assert.Equal(ValueState.Mismatched, missingEntry.Values[0].State);
        Assert.Equal(ValueState.Missing, missingEntry.Values[1].State);
    }

    /// <summary>
    /// 一致する派生型を持つキー付き要素が、キーによる位置合わせを維持しながら実行時メンバーを比較することを検証します。
    /// </summary>
    [Fact]
    public void Compare_WhenKeyedElementsShareDerivedType_PreservesKeyAlignmentAndUsesRuntimeMembers()
    {
        var models = new[]
        {
            new KeyedPolymorphicRoot
            {
                Items =
                [
                    new KeyedPolymorphicValue { Id = 1, Value = 10 },
                ],
            },
            new KeyedPolymorphicRoot
            {
                Items =
                [
                    new KeyedPolymorphicValue { Id = 1, Value = 20 },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        var root = Assert.IsType<ParallelNode<KeyedPolymorphicRoot>>(result.Root);
        var item = Assert.Single(root.GetChildren<KeyedPolymorphicItem>(nameof(KeyedPolymorphicRoot.Items)));
        Assert.Equal("1", item.KeyText);

        var entry = Assert.Single(result.GetDiffEntries());
        Assert.Equal("Items[1].Value", entry.Path);
    }

    /// <summary>
    /// トレース出力が宣言されたノード型と実行時の比較型の両方を識別することを検証します。
    /// </summary>
    [Fact]
    public void Compare_WhenTraceEnabled_ReportsDeclaredNodeAndRuntimeComparisonTypes()
    {
        var logs = new List<string>();
        var configuration = new CompareConfiguration { TraceLog = logs.Add };
        var models = new[]
        {
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "left" },
                ],
            },
            new PolymorphicRoot
            {
                Items =
                [
                    new PolymorphicNode { Name = "right" },
                ],
            },
        };

        _ = ParallelCompareApi.Compare(models, configuration);

        Assert.Contains(logs, line =>
            line.Contains("nodeType=SSC.E2E.Tests.PolymorphicSequenceE2ETests+PolymorphicItem", StringComparison.Ordinal)
            && line.Contains("comparisonType=SSC.E2E.Tests.PolymorphicSequenceE2ETests+PolymorphicNode", StringComparison.Ordinal));
    }

    /// <summary>
    /// ポリモーフィックなシーケンス要素を公開するテストルートを表します。
    /// </summary>
    public sealed class PolymorphicRoot
    {
        /// <summary>
        /// シーケンステストで比較するポリモーフィックな要素を取得します。
        /// </summary>
        public List<PolymorphicItem?> Items { get; init; } = [];
    }

    /// <summary>
    /// ポリモーフィックなシーケンス要素の宣言上の基底型を定義します。
    /// </summary>
    public abstract class PolymorphicItem
    {
    }

    /// <summary>
    /// 名前と入れ子のポリモーフィックな子要素を持つ要素を表します。
    /// </summary>
    public sealed class PolymorphicNode : PolymorphicItem
    {
        /// <summary>
        /// このポリモーフィックノードで比較する名前を取得します。
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 再帰的に比較する入れ子のポリモーフィックな要素を取得します。
        /// </summary>
        public List<PolymorphicItem?> Children { get; init; } = [];
    }

    /// <summary>
    /// テキスト内容を持つポリモーフィックな要素を表します。
    /// </summary>
    public sealed class PolymorphicContent : PolymorphicItem
    {
        /// <summary>
        /// このポリモーフィックな内容要素で比較するテキストを取得します。
        /// </summary>
        public string Text { get; init; } = string.Empty;
    }

    /// <summary>
    /// キー付きポリモーフィックなシーケンス要素を公開するテストルートを表します。
    /// </summary>
    public sealed class KeyedPolymorphicRoot
    {
        /// <summary>
        /// シーケンステストで比較するキー付きポリモーフィックな要素を取得します。
        /// </summary>
        public List<KeyedPolymorphicItem> Items { get; init; } = [];
    }

    /// <summary>
    /// キー付きポリモーフィックなシーケンス要素の宣言上の基底型を定義します。
    /// </summary>
    public abstract class KeyedPolymorphicItem
    {
        /// <summary>
        /// 比較するモデル間でポリモーフィックな要素を位置合わせするキーを取得します。
        /// </summary>
        [CompareKey]
        public int Id { get; init; }
    }

    /// <summary>
    /// キーによる位置合わせ後に値を比較するキー付きポリモーフィックな要素を表します。
    /// </summary>
    public sealed class KeyedPolymorphicValue : KeyedPolymorphicItem
    {
        /// <summary>
        /// キー付きポリモーフィックな要素で比較する値を取得します。
        /// </summary>
        public int Value { get; init; }
    }
}
