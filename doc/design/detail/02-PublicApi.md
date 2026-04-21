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
public interface IParallelNode
{
    int Count { get; }
    bool AllPresent { get; }
    bool AnyPresent { get; }
    string? KeyText { get; }
    object? GetValue(int modelIndex);
    ValueState GetState(int modelIndex);
    bool HasDifferences();
    IReadOnlyList<ParallelChildSet> GetDirectChildren();
}

public readonly struct ParallelChildSet
{
    public string Name { get; }
    public IReadOnlyList<IParallelNode> Nodes { get; }
    public bool HasDifferences { get; }
}
```

- `IParallelNode` は既存の公開 interface であり、T-070 の `HasDifferences()` / `GetDirectChildren()` 追加は外部実装者に対する breaking change となる
- 上記 breaking change は `Design/BreakingChanges.md` に記録する

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
- `HasDifferences()` は current node 自体、または配下 subtree のいずれかに差分があれば `true`
- `GetDirectChildren()` は current node の直下 property を `ParallelChildSet` 単位で返す

### 4.0 Direct Child Traversal Contract

`GetDirectChildren()` は「ユーザーが自前で再帰を書くための最小探索プリミティブ」として定義する。

- 返却順:
  - `ParallelChildSet` の順序は comparable property の順序に従う
  - 各 `ParallelChildSet.Nodes` の順序は既存 child access の順序に従う
  - container member では key union / 正規化済み要素順を維持する
- 返却形:
  - scalar/object member は `Nodes.Count == 1`
  - `List` / `Array` / `IEnumerable` / `Dictionary` は正規化済み要素 node 群を `Nodes` に格納する
- `HasDifferences` はその property 自体または `Nodes` 配下に差分がある場合に `true`
- `Name` は source model の property 名そのものを使う
- child を持たない node は空配列を返す
- 親参照は公開しない。必要な再帰・path 組み立てはユーザー側で `Name` と `IParallelNode` を使って行う
- `Name` だけでは container member 配下の複数 child を一意化できないため、path 表現が必要な場合は `childSet.Name` に child 側の識別子を付けて segment を作る
- container member の segment は `child.KeyText` がある場合はそれを優先し、無い場合だけ同一 `ParallelChildSet.Nodes` 内の ordinal index を代替識別子として使う
- 推奨表現は `Items[100]` / `Items[A]` / `Items[#0]` のような `Name[discriminator]` 形式とする

`HasDifferences()` は単なる object slot の参照等価には依存しない独立プリミティブとする。

- leaf/value node では各 model slot の比較結果を使う
- object/container node では direct member node と normalized child node を再帰的に調べる
- object/container node 自身については object 参照等価を使わず、各 model slot の presence category（`Missing` / `PresentNull` / `PresentValue`）だけを比較する
- object/container node は「自身の presence category が model 間で揃っていない」または「いずれかの子孫 node が差分あり」のどちらかで `true`
- 判定基準は「self presence mismatch または subtree 内のいずれかの node に `ValueState.Mismatched` が存在するか」とする
- `Missing` のみで構成された subtree は差分ありとはみなさない
- 1 model 入力では比較対象がないため `false`

## 4.1 Source Dataset Example

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

## 4.2 Container Member Access Pattern (Current API)

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

### 4.2.1 Direct Child Traversal Example

`GetDirectChildren()` は通常の node 型で直下 member を辿るための共通面として使う。

```csharp
IParallelNode rootNode = (IParallelNode)Assert.IsType<ParallelNode<Dataset>>(result.Root);

var diffChildren = rootNode
    .GetDirectChildren()
    .Where(child => child.HasDifferences)
    .ToList();
```

```csharp
static IEnumerable<(string Path, IParallelNode Node)> Walk(IParallelNode node, string path)
{
    foreach (ParallelChildSet childSet in node.GetDirectChildren())
    {
        for (var childIndex = 0; childIndex < childSet.Nodes.Count; childIndex++)
        {
            IParallelNode child = childSet.Nodes[childIndex];
            var segment = childSet.Nodes.Count == 1
                ? childSet.Name
                : $"{childSet.Name}[{child.KeyText ?? $"#{childIndex}"}]";
            var childPath = string.IsNullOrEmpty(path) ? segment : $"{path}.{segment}";
            yield return (childPath, child);

            foreach (var descendant in Walk(child, childPath))
            {
                yield return descendant;
            }
        }
    }
}
```

- ライブラリは再帰 API を持たず、ユーザー側が `yield return` や LINQ で探索を組み立てる
- 探索対象 node は通常アクセスと同じ `IParallelNode` で統一する
- empty container のように `Nodes.Count == 0` でも property 差分があり得るため、direct traversal の一次判定には `ParallelChildSet.HasDifferences` を使う
- scalar/object member は `Name` だけで一意化し、container member は `Name[discriminator]` で一意化する
- container child の discriminator は `KeyText` を優先し、`KeyText == null` のときだけ `#<ordinal>` を代替識別子として使う

### 4.2.2 Dynamic `GetState` の保証範囲

この節で扱うのは、`AsDynamic()` から辿る値経路の `GetState(modelIndex)` である。

- 対象:
  - `root.Groups[0].Items[0].MetricA.GetState(modelIndex)`
  - `root.Groups[0].Items[0].Detail.Label.GetState(modelIndex)` のような dynamic nested value path
- 対象外:
  - node 自体の `GetState(modelIndex)`
  - value の indexer 読み取り（`Property[modelIndex]`）
  - generated projection の nested value path 実装

ここで使う用語:

- プロパティ宣言型:
  - モデルのプロパティ宣言に書かれている型。
  - 例: `public DetailBase Detail { get; init; }` の `Detail` のプロパティ宣言型は `DetailBase`。
- 比較時に事前構築済みのメンバー:
  - `Compare(...)` 実行中に、ライブラリが内部 node / state として先に作っておいたメンバー。
  - 典型例は、プロパティ宣言型からそのまま辿れるメンバー。
- 呼び出し時の反射による代替解決:
  - 比較時に事前構築済みの node が無いため、`GetState(...)` を呼んだ瞬間に実行時オブジェクトを反射で辿って値を読む経路。
  - ここでいう「代替」は、「事前構築済み state を読む本来経路の代わりに、その場で値を辿る経路」を指す。

比較時に事前構築済みのメンバーでは、`GetState(modelIndex)` は保存済みの state を参照して判定する。
そのため、`GetState` 呼び出し中に member getter を再実行しないことを保証する。

この保証は、getter の副作用や例外発生タイミングを比較実行時へ寄せるためのものである。
`GetState` のために必要な getter 評価や例外発生は compare / node construction 時に前倒しされ得るが、
`GetState` 呼び出し時に追加の getter 実行は発生させない。

ただし、この保証は比較時に事前構築済みのメンバーに限る。
プロパティ宣言型に存在しない実行時専用メンバーは dynamic access 自体は継続利用できるが、
`GetState` は「呼び出し時の反射による代替解決」で判定する。
そのため、`GetState` 自体が常に使えないわけではないが、
「getter を再実行しない」「比較時に保存済み state だけを見る」という保証は適用しない。

実行時専用メンバーの `GetState` が呼べる例:

```csharp
public abstract class DetailBase
{
}

public sealed class DetailLeaf : DetailBase
{
    public string? Label { get; init; }
}

public sealed class Item
{
    public DetailBase Detail { get; init; } = null!;
}

dynamic root = result.AsDynamic();
ValueState state = root.Items[0].Detail.Label.GetState(0);
```

- `DetailBase` には `Label` が無いが、実行時オブジェクトが `DetailLeaf` なら access 自体は継続できる
- この `GetState` は比較時に保存済みの `Label` state を読むのではなく、呼び出し時に実行時オブジェクトを反射で辿って判定する

実行時専用メンバーの `GetState` が失敗し得る例:

```csharp
public abstract class DetailBase
{
}

public sealed class DetailLeaf : DetailBase
{
    public string? Label { get; init; }
}

public sealed class DetailWithoutLabel : DetailBase
{
}

// left は DetailLeaf、right は DetailWithoutLabel
dynamic root = result.AsDynamic();
ValueState state = root.Items[0].Detail.Label.GetState(0);
```

- 片側の実行時オブジェクトに `Label` が無いので、`GetState` は `MissingMemberException` で失敗し得る
- これは「プロパティ宣言型に無い実行時専用メンバー」を、呼び出し時に反射で辿っているため

一方、実行時専用メンバーが container の場合は、member access 自体を list view として継続利用できる。

```csharp
public abstract class DetailBase
{
}

public sealed class DetailWithChildren : DetailBase
{
    public List<Child> Children { get; init; } = [];
}

public sealed class Child
{
    [CompareKey]
    public int ChildId { get; init; }

    public string? Label { get; init; }
}

dynamic root = result.AsDynamic();
foreach (dynamic child in root.Items[0].Detail.Children)
{
    string? label = child.Label[0];
}
```

- これは `a.b.c.d` の `d` が実行時専用 `List` でも `foreach` / index access できる、という意味である
- ただし container 正規化の前提（例: sequence element に `[CompareKey]` が必要）を満たさない場合は、silent に欠落させず access 時に `CompareExecutionException` を返す

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
- getter を再実行しない `GetState` 保証は dynamic value path に限定し、generated nested value path の parity はこの設計範囲に含めない

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
- dynamic value-path `GetState` は、比較時に事前構築済みのメンバーについては compare 時に保持した member state を参照し、状態判定のために getter を再実行しない
- その結果、dynamic value path で観測される getter の副作用や例外は compare / node construction 時に前倒しされ得る
- ただし、この保証は比較時に事前構築済みのメンバーに限定され、プロパティ宣言型に無い実行時専用メンバーは呼び出し時の反射による代替解決を利用する
- generated projection の nested value path には、この timing 制約を直ちには適用しない

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
- プロパティ宣言型
- container category（`Dictionary` / `List` / `Array` / `IEnumerable` / `ScalarOrObject`）
- runtime type（判明時）
- 追加情報（element type、key type、実体化件数、compare key 名、issue code など）

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
