# Public API

## 1. Entry Point

```csharp
public static class ParallelCompareApi
{
    public static CompareResult<T> Compare<T>(
        IReadOnlyList<T> models,
        CompareConfiguration? configuration = null);
}
```

## 2. Input Contract

- `models.Count == 0` は `InputModelListEmpty`
- `models` 要素 `null` は `InputModelNullElement`
- `configuration == null` は既定値を適用

## 3. Parallel Node API

```csharp
public interface Parallel<T>
{
    T? this[int modelIndex] { get; }
    int Count { get; }
    bool AllPresent { get; }
    bool AnyPresent { get; }
    ValueState GetState(int modelIndex);
}
```

```csharp
public interface ParallelDataset : Parallel<Dataset>
{
    IEnumerable<ParallelGroup> Groups { get; }
}

public interface ParallelGroup : Parallel<Group>
{
    IEnumerable<ParallelItem> Items { get; }
}

public interface ParallelItem : Parallel<Item>
{
}
```

```csharp
public static class ParallelDynamicAccessExtensions
{
    public static dynamic AsDynamic<T>(this Parallel<T> node);
}
```

## 4. Behavior Contract

- indexer の範囲外アクセスは `ModelIndexOutOfRange`
- dynamic list index の範囲外アクセスも `ModelIndexOutOfRange`
- `AllPresent == Values.All(v => v != null かつ Missing でない)`
- `AnyPresent == Values.Any(v => Missing でない)`

## 4.0 Source Dataset Example

`Children(...)` のアクセス例がどの入力データを前提にしているかを明示するため、
比較前の元データセット例を以下に示す。

```csharp
public sealed class Dataset
{
    public List<Group> Groups { get; init; } = [];
}

public sealed class Group
{
    [CompareKey]
    public int GroupId { get; init; }
    public List<Item> Items { get; init; } = [];
}

public sealed class Item
{
    [CompareKey]
    public int ItemId { get; init; }
    public double MetricA { get; init; }
}

var models = new[]
{
    new Dataset
    {
        Groups =
        [
            new Group
            {
                GroupId = 1,
                Items =
                [
                    new Item { ItemId = 100, MetricA = 1.0 },
                    new Item { ItemId = 200, MetricA = 2.0 },
                ],
            },
        ],
    },
    new Dataset
    {
        Groups =
        [
            new Group
            {
                GroupId = 1,
                Items =
                [
                    new Item { ItemId = 100, MetricA = 10.0 },
                    new Item { ItemId = 300, MetricA = 30.0 },
                ],
            },
        ],
    },
};
```

この入力では `GroupId=1` が対応し、`Items` は `ItemId` の union（`100, 200, 300`）で揃う。

## 4.1 Container Member Access Pattern (Current API)

現行 API では、コンテナ要素は型付き selector の `Children(...)` で取得できる。
既存の `GetChildren<TElement>(memberName)` も後方互換として利用可能。

```csharp
var root = Assert.IsType<ParallelNode<Dataset>>(result.Root);
var groups = root.Children(model => model.Groups);
var items = groups[0].Children(model => model.Items);

// ItemId union は [100, 200, 300]
// Items[index] x modelIndex (0=left, 1=right)
var leftMetricAt100 = items[0][0]?.MetricA; // 1.0
var leftMetricAt200 = items[1][0]?.MetricA; // 2.0
var rightMetricAt100 = items[0][1]?.MetricA; // 10.0
var rightStateAt200 = items[1].GetState(1); // Missing
```

意図する参照記法（概念表現）は次の形。

```csharp
var leftMetricAt100 = root.Groups[0].Items[0].MetricA[0]; // 1.0
```

- 左側の `[]`: List のインデックスアクセス（key union 後の要素順）
- 右側の `[]`: model インデックスアクセス（0=left, 1=right）

上記の記法を実際に使う場合は、`AsDynamic()` で動的投影した root から辿る。

```csharp
dynamic root = result.Root!.AsDynamic();
var leftMetricAt100 = root.Groups[0].Items[0].MetricA[0]; // 1.0
var leftItemAt100 = root.Groups[0].Items[0][0]; // Item object
var nodeCount = root.Groups[0].Items[0].NodeCount; // node slot count
```

- `root...Items[index][model]` は要素オブジェクト全体を返す
- `root...Items[index].MetricA[model]` は要素プロパティ値を返す
- `root...Items[index].NodeCount` / `NodeKeyText` は node メタ情報を返す

深い階層は `Children(...)` を連鎖して辿る。

```csharp
var groups = root.Children(model => model.Groups);
var groupItems = groups[0].Children(model => model.Items);
```

## 4.2 Nullability and State

- `result.Root` は入力エラー時に `null` になり得る。
- `node[modelIndex]` の値は `Missing` または `PresentNull` で `null` になり得る。
- index が有効な限り、`groups[i]` や `items[j]` のノード自体は通常 `null` ではない。
- `null` の意味を区別する場合は `GetState(modelIndex)` を併用する。

```csharp
var metric = items[1][1]?.MetricA;
var state = items[1].GetState(1); // Missing / PresentNull / PresentValue

dynamic root = result.Root!.AsDynamic();
var objectState = root.Groups[0].Items[1].GetState(1); // Missing / PresentNull / PresentValue
```

## 5. Configuration Entry

```csharp
public sealed class CompareConfiguration
{
    public bool StrictMode { get; init; } = false;
    public StringComparison StringKeyComparison { get; init; } = StringComparison.Ordinal;
    public NullKeyPolicy NullKeyPolicy { get; init; } = NullKeyPolicy.Error;
    public MissingCompareKeyListPolicy MissingCompareKeyListPolicy { get; init; } =
        MissingCompareKeyListPolicy.SkipAndRecordError;
    public DuplicateKeyPolicy DuplicateKeyPolicy { get; init; } =
        DuplicateKeyPolicy.RecordError;
}
```

## 6. Result Entry

`Compare` は常に `CompareResult<T>` を返し、成功時は `Root` が設定される。
strict 時は Error 発生で例外送出を許可する。

## 7. Exception Types (Strict Mode)

例外は 2 系統に分ける。

```csharp
public class CompareInputException : Exception
{
    public CompareIssueCode Code { get; }
    public CompareInputException(CompareIssueCode code, string message) : base(message) => Code = code;
}

public class CompareExecutionException : Exception
{
    public CompareIssueCode Code { get; }
    public CompareExecutionException(CompareIssueCode code, string message) : base(message) => Code = code;
}
```

- `CompareInputException`:
  - 入力妥当性違反（空 model、null 要素など）
- `CompareExecutionException`:
  - 正規化・反射・キー処理など実行中エラー
