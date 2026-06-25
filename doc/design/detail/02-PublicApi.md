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

```csharp
public static class ParallelPathAccessExtensions
{
    public static IParallelNode? GetNodeByPath<T>(this CompareResult<T> result, string path);

    public static object? GetValueByPath<T>(
        this CompareResult<T> result,
        string path,
        int modelIndex);

    public static ValueState GetStateByPath<T>(
        this CompareResult<T> result,
        string path,
        int modelIndex);

    public static IReadOnlyList<ParallelDiffEntry> GetDiffEntries<T>(
        this CompareResult<T> result);
}

public sealed class ParallelDiffEntry
{
    public string Path { get; }
    public string? ParentPath { get; }
    public ParallelDiffEntryKind Kind { get; }
    public IParallelNode? ParentNode { get; }
    public IParallelNode? Node { get; }
    public IReadOnlyList<ParallelDiffValue> Values { get; }

    public override string ToString();
}

public enum ParallelDiffEntryKind
{
    Node,
    ContainerPresence,
}

public sealed class ParallelDiffValue
{
    public int ModelIndex { get; }
    public object? Value { get; }
    public ValueState State { get; }

    public override string ToString();
}
```

## 4. Behavior Contract

- indexer の範囲外アクセスは `ModelIndexOutOfRange`
- dynamic list index の範囲外アクセスも `ModelIndexOutOfRange`
- generated list index の範囲外アクセスも `ModelIndexOutOfRange`
- generated list の key text indexer で key が見つからない場合は `KeyNotFound`
- `AllPresent == Values.All(v => v != null かつ Missing でない)`
- `AnyPresent == Values.Any(v => Missing でない)`
- `HasDifferences()` は current node 自体、または配下 subtree のいずれかに差分があれば `true`
- `GetDirectChildren()` は current node の直下 property を `ParallelChildSet` 単位で返す
- `ParallelNode<T>.ToString()` と generated value の `ToString()` は model slot 別 value/state を Diff と同じ形式で返す
- generated object view の `ToString()` は underlying `ParallelNode<T>.ToString()` に委譲する
  - 元モデル型が `ToString()` を override している場合、その文字列表現を model slot 別に確認できる
  - 例: `root.Root.Attribute["id"].ToString()` は `GeneratedXmlAttribute.ToString()` の結果を `[0]=...(State), [1]=...(State)` 形式で返す
  - 生成型に comparable member `ToString` がある場合は名前衝突を避けるため、object view の override は生成しない
- direct generated scalar member は対応する member `ParallelNode<TValue>` に indexer / `GetState()` / `ToString()` を委譲し、`ParallelNode<T>.ToString()` と表示実装を共有する
  - `Select(...)` 由来の派生 generated value は materialized member node を持たないため、一時的な leaf `ParallelNode<TValue>` に変換して `ToString()` 表示を共有する
- dynamic projection は materialized node がある path では `ToString()` を同じ node 表示へ委譲する
  - runtime reflection だけで辿る materialized node のない派生 path は対象外

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

### 4.2.2 XPath-like Path Access And Diff Helper

XPath-like path access は、比較結果 tree 内の node を文字列 path で取得する helper である。
XML XPath の完全実装ではなく、SSC の object member / container node / model slot に必要な最小 grammar を定義する。

目的:

- dynamic / generated projection を使わない利用者にも、path 文字列で値を取得する導線を提供する
- 差分表示 helper が、path と model 別 value/state を併記できるようにする
- `GetDirectChildren()` で利用者が自前実装していた再帰 traversal と path 組み立てを、標準 helper として提供する

非目的:

- XML XPath の axis、attribute、namespace、function、predicate 全般を実装すること
- 任意条件で node set を検索する query language を提供すること
- `CompareIssue.Path` の既存診断形式を置き換えること

#### 4.2.2.1 Public API Contract

`GetNodeByPath<T>(this CompareResult<T> result, string path)` は、XPath-like path に一致する `IParallelNode` を返す。
path が解決できない場合は `null` を返す。

`GetValueByPath<T>(this CompareResult<T> result, string path, int modelIndex)` は、`GetNodeByPath(path)` で取得した node の `GetValue(modelIndex)` を返す。
path が解決できない場合は `null` を返す。
model index が範囲外の場合は、既存 node indexer と同じく `ModelIndexOutOfRange` の `CompareExecutionException` を返す。

`GetStateByPath<T>(this CompareResult<T> result, string path, int modelIndex)` は、`GetNodeByPath(path)` で取得した node の `GetState(modelIndex)` を返す。
path が解決できない場合は `Missing` を返す。
model index が範囲外の場合は、既存 node indexer と同じく `ModelIndexOutOfRange` の `CompareExecutionException` を返す。

`GetDiffEntries<T>(this CompareResult<T> result)` は、差分のある node を leaf/value path 単位で列挙し、child node を持たない container presence mismatch を container member path で列挙する。
返却値は構造化データとし、表示専用 string API にはしない。
ただし `ParallelDiffEntry` と `ParallelDiffValue` は人間確認用の `ToString()` を必ず実装する。

#### 4.2.2.2 Path Grammar

XPath-like path は root からの相対 path を基本とする。
root type 名の prefix は任意であり、指定された場合は比較 root の型名と一致する必要がある。

```text
path             = [ root-name "." ] segment *( "." segment )
segment          = member-name [ selector ]
selector         = "[" discriminator "]"
discriminator    = key-discriminator / ordinal-discriminator
ordinal-discriminator = "#" 1*DIGIT
key-discriminator     = 1*( key-char / escape-sequence )
escape-sequence       = "\]" / "\\" / "\#"
```

`member-name` は比較対象 model の public property 名である。
大文字小文字は区別し、`StringComparer.Ordinal` 相当で解決する。

例:

- `Groups`
- `Groups[1]`
- `Groups[1].Items[100].MetricA`
- `Dataset.Groups[1].Items[100].MetricA`
- `Items[#0].Name`

segment の意味:

- `Name`
  - scalar/object member を表す
  - `ParallelChildSet.Nodes.Count == 1` の member に対応する
- `Name[key]`
  - container member の child node を key text で選択する
  - child node の `KeyText` と完全一致する child を選ぶ
- `Name[#ordinal]`
  - key text を持たない container child を ordinal で選択する
  - ordinal は同一 `ParallelChildSet.Nodes` 内の 0-based index である

selector を持つ segment は container member に対してだけ有効である。
scalar/object member に selector が指定された場合は未解決扱いとする。

selector を持たない container member は、その container 全体を表す node ではなく child set を表すため、
`GetNodeByPath` の最終 segment としては解決できない。
container child を取得する場合は `Items[100]` または `Items[#0]` のように selector を指定する。

#### 4.2.2.3 Escaping Rule

`.` は bracket 外では segment 区切りである。
bracket 内では key discriminator の一部として扱う。

bracket 内で `]` または `\` を key text として含める場合は escape する。
key text が `#<digits>` 形式そのものの場合は、先頭の `#` を escape して ordinal discriminator と区別する。

```text
\]  => ]
\\  => \
\#  => #（bracket 先頭で ordinal discriminator と区別する場合）
```

例:

- key text `A.B` は `Items[A.B]`
- key text `A]B` は `Items[A\]B]`
- key text `A\B` は `Items[A\\B]`
- key text `#0` は `Items[\#0]`

#### 4.2.2.4 Path Generation Rule

`GetDiffEntries()` が生成する path は、`GetDirectChildren()` と同じ direct child traversal を基にする。

- scalar/object member は `Name`
- container child は `Name[discriminator]`
- discriminator は `child.KeyText` を優先する
- `child.KeyText == null` の場合だけ `#<ordinal>` を使う
- root type 名は生成 path には含めない

`Kind == Node` の diff entry で生成される path は、同一 `CompareResult<T>` 内で `GetNodeByPath(path)` に渡すと同じ node を解決できなければならない。
同時に、entry の親 path / 親 node も traversal 中に保持し、利用者が `Path` を文字列分割して親へ戻る必要がないようにする。

empty container の presence mismatch のように child node が存在しない差分は、
特定 child path を生成できない。
この場合は container member 名の path を持つ `Kind == ContainerPresence` の diff entry として表す。
`ContainerPresence` entry は public node に対応しないため、`Node == null` とし、`GetNodeByPath(Path)` による node 解決は保証しない。
それでも `Path` には該当 container member 名を入れ、差分表示で位置を失わない。
`ContainerPresence` entry の `ParentNode` は、該当 container member を所有する親 node を指す。

#### 4.2.2.5 Diff Entry Contract

`ParallelDiffEntry` は 1 つの差分箇所を表す。

- `Path`
  - XPath-like path
  - `Kind == Node` では `GetNodeByPath(Path)` で同じ node を解決できる
  - `Kind == ContainerPresence` では container member の位置を表すが、node 解決は保証しない
- `ParentPath`
  - 親 node の XPath-like path
  - root 直下の diff entry では `null`
  - `Kind == Node` / `Kind == ContainerPresence` のどちらでも同じ規則で設定する
  - `ParentPath != null` の場合、同一 `CompareResult<T>` 内で `GetNodeByPath(ParentPath)` に渡すと `ParentNode` と同じ node を解決できる
- `Kind`
  - `Node`: `IParallelNode` に対応する差分
  - `ContainerPresence`: child node を持たない container presence mismatch
- `ParentNode`
  - diff entry の直接の親 `IParallelNode`
  - root 直下の diff entry では compare root を指す
  - `Kind == ContainerPresence` では container member を所有する親 node を指す
- `Node`
  - `Kind == Node` では差分が観測された `IParallelNode`
  - `Kind == ContainerPresence` では `null`
- `Values`
  - model slot ごとの value/state
  - `Values.Count == result.Root.Count`

`ParallelDiffValue` は 1 model slot の表示単位である。

- `ModelIndex`
  - model slot の index
- `Value`
  - `Kind == Node` では `Node.GetValue(ModelIndex)` の値
  - `Kind == ContainerPresence` では `null`
  - `Missing` と実値 `null` は `State` で区別する
- `State`
  - `Kind == Node` では `Node.GetState(ModelIndex)` の値
  - `Kind == ContainerPresence` では該当 container member の presence mismatch から導いた値

`GetDiffEntries()` は次の node を返す。

- `HasDifferences() == true` の leaf/value node
- object/container node 自身の presence mismatch
- empty container など child node を持たないが `ParallelChildSet.HasDifferences == true` の差分

object/container node で child 側に差分があるだけの場合は、親 node の diff entry を重複して返さない。
親自身の presence mismatch がある場合だけ親 node の diff entry を返す。

#### 4.2.2.6 ToString Contract

`ParallelNode<T>.ToString()` は、node 自身の model slot 別 value/state を 1 行で表す。
generated value の `ToString()` も同じ形式で、対象 member の value/state を 1 行で表す。

例:

```text
[0]="left"(Mismatched), [1]="right"(Mismatched)
[0]=null(Matched), [1]=null(Matched)
[0]=<missing>(Missing), [1]=10(Mismatched)
```

`ParallelDiffEntry.ToString()` は、path と model 別 value/state を 1 行で表す。

例:

```text
Groups[1].Items[100].MetricA: [0]=1(Mismatched), [1]=10(Mismatched)
Groups[1].Items[200].Name: [0]="left"(Mismatched), [1]=<missing>(Missing)
Items: [0]=null(Mismatched), [1]=<missing>(Missing)
```

`ParallelDiffValue.ToString()`、`ParallelNode<T>.ToString()`、generated value の `ToString()` の各 slot は `[modelIndex]=value(state)` 形式を返す。

value 表示:

- `Missing` の slot は `<missing>`
- 実値 `null` は `null`
- string は `"` で囲む
- その他は `Convert.ToString(value, CultureInfo.InvariantCulture)` 相当

`ToString()` は人間確認用の便利表示であり、機械処理の安定契約は `Path` / `Values` / `State` / `Value` / indexer / `GetState(modelIndex)` を使う。
ただし、同じライブラリ version 内では deterministic な表示を保つ。

#### 4.2.2.7 CompareIssue.Path との違い

`CompareIssue.Path` は issue 診断用の property path であり、既存どおり container key / ordinal を含めない。
一方、XPath-like path は比較結果 tree の node 解決用であり、container child を `Items[100]` / `Items[#0]` のように識別する。

この 2 つは互換変換を保証しない。
issue から詳細 node を辿る必要がある場合は、`CompareIssue.Path` と `KeyText` を組み合わせて利用者側で判断する。

### 4.2.3 Dynamic `GetState` の保証範囲

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
- sequence element に `[CompareKey]` が無い場合は ordinal index で揃えるため、runtime-only container でも list view として access できる
- `MissingCompareKeyListPolicy.SkipAndRecordError` を明示した場合は、旧来どおり `CompareKeyNotFoundOnSequenceElement` を記録して配下をスキップする

### 4.2.4 実行時専用メンバーで `GetState` が判定される仕組み

この節では、`root.Items[0].Detail.Label.GetState(0)` のような dynamic value path が、
保存済み state を読む通常経路と、呼び出し時に反射で値を辿る代替経路のどちらへ入るかを説明する。

実行時専用メンバーとは、プロパティ宣言型には無いが、実行時オブジェクトには存在するメンバーである。

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
```

この例では、`Detail` のプロパティ宣言型は `DetailBase` であり、`Label` は `DetailBase` には無い。
そのため `Detail.Label` は、比較時に常に内部 node として作られるとは限らない。

dynamic value path の `GetState` は、内部的には次の情報を持つ。

- 比較結果 root node
- `Detail.Label` のような member path
- その path に対応する保存済み node があれば、その参照

判定手順は次の 2 経路に分かれる。

1. 保存済み state を読む通常経路
   - 比較時にその member path が内部 node として作られていれば、その保存済み node を使う
   - `GetState(modelIndex)` はその node の `GetState` をそのまま返す
   - この経路では、`GetState` 呼び出し中に getter を再実行しない

2. 呼び出し時に反射で値を辿る代替経路
   - 保存済み node が無い場合、`GetState(modelIndex)` はその場で root model object から member path を辿る
   - 具体的には、各段で現在の実行時型に対して public property を反射で探し、値を取得する
   - 対象 model slot が欠損なら `Missing`
   - 比較相手 model が 1 つも無い場合も `Missing`
   - 対象 model slot に値があり、比較相手 slot のどれかが欠損なら `Mismatched`
   - 双方に値があり、presence が同じで `Equals` が一致すれば `Matched`
   - 双方に値があり、presence が違うか `Equals` が不一致なら `Mismatched`

代替経路の擬似フロー:

```text
GetState(modelIndex)
  -> 保存済み node があるか?
     -> Yes: 保存済み node の GetState を返す
     -> No:
        -> 指定 model の root 値を取得
        -> member path を各段で反射して辿る
        -> 途中の property が見つからなければ MissingMemberException
        -> 比較相手 model が無ければ Missing
        -> 他 model に対しても同じ path を反射して辿る
        -> presence / Equals で Matched/Mismatched/Missing を決める
```

この代替経路で起こり得ること:

1. `GetState` は呼べることがある
   - 実行時オブジェクトに対象メンバーがあれば、保存済み node が無くても判定自体はできる

2. `GetState` は失敗することがある
   - 対象 model でも比較相手 model でも、member path の途中で property を見つけられないと `MissingMemberException` になる
   - getter 自体が例外を投げる場合、その例外は `GetState` 呼び出し側へそのまま伝播する

3. 「getter を再実行しない」保証は無い
   - 判定のために、その場で property getter を呼んで値を取得するからである
   - したがって、副作用や例外発生は compare 時ではなく `GetState` 呼び出し時に起き得る

実行時専用メンバーが container の場合は、この代替経路に入る前に member access 側で container 判定を行う。

- 実行時オブジェクト上の `Children` が `List<T>` なら、`root.Items[0].Detail.Children` は list view へ切り替える
- その結果、`foreach` や `[index]` は継続利用できる
- ただし container 正規化の前提を満たさない場合は `CompareExecutionException` で失敗する

つまり、実行時専用メンバーの `GetState` は「常に使えない」のではない。
ただし、保存済み state を読む通常経路ではなく、呼び出し時に反射で値を辿る代替経路へ入ることがあり、
その場合は getter 再実行・`MissingMemberException`・getter 例外伝播を許容する。

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
var leftLabel = root.Groups[0].Items[0].Detail.Label[0]; // direct nested object view
var leftLabelViaSelector = root.Groups[0].Items[0].Detail.Select(x => x.Label)[0]; // compatible nested value path
var nodeCount = root.Groups[0].Items[0].NodeMeta.Count;
var group1 = root.Groups["1"];
var idAttributeValue = root.Root.Attribute["id"].Value[0];
var score100 = root.Scores[100][0];
var attributeFromDiffPath = root.Root.Attribute.ByPathKey("A\\]B").Value[0];

// model 単位で list を選択（Missing slot を除外）
var leftGroups = root.Groups.SelectModel(0);
var rightGroups = root.Groups.SelectModel(1);
var leftGroupIdAt0 = leftGroups[0].GroupId[0];
```

- generated view は `CompareResult<T>` の compare result node に対してのみ有効
- generated view で取得する公開 `ValueState` の意味は `AsDynamic()` と同一
- 投影切替の入口は `CompareResult` 拡張に統一する
- generated API の node メタ情報は `NodeMeta` 配下に分離し、モデル同名メンバーと衝突させない
- generated sequence container は key union 順の index でアクセスできる
- `NodeMeta.KeyText` を持つ generated sequence child は key text でもアクセスできる（例: `root.Groups["1"]`）
- Dictionary member は `ParallelGeneratedDictionary<TKey, TElement, TView>` として生成し、通常の dictionary に近い形で key 型の indexer を使う（例: `root.Root.Attribute["id"]` / `root.Scores[100]`）
- Dictionary member の key union 順 access が必要な場合は `AtIndex(index)` を使う
- Dictionary member で `GetDiffEntries().Path` の bracket 内に現れる XPath-like escaped discriminator text からアクセスする場合は `ByPathKey(discriminator)` を使う
- key lookup は繰り返し利用時に線形探索を繰り返さない
- key が存在しない場合は `KeyNotFound` の `CompareExecutionException` を送出する
- class / struct 型の object member は nested generated view として直接辿れる（例: `root.Root.Name[0]`）
- object member view は互換導線として `Select(...)` も提供し、既存の nested value path 利用を維持する
- `SelectModel(modelIndex)` は指定 model で `Missing` でない要素のみを返し、順序は key union 順を維持する
- getter を再実行しない `GetState` 保証は dynamic value path に限定し、generated nested value path の parity はこの設計範囲に含めない

## 4.4 Generated Projection Scope (Initial)

初期版の生成対象は次に固定する。

- container path:
  - `IEnumerable<TElement>`, `IReadOnlyDictionary<TKey, TValue>`, `IDictionary<TKey, TValue>`
- value path:
  - `Property[modelIndex]`
  - `Property.GetState(modelIndex)`
- object path:
  - class / struct 型の direct member は nested generated view として生成
  - nested object path は直接 member access で連鎖
  - `Select(...)` による nested value path は互換導線として維持
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
        MissingCompareKeyListPolicy.AlignByIndex;
    public DuplicateKeyPolicy DuplicateKeyPolicy { get; init; } =
        DuplicateKeyPolicy.RecordError;
    public Action<string>? TraceLog { get; init; }
}
```

`TraceLog` を指定した場合、`Compare(...)` 実行中に内部 trace 行を同期的に受け取れる。

`MissingCompareKeyListPolicy.AlignByIndex` は、sequence 要素型に `CompareKey` が無い場合に ordinal index で要素を揃える。
旧来どおり `CompareKeyNotFoundOnSequenceElement` を Error として記録して配下をスキップする場合は、`SkipAndRecordError` を明示する。

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
