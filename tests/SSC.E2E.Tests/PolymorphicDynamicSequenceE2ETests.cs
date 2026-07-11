using SSC;

namespace SSC.E2E.Tests;

/// <summary>
/// 宣言された要素型と実行時型が異なるシーケンスの動的プロジェクションを検証します。
/// </summary>
public sealed class PolymorphicDynamicSequenceE2ETests
{
    /// <summary>
    /// 動的プロジェクションがポリモーフィックなシーケンスの実行時メンバーを公開し、その不一致状態を保持することを検証します。
    /// </summary>
    [Fact]
    public void Compare_DynamicProjection_RuntimeDerivedPolymorphicSequence_UsesRuntimeMembers()
    {
        var models = new[]
        {
            new DynamicRoot
            {
                Detail = new DynamicDetail
                {
                    Items =
                    [
                        new DynamicContent { Text = "left" },
                    ],
                },
            },
            new DynamicRoot
            {
                Detail = new DynamicDetail
                {
                    Items =
                    [
                        new DynamicContent { Text = "right" },
                    ],
                },
            },
        };

        var result = ParallelCompareApi.Compare(models);
        dynamic root = result.AsDynamic()!;
        dynamic item = root.Detail.Items[0];

        Assert.Equal("left", (string?)item.Text[0]);
        Assert.Equal("right", (string?)item.Text[1]);
        Assert.Equal(ValueState.Mismatched, (ValueState)item.GetState(0));
        Assert.Equal(ValueState.Mismatched, (ValueState)item.GetState(1));
    }

    /// <summary>
    /// ポリモーフィックな詳細オブジェクトを公開するテストルートを表します。
    /// </summary>
    public sealed class DynamicRoot
    {
        /// <summary>
        /// テストが動的プロジェクションで公開するポリモーフィックな詳細オブジェクトを取得します。
        /// </summary>
        public DynamicBase Detail { get; init; } = null!;
    }

    /// <summary>
    /// 動的な詳細オブジェクトの宣言上の基底型を定義します。
    /// </summary>
    public abstract class DynamicBase;

    /// <summary>
    /// ポリモーフィックな要素を含む動的な詳細型を表します。
    /// </summary>
    public sealed class DynamicDetail : DynamicBase
    {
        /// <summary>
        /// 動的プロジェクションを通じて公開するポリモーフィックな要素を取得します。
        /// </summary>
        public List<DynamicItem> Items { get; init; } = [];
    }

    /// <summary>
    /// 動的なポリモーフィック要素の宣言上の基底型を定義します。
    /// </summary>
    public abstract class DynamicItem;

    /// <summary>
    /// テキスト内容を持つ動的なポリモーフィック要素を表します。
    /// </summary>
    public sealed class DynamicContent : DynamicItem
    {
        /// <summary>
        /// 動的プロジェクションを通じて公開するテキストを取得します。
        /// </summary>
        public string Text { get; init; } = string.Empty;
    }
}
