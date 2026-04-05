# README Source Generator Example Update (Three Groups)

- Date: 2026-04-05
- Scope: `README.md`

## Background

`Source Generator Example` の `Dataset` サンプルで、`Groups` が 1 要素のみだったため、
複数 group を前提とした利用イメージが伝わりにくかった。

## Change

- `models[0].Groups` を 3 要素へ拡張（`GroupId`: 1, 2, 3）
- `models[1].Groups` も 3 要素へ拡張（`GroupId`: 1, 2, 3）
- `models[2]` を追加し、`Dataset[]` を 3 モデル入力へ拡張
- group ごとに item 差分を残し、既存の `Mismatched` 抽出例と整合するデータへ調整
- 一致ケースを明示するため、3モデルで同値の `Items` を追加
  - `GroupId=2`: `ItemId=210, MetricA=21.0`
  - `GroupId=2`: `ItemId=211, MetricA=21.1`
  - `GroupId=3`: `ItemId=320, MetricA=32.0`
- `itemIds` / `mismatchedItemIds` の ID 参照式を 3 モデル対応へ更新（`[0] ?? [1] ?? [2]`）
- モデル数固定の参照式を除去し、`NodeMeta.Count` + `Enumerable.Range` で走査する汎用式へ更新
- 参照式が読みにくくなったため、ID 解決と不一致判定をローカル関数へ抽出
  - `ResolveFirstAvailableId(IEnumerable<int?> candidates)`
  - `IsMismatchedInAnyModel(int modelCount, Func<int, ValueState> getState)`
  - これによりモデル数非依存のまま、`Select` / `Where` 側の式を簡潔化

## Result

README の Source Generator サンプルで、複数 `Groups` を持つ入力形をそのまま参照可能になり、
モデル数非依存ロジックの可読性も改善された。
