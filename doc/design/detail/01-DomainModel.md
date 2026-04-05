# Domain Model

## 1. Input Model (Example)

```csharp
public sealed class Dataset
{
    public IReadOnlyList<Group> Groups { get; init; } = new List<Group>();
}

public sealed class Group
{
    [CompareKey]
    public int GroupId { get; init; }
    public IReadOnlyList<Item> Items { get; init; } = new List<Item>();
}

public sealed class Item
{
    [CompareKey]
    public int ItemId { get; init; }
    public double MetricA { get; init; }
    [CompareIgnore]
    public string? InternalMemo { get; init; }
}
```

## 2. Lifted Model Concept

```text
Parallel<Dataset>
 └ Groups : IEnumerable<ParallelGroup>
      └ Items : IEnumerable<ParallelItem>
```

変換規則:

- `T -> Parallel<T>`
- `List<T> -> IEnumerable<Parallel<T>>`（CompareKey 正規化済み）
- `Dictionary<TKey, TValue> -> IEnumerable<Parallel<TValue>>`（keyUnion 正規化済み）

## 3. Internal Representation (Concept)

```csharp
internal sealed class ParallelNode<T> : Parallel<T>
{
    T?[] Values;
    Dictionary<string, IReadOnlyList<IParallelNode>> Children;
}
```

- `Values[modelIndex]` が model slot
- `Children` は構築中内部表現
- 公開面では `Children(...)` / `AsDynamic()` / generated projection により階層アクセスへ投影される

## 3.2 Dynamic Projection Access (Public)

`AsDynamic()` を使うと、コンテナ階層を次の形でアクセスできる。

```csharp
dynamic root = ParallelCompareApi.Compare(models).AsDynamic();
var metric = root.Groups[0].Items[0].MetricA[0];
```

- 左の `[]`: key union 後の要素 index
- 右の `[]`: model index
- node メタ情報は `NodeCount` / `NodeKeyText` で参照できる（モデル同名メンバー衝突を回避）

## 3.3 Generated Projection Access (Public)

`[GenerateParallelView]` を root model に付与すると、Source Generator が型付き view を生成する。

```csharp
[GenerateParallelView]
public sealed class Dataset
{
    public List<Group> Groups { get; init; } = [];
}

var typedRoot = ParallelCompareApi.Compare(models).AsGeneratedView();
var metric = typedRoot.Groups[0].Items[0].MetricA[0];
var keyText = typedRoot.Groups[0].Items[0].NodeMeta.KeyText;
```

- `dynamic` 非依存で IDE 補完を利用できる
- list index 範囲外は `ModelIndexOutOfRange`
- node メタ情報は `NodeMeta` 配下（`NodeMeta.Count` / `NodeMeta.KeyText`）で参照する
- 投影切替の入口は `CompareResult` 拡張（`AsDynamic()` / `AsGeneratedView()`）に統一する

## 3.1 CompareIgnore Attribute

比較対象に入れたくないメンバーは `CompareIgnore` 属性で除外する。

```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CompareIgnoreAttribute : Attribute
{
}
```

- 制限を増やさず、必要な除外だけ明示するための属性
- `CompareIgnore` が付いたメンバーはノード生成対象から外す

## 4. Presence State

```csharp
public enum ValueState
{
    Missing,
    Matched,
    Mismatched
}
```

`GetState(modelIndex)` は比較可否と一致/不一致を返す。

- `Missing`: 比較対象が存在しない
- `Matched`: slot が存在し、比較結果が一致
- `Mismatched`: slot が存在し、比較結果が不一致（比較先欠損を含む）

## 5. Invariants

- 1 つの `Parallel<T>` は「論理要素 1 件」を表す
- すべての `Parallel<T>.Count` は `models.Count` と一致する
- Compare 完了後、順序キー自体は保持しない（位置意味のみ保持）

## 6. Example (Group)

```text
data0.Groups = [GroupId=1, GroupId=2]
data1.Groups = [GroupId=1, GroupId=3]

keyUnion = [1,2,3]

Groups[0] = [Group(1), Group(1)]
Groups[1] = [Group(2), null]
Groups[2] = [null, Group(3)]
```
