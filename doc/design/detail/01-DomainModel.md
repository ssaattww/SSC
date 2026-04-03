# Domain Model

## Input Model Example

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

## Lifted Model Concept

```text
Parallel<Dataset>
 └ Groups : IEnumerable<ParallelGroup>
      └ Items : IEnumerable<ParallelItem>
```

## Presence State

各 model slot は次の 3 状態を持つ。

- `Missing`: 対応要素なし
- `PresentNull`: 要素あり・値 null
- `PresentValue`: 要素あり・値あり
