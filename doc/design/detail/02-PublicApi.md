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
    public static dynamic? AsDynamic<T>(this CompareResult<T> result);
}
```

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GenerateParallelViewAttribute : Attribute
{
}
```

```csharp
// generated (consumer assembly)
internal static class DatasetGeneratedViewExtensions
{
    // non-compare node の場合は ArgumentException
    internal static DatasetParallelView? AsGeneratedView(this CompareResult<Dataset> result);
}
```

## 4. Behavior Contract

- indexer の範囲外アクセスは `ModelIndexOutOfRange`
- dynamic list index の範囲外アクセスも `ModelIndexOutOfRange`
- generated list index の範囲外アクセスも `ModelIndexOutOfRange`
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
dynamic root = result.AsDynamic();
var leftMetricAt100 = root.Groups[0].Items[0].MetricA[0]; // 1.0
var leftItemAt100 = root.Groups[0].Items[0][0]; // Item object
var nodeCount = root.Groups[0].Items[0].NodeCount; // node slot count
```

- `root...Items[index][model]` は要素オブジェクト全体を返す
- `root...Items[index].MetricA[model]` は要素プロパティ値を返す
- `root...Items[index].NodeCount` / `NodeKeyText` は node メタ情報を返す

### 4.1.1 T-042 Dynamic Value-Path `GetState` Scope

T-042 で設計対象にするのは、`AsDynamic()` から辿る value path の `GetState(modelIndex)` のみである。

- 対象:
  - `root.Groups[0].Items[0].MetricA.GetState(modelIndex)`
  - `root.Groups[0].Items[0].Detail.Label.GetState(modelIndex)` のような dynamic nested value path
- 対象外:
  - node 自体の `GetState(modelIndex)`
  - value の indexer 読み取り（`Property[modelIndex]`）
  - generated projection の nested value path 実装

T-042 の dynamic value-path `GetState` は、compare 時に materialize された member state を参照して判定する。
そのため、`GetState` 呼び出し中に member getter を再実行してはならない。

この変更で観測可能な差分は getter 評価タイミングに限定される。
dynamic value-path `GetState` のために必要な getter 評価や例外発生は compare / node construction 時に前倒しされ得るが、
`GetState` 呼び出し時に追加の getter 実行を発生させない。

上記の non-invasive 保証は、compare 時に materialize 済みの member に適用する。
declared type に存在しない runtime-derived 追加メンバーは dynamic access 自体は継続利用できるが、
`GetState` 判定は legacy の runtime reflection fallback を使うため T-042 の保証対象外とする。

深い階層は `Children(...)` を連鎖して辿る。

```csharp
var groups = root.Children(model => model.Groups);
var groupItems = groups[0].Children(model => model.Items);
```

## 4.3 Generated Projection Access Pattern

`dynamic` の代替として、Source Generator で生成される型付き view を利用できる。

```csharp
[GenerateParallelView]
public sealed class Dataset
{
    public List<Group> Groups { get; init; } = [];
}

var root = ParallelCompareApi.Compare(models).AsGeneratedView();

var leftMetricAt100 = root.Groups[0].Items[0].MetricA[0]; // 1.0
var rightStateAt200 = root.Groups[0].Items[1].MetricA.GetState(1); // Missing
var leftLabel = root.Groups[0].Items[0].Detail.Select(x => x.Label)[0]; // nested value path
var nodeCount = root.Groups[0].Items[0].NodeMeta.Count;

// model 単位で list を選択（Missing slot を除外）
var leftGroups = root.Groups.SelectModel(0);
var rightGroups = root.Groups.SelectModel(1);
var leftGroupIdAt0 = leftGroups[0].GroupId[0];
```

- generated view は `CompareResult<T>` の compare result node に対してのみ有効
- generated view で取得する公開 `ValueState` の意味は `AsDynamic()` と同一
- 投影切替の入口は `CompareResult` 拡張に統一する
- generated API の node メタ情報は `NodeMeta` 配下に分離し、モデル同名メンバーと衝突させない
- Dictionary も List と同様に key union 順の index でアクセスする（例: `root.Scores[0][1]`）
- `SelectModel(modelIndex)` は指定 model で `Missing` でない要素のみを返し、順序は key union 順を維持する
- T-042 の non-invasive `GetState` 対応は dynamic value path に限定し、generated nested value path の parity はこの task の対象外とする

## 4.4 Generated Projection Scope (Initial)

初期版の生成対象は次に固定する。

- container path:
  - `IEnumerable<TElement>`, `IReadOnlyDictionary<TKey, TValue>`, `IDictionary<TKey, TValue>`
- value path:
  - `Property[modelIndex]`
  - `Property.GetState(modelIndex)`
  - nested object path は `Select(...)` で連鎖
- out of scope（初期版）:
  - 任意メソッド呼び出しの投影
  - indexer プロパティ投影

## 4.5 Generated Naming and Placement

- 生成コードの配置 namespace は `SSC.Generated`
- 生成型名は fully-qualified name 由来のサニタイズ名を使い、同名型（別 namespace）でも衝突しないようにする
- nested type は containing type 名を連結して一意化する

## 4.2 Nullability and State

- `result.Root` は入力エラー時に `null` になり得る。
- `node[modelIndex]` の値は、欠損または実値 `null` のどちらでも `null` になり得る。
- index が有効な限り、`groups[i]` や `items[j]` のノード自体は通常 `null` ではない。
- 比較状態は `GetState(modelIndex)` で判定する。
  - `Missing`: 当該 slot が欠損、または比較対象がない
  - `Matched`: 当該 slot が存在し、比較対象と一致
  - `Mismatched`: 当該 slot が存在し、比較対象と不一致（比較先欠損を含む）
- T-042 の dynamic value-path `GetState` は compare 時に保持した member state を参照し、状態判定のために getter を再実行しない
- その結果、dynamic value path で観測される getter の副作用や例外は compare / node construction 時に前倒しされ得る
- ただし、この non-invasive 保証は materialize 済み member に限定され、runtime-derived 追加メンバーは legacy reflection fallback を利用する
- generated projection の nested value path は T-042 の対象外であり、この timing 制約は直ちには適用しない

```csharp
var metric = items[1][1]?.MetricA;
var state = items[1].GetState(1); // Missing / Matched / Mismatched

dynamic root = result.AsDynamic();
var objectState = root.Groups[0].Items[1].GetState(1); // Missing / Matched / Mismatched
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
    public Action<string>? TraceLog { get; init; }
}
```

`TraceLog` を指定した場合、`Compare(...)` 実行中に内部 trace 行を同期的に受け取れる。

- 用途:
  - container 判定の確認
  - `List` / `Array` / `IEnumerable` / `Dictionary` の分類経路の確認
  - path 単位の metadata / normalization / issue 発生確認
- 非用途:
  - `CompareResult` への永続保存
  - 構造化ログ基盤の代替

trace 行は人間確認を主目的とし、少なくとも次を含む。

- phase
- path
- declared type
- container category（`Dictionary` / `List` / `Array` / `IEnumerable` / `ScalarOrObject`）
- runtime type（判明時）
- 追加情報（element type、key type、materialized count、compare key 名、issue code など）

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
