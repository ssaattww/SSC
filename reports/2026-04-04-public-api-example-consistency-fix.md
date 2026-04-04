# 2026-04-04 Public API Example Consistency Fix

## Summary

- `doc/design/detail/02-PublicApi.md` の `4.1` アクセス例が、`4.0` の元データセット（`Dataset/Group/Item`）と不整合だったため修正。
- 参照例を `MetricA` と `ItemId` union（`100, 200, 300`）に統一。

## Fixed Points

- `ParallelNode<EnumerableRoot>` -> `ParallelNode<Dataset>`
- `items = root.Children(model => model.Items)` -> `groups[0].Children(model => model.Items)`
- `Value` 参照 -> `MetricA` 参照
- key/値コメントを `100/200/300` 系に揃えた

## Updated File

- `doc/design/detail/02-PublicApi.md`
