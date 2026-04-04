# 2026-04-04 Public API Reference Notation and Nullability

## Summary

- `doc/design/detail/02-PublicApi.md` に、参照記法の意図を明示するため以下を追記。
  - 概念表現: `root.Groups[0].Items[0].MetricA[0]`
  - 左右 `[]` の意味（左: List index、右: model index）
- 併せて `null` になり得る範囲と `GetState` 併用方針を追記。

## Updated File

- `doc/design/detail/02-PublicApi.md`

## Added Notes

- 概念表現の完全形をサンプルとして記載。
- 現行 API (`Children(...)`) との関係を維持したまま、参照軸を明確化。
- `result.Root` / slot 値の `null` 可能性を明文化し、`GetState(modelIndex)` で判定する指針を追加。
