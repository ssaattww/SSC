# README Source Generator Example Update (Three Groups)

- Date: 2026-04-05
- Scope: `README.md`

## Background

`Source Generator Example` の `Dataset` サンプルで、`Groups` が 1 要素のみだったため、
複数 group を前提とした利用イメージが伝わりにくかった。

## Change

- `models[0].Groups` を 3 要素へ拡張（`GroupId`: 1, 2, 3）
- `models[1].Groups` も 3 要素へ拡張（`GroupId`: 1, 2, 3）
- group ごとに item 差分を残し、既存の `Mismatched` 抽出例と整合するデータへ調整
- 一致ケースを明示するため、同値 `Items` を追加
  - `GroupId=2`: `ItemId=211, MetricA=21.1`（両モデル一致）
  - `GroupId=3`: `ItemId=320, MetricA=32.0`（両モデル一致）

## Result

README の Source Generator サンプルで、複数 `Groups` を持つ入力形をそのまま参照可能になった。
