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
        var items = root.GetChildren<KeyedItem>(nameof(EnumerableRoot.Items));

        Assert.Equal(1, left.EnumerationCount);
        Assert.Equal(1, right.EnumerationCount);
        Assert.Equal(["1", "2"], items.Select(item => item.KeyText).ToArray());
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
        var items = root.GetChildren<KeyedItem>(nameof(KeyedRoot.Items));

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
