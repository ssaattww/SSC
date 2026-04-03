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
    Dictionary<string, IParallelNode> Children;
}
```

- `Values[modelIndex]` が model slot
- `Children` は構築中内部表現
- 公開面では型付きプロパティへ変換される

## 4. Presence State

```csharp
public enum ValueState
{
    Missing,
    PresentNull,
    PresentValue
}
```

`this[int]` だけでは `Missing` と `PresentNull` を区別できないため、
`GetState(modelIndex)` を併用する。

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
