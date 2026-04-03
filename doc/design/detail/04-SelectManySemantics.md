# SelectMany Semantics

## 1. Principle

- `SelectMany` は構造階層を 1 段展開する
- model slot は展開しない
- 展開後要素は `ParallelXxx` を維持する

## 2. Type Transition

```text
IEnumerable<ParallelDataset>
  -> SelectMany(d => d.Groups)
IEnumerable<ParallelGroup>
  -> SelectMany(g => g.Items)
IEnumerable<ParallelItem>
```

## 3. Per-Model Slot Contract

```text
groups[i][0] = model0 側 Group
groups[i][1] = model1 側 Group

items[i][0] = model0 側 Item
items[i][1] = model1 側 Item
```

## 4. Order Contract

1. 外側の列挙順を維持
2. 内側は keyUnion 順
3. LINQ 標準の連結順で flatten

## 5. Missing Contract

- 片側欠損でも存在側から keyUnion 構築
- 全側欠損は空列
- 欠損判定は `GetState(modelIndex)` を使用

## 6. Example

```text
Groups:
[0]=[G1,G1]
[1]=[G2,null]
[2]=[null,G3]

Items (group order then item key order):
[0]=[I100,I100]
[1]=[I200,null]
[2]=[null,I400]
[3]=[I300,null]
[4]=[null,I500]
```

## 7. User / Library Boundary

- ユーザー定義: `Dataset`, `Group`, `Item`
- ライブラリ提供: `Parallel<T>` 系、`Compare(...)`
- 境界は `Compare` 呼び出し時点
