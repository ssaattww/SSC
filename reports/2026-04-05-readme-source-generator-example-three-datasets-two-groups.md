# README Source Generator Example Adjustment (Three Datasets, Two Groups)

- Date: 2026-04-05
- Scope: `README.md`

## Background

Source Generator Example について、README 掲載時の見通しを優先し、
ID 参照式をモデル数固定の明示形（`??` 連結）へ揃えたいという要望があった。
あわせて、入力サンプルは `Dataset` 3件・`Group` 2件の構成へ調整する方針となった。

## Change

- `Dataset[] models` は 3 モデルのまま維持
- 各 `Dataset` の `Groups` を 2 要素（`GroupId: 1, 2`）へ調整
- 3 モデル一致の item として `ItemId=210` を維持
- `groupIds` の参照式を明示形へ変更
  - `group.GroupId[0] ?? group.GroupId[1] ?? group.GroupId[2] ?? -1`
- `itemIds` の参照式を明示形へ変更
  - `item.ItemId[0] ?? item.ItemId[1] ?? item.ItemId[2] ?? -1`
- `mismatchedItemIds` の判定を明示形へ変更
  - `GetState(0) || GetState(1) || GetState(2)`

## Result

README の Source Generator Example は、
3 モデル比較の文脈を維持しつつ、2 group 構成と明示的な `??` 連結記法で読みやすい形に統一された。
