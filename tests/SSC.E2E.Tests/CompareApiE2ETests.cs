using SSC;

namespace SSC.E2E.Tests;

public sealed class CompareApiE2ETests
{
    [Fact]
    public void Compare_WithEmptyModels_RecordsInputIssueInNonStrictMode()
    {
        // Intent: 入力モデルが空の場合は strict=false で Issue 蓄積し、Root を生成しない。
        var result = ParallelCompareApi.Compare(Array.Empty<SimpleRoot>());

        Assert.True(result.HasError);
        Assert.Null(result.Root);
        Assert.Contains(result.Issues, issue => issue.Code == CompareIssueCode.InputModelListEmpty);
    }

    [Fact]
    public void Compare_WithEmptyModels_ResultAsDynamicReturnsNull()
    {
        // Intent: CompareResult 入口の AsDynamic でも Root 未生成時は null を返す。
        var result = ParallelCompareApi.Compare(Array.Empty<SimpleRoot>());

        dynamic? root = result.AsDynamic();

        Assert.Null(root);
    }

    [Fact]
    public void Compare_WithEmptyModels_ThrowsInputExceptionInStrictMode()
    {
        // Intent: 入力モデルが空の場合は strict=true で入力例外を即時送出する。
        var configuration = new CompareConfiguration { StrictMode = true };

        var exception = Assert.Throws<CompareInputException>(() =>
            ParallelCompareApi.Compare(Array.Empty<SimpleRoot>(), configuration));

        Assert.Equal(CompareIssueCode.InputModelListEmpty, exception.Code);
    }

    [Fact]
    public void Compare_WhenIgnoredSequenceHasNoCompareKey_DoesNotRaiseIssue()
    {
        // Intent: [CompareIgnore] が付いたメンバーはキー検証対象から除外する。
        var models = new[]
        {
            new IgnoreContainerRoot
            {
                ValidItems =
                [
                    new KeyedItem { Id = 1, Value = 10 },
                ],
                IgnoredItems =
                [
                    new NonKeyedItem { Value = 11 },
                ],
            },
            new IgnoreContainerRoot
            {
                ValidItems =
                [
                    new KeyedItem { Id = 1, Value = 20 },
                ],
                IgnoredItems =
                [
                    new NonKeyedItem { Value = 22 },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == CompareIssueCode.CompareKeyNotFoundOnSequenceElement);
    }

    [Fact]
    public void Compare_WhenSequenceElementHasNoCompareKey_RecordsErrorAndSkips()
    {
        // Intent: List 要素に [CompareKey] が無い場合は Error を記録し、その配下ノードをスキップする。
        var models = new[]
        {
            new NonKeyedRoot
            {
                Items =
                [
                    new NonKeyedItem { Value = 1 },
                ],
            },
            new NonKeyedRoot
            {
                Items =
                [
                    new NonKeyedItem { Value = 2 },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        Assert.True(result.HasError);
        Assert.Contains(result.Issues, issue => issue.Code == CompareIssueCode.CompareKeyNotFoundOnSequenceElement);

        var root = Assert.IsType<ParallelNode<NonKeyedRoot>>(result.Root);
        Assert.Empty(root.GetChildren<NonKeyedItem>(nameof(NonKeyedRoot.Items)));
    }

    [Fact]
    public void Compare_WhenTraceLogEnabled_EmitsListContainerClassification()
    {
        // Intent: List 宣言プロパティは trace から List 分類と CompareKey 解決を確認できる。
        var logs = new List<string>();
        var models = new[]
        {
            new KeyedRoot
            {
                Items =
                [
                    new KeyedItem { Id = 1, Value = 10 },
                ],
            },
            new KeyedRoot
            {
                Items =
                [
                    new KeyedItem { Id = 1, Value = 20 },
                ],
            },
        };

        var configuration = new CompareConfiguration { TraceLog = logs.Add };

        _ = ParallelCompareApi.Compare(models, configuration);

        Assert.Contains(logs, line =>
            line.Contains("path=KeyedRoot.Items", StringComparison.Ordinal)
            && line.Contains("container=List", StringComparison.Ordinal)
            && line.Contains("declaredType=System.Collections.Generic.List", StringComparison.Ordinal));
        Assert.Contains(logs, line =>
            line.Contains("path=KeyedRoot.Items", StringComparison.Ordinal)
            && line.Contains("compareKey=Id", StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_WhenTraceLogEnabled_EmitsEnumerableMaterializationDetail()
    {
        // Intent: IEnumerable 宣言プロパティは runtime type と materialize 件数を trace で確認できる。
        var logs = new List<string>();
        var left = new CountingEnumerable<KeyedItem>(
        [
            new KeyedItem { Id = 1, Value = 10 },
            new KeyedItem { Id = 2, Value = 20 },
        ]);
        var right = new CountingEnumerable<KeyedItem>(
        [
            new KeyedItem { Id = 1, Value = 30 },
        ]);
        var models = new[]
        {
            new EnumerableRoot { Items = left },
            new EnumerableRoot { Items = right },
        };

        var configuration = new CompareConfiguration { TraceLog = logs.Add };

        _ = ParallelCompareApi.Compare(models, configuration);

        Assert.Contains(logs, line =>
            line.Contains("path=EnumerableRoot.Items", StringComparison.Ordinal)
            && line.Contains("container=IEnumerable", StringComparison.Ordinal)
            && line.Contains("runtimeType=", StringComparison.Ordinal)
            && line.Contains("CountingEnumerable", StringComparison.Ordinal));
        Assert.Contains(logs, line =>
            line.Contains("path=EnumerableRoot.Items", StringComparison.Ordinal)
            && line.Contains("materializedCount=2", StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_WhenTraceLogEnabled_EmitsArrayContainerClassification()
    {
        // Intent: 配列宣言プロパティは trace から Array 分類と runtime type を確認できる。
        var logs = new List<string>();
        var models = new[]
        {
            new ArrayRoot
            {
                Items =
                [
                    new KeyedItem { Id = 1, Value = 10 },
                    new KeyedItem { Id = 2, Value = 20 },
                ],
            },
            new ArrayRoot
            {
                Items =
                [
                    new KeyedItem { Id = 1, Value = 30 },
                ],
            },
        };

        var configuration = new CompareConfiguration { TraceLog = logs.Add };

        _ = ParallelCompareApi.Compare(models, configuration);

        Assert.Contains(logs, line =>
            line.Contains("path=ArrayRoot.Items", StringComparison.Ordinal)
            && line.Contains("container=Array", StringComparison.Ordinal)
            && line.Contains("declaredType=", StringComparison.Ordinal)
            && line.Contains("KeyedItem[]", StringComparison.Ordinal));
        Assert.Contains(logs, line =>
            line.Contains("path=ArrayRoot.Items", StringComparison.Ordinal)
            && line.Contains("runtimeType=", StringComparison.Ordinal)
            && line.Contains("KeyedItem[]", StringComparison.Ordinal)
            && line.Contains("materializedCount=2", StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_WhenTraceLogEnabled_EmitsDictionaryContainerClassification()
    {
        // Intent: Dictionary 宣言プロパティは trace から Dictionary 分類と key/value 型を確認できる。
        var logs = new List<string>();
        var models = new[]
        {
            new DictionaryRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["A"] = 10,
                },
            },
            new DictionaryRoot
            {
                Scores = new Dictionary<string, int>
                {
                    ["A"] = 12,
                    ["B"] = 20,
                },
            },
        };

        var configuration = new CompareConfiguration { TraceLog = logs.Add };

        _ = ParallelCompareApi.Compare(models, configuration);

        Assert.Contains(logs, line =>
            line.Contains("path=DictionaryRoot.Scores", StringComparison.Ordinal)
            && line.Contains("container=Dictionary", StringComparison.Ordinal)
            && line.Contains("keyType=System.String", StringComparison.Ordinal)
            && line.Contains("valueType=System.Int32", StringComparison.Ordinal));
        Assert.Contains(logs, line =>
            line.Contains("path=DictionaryRoot.Scores", StringComparison.Ordinal)
            && line.Contains("runtimeType=System.Collections.Generic.Dictionary", StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_WhenDuplicateSequenceKeyDetected_ThrowsExecutionExceptionInStrictMode()
    {
        // Intent: 同一 model 内で CompareKey 重複時は strict=true で実行例外を送出する。
        var models = new[]
        {
            new KeyedRoot
            {
                Items =
                [
                    new KeyedItem { Id = 1, Value = 10 },
                    new KeyedItem { Id = 1, Value = 99 },
                ],
            },
            new KeyedRoot
            {
                Items =
                [
                    new KeyedItem { Id = 1, Value = 12 },
                ],
            },
        };

        var configuration = new CompareConfiguration { StrictMode = true };

        var exception = Assert.Throws<CompareExecutionException>(() => ParallelCompareApi.Compare(models, configuration));
        Assert.Equal(CompareIssueCode.DuplicateCompareKeyDetected, exception.Code);
    }

    [Fact]
    public void Compare_WhenDuplicateSequenceKeyDetected_RecordsIssueWithPathModelIndexAndKeyText()
    {
        // Intent: キー重複 Issue には Path/ModelIndex/KeyText を必須情報として格納する。
        var models = new[]
        {
            new StringKeyRoot
            {
                Items =
                [
                    new StringKeyedItem { Id = "dup", Value = 10 },
                    new StringKeyedItem { Id = "dup", Value = 11 },
                ],
            },
            new StringKeyRoot
            {
                Items =
                [
                    new StringKeyedItem { Id = "base", Value = 20 },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        var issue = Assert.Single(result.Issues.Where(item => item.Code == CompareIssueCode.DuplicateCompareKeyDetected));
        Assert.Equal("StringKeyRoot.Items", issue.Path);
        Assert.Equal(0, issue.ModelIndex);
        Assert.Equal("dup", issue.KeyText);
    }

    [Fact]
    public void Compare_WhenSequenceCompareKeyIsNull_RecordsIssueWithNullKeyText()
    {
        // Intent: CompareKey が null の場合でも Issue にキー情報を可視化するため KeyText を設定する。
        var models = new[]
        {
            new NullableKeyRoot
            {
                Items =
                [
                    new NullableKeyedItem { Id = null, Value = 10 },
                ],
            },
            new NullableKeyRoot
            {
                Items =
                [
                    new NullableKeyedItem { Id = "base", Value = 20 },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        var issue = Assert.Single(result.Issues.Where(item => item.Code == CompareIssueCode.CompareKeyValueIsNull));
        Assert.Equal("NullableKeyRoot.Items", issue.Path);
        Assert.Equal(0, issue.ModelIndex);
        Assert.Equal("<null>", issue.KeyText);
    }

    [Fact]
    public void Compare_WhenSequenceElementIsNull_RecordsIssueWithNullKeyText()
    {
        // Intent: sequence element が null の場合も KeyText を <null> に統一して診断情報を揃える。
        var models = new[]
        {
            new KeyedRoot
            {
                Items =
                [
                    null!,
                    new KeyedItem { Id = 1, Value = 10 },
                ],
            },
            new KeyedRoot
            {
                Items =
                [
                    new KeyedItem { Id = 1, Value = 20 },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);

        var issue = Assert.Single(result.Issues.Where(item => item.Code == CompareIssueCode.CompareKeyValueIsNull));
        Assert.Equal("KeyedRoot.Items", issue.Path);
        Assert.Equal(0, issue.ModelIndex);
        Assert.Equal("<null>", issue.KeyText);
    }

    [Fact]
    public void Compare_WithEnumerableProperty_MaterializesEachModelExactlyOnce()
    {
        // Intent: IEnumerable 入力は model ごとに1回だけ列挙し、再列挙しない。
        var left = new CountingEnumerable<KeyedItem>(
        [
            new KeyedItem { Id = 1, Value = 10 },
            new KeyedItem { Id = 2, Value = 20 },
        ]);
        var right = new CountingEnumerable<KeyedItem>(
        [
            new KeyedItem { Id = 1, Value = 30 },
        ]);
        var models = new[]
        {
            new EnumerableRoot { Items = left },
            new EnumerableRoot { Items = right },
        };

        var result = ParallelCompareApi.Compare(models);
        var root = Assert.IsType<ParallelNode<EnumerableRoot>>(result.Root);
        var items = root.Children(model => model.Items);

        Assert.Equal(1, left.EnumerationCount);
        Assert.Equal(1, right.EnumerationCount);
        Assert.Equal(["1", "2"], items.Select(item => item.KeyText).ToArray());

        // Intent: union 済み Items[index] ごとに model slot を参照できる（Items[index] x modelIndex）。
        Assert.Equal(10, items[0][0]?.Value);
        Assert.Equal(20, items[1][0]?.Value);
        Assert.Equal(30, items[0][1]?.Value);
        Assert.Equal(ValueState.Missing, items[1].GetState(1));
    }

    [Fact]
    public void Compare_WithListMemberAcrossModels_BuildsUnionAndPreservesModelSlots()
    {
        // Intent: モデル直下の List メンバーで、両 model が複数要素を持つ場合も key union と slot 対応を維持する。
        var models = new[]
        {
            new KeyedRoot
            {
                Items =
                [
                    new KeyedItem { Id = 1, Value = 10 },
                    new KeyedItem { Id = 2, Value = 20 },
                ],
            },
            new KeyedRoot
            {
                Items =
                [
                    new KeyedItem { Id = 2, Value = 200 },
                    new KeyedItem { Id = 3, Value = 300 },
                ],
            },
        };

        var result = ParallelCompareApi.Compare(models);
        var root = Assert.IsType<ParallelNode<KeyedRoot>>(result.Root);
        var items = root.Children(model => model.Items);

        Assert.Equal(["1", "2", "3"], items.Select(item => item.KeyText).ToArray());

        Assert.Equal(10, items[0][0]?.Value);
        Assert.Equal(ValueState.Missing, items[0].GetState(1));

        Assert.Equal(20, items[1][0]?.Value);
        Assert.Equal(200, items[1][1]?.Value);

        Assert.Equal(ValueState.Missing, items[2].GetState(0));
        Assert.Equal(300, items[2][1]?.Value);
    }

    public sealed class SimpleRoot
    {
        public int Value { get; init; }
    }

    public sealed class NonKeyedRoot
    {
        public List<NonKeyedItem> Items { get; init; } = [];
    }

    public sealed class KeyedRoot
    {
        public List<KeyedItem> Items { get; init; } = [];
    }

    public sealed class IgnoreContainerRoot
    {
        public List<KeyedItem> ValidItems { get; init; } = [];

        [CompareIgnore]
        public List<NonKeyedItem> IgnoredItems { get; init; } = [];
    }

    public sealed class EnumerableRoot
    {
        public IEnumerable<KeyedItem> Items { get; init; } = Array.Empty<KeyedItem>();
    }

    public sealed class StringKeyRoot
    {
        public List<StringKeyedItem> Items { get; init; } = [];
    }

    public sealed class ArrayRoot
    {
        public KeyedItem[] Items { get; init; } = [];
    }

    public sealed class DictionaryRoot
    {
        public Dictionary<string, int> Scores { get; init; } = [];
    }

    public sealed class NullableKeyRoot
    {
        public List<NullableKeyedItem> Items { get; init; } = [];
    }

    public sealed class NonKeyedItem
    {
        public int Value { get; init; }
    }

    public sealed class KeyedItem
    {
        [CompareKey]
        public int Id { get; init; }

        public int Value { get; init; }
    }

    public sealed class StringKeyedItem
    {
        [CompareKey]
        public string Id { get; init; } = string.Empty;

        public int Value { get; init; }
    }

    public sealed class NullableKeyedItem
    {
        [CompareKey]
        public string? Id { get; init; }

        public int Value { get; init; }
    }

    private sealed class CountingEnumerable<T>(IReadOnlyList<T> items) : IEnumerable<T>
    {
        private readonly IReadOnlyList<T> _items = items;

        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            return _items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
