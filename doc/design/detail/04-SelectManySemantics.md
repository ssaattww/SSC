# SelectMany Semantics

## Rule

- `SelectMany` は構造階層を 1 段展開する
- model 軸は展開しない
- 結果要素は常に `ParallelXxx` のまま

## Type Transition

```text
IEnumerable<ParallelDataset>
  --SelectMany(d => d.Groups)--> IEnumerable<ParallelGroup>
  --SelectMany(g => g.Items)-->  IEnumerable<ParallelItem>
```

## Order Guarantee

1. 外側要素順
2. 内側 keyUnion 順
3. LINQ 標準の連結順

## Example (Per-Model Slots)

```text
groups[i][0] = model0 側 Group
groups[i][1] = model1 側 Group

items[i][0] = model0 側 Item
items[i][1] = model1 側 Item
```

## Missing Behavior

- 片側欠損: 存在側から keyUnion を構築
- 両側欠損: 空列
