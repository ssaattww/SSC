# 2026-04-04 Public API Source Dataset Clarification

## Summary

- `doc/design/detail/02-PublicApi.md` に、`Children(...)` アクセス例の前提となる「比較前の元データセット例」を追記。
- 例示の入力と出力アクセスの対応関係（ItemId union）を明文化。

## Updated File

- `doc/design/detail/02-PublicApi.md`

## Added Content

- `Source Dataset Example` 節を追加。
- `Dataset / Group / Item` の型定義を追記。
- `Dataset -> Groups -> Items` の2モデル入力例を記載。
- `ItemId` の union（`100, 200, 300`）で整列されることを説明。
