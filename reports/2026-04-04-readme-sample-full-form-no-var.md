# README Samples: Full Form And No `var`

- Date: 2026-04-04
- Goal: README のコードサンプルを省略なし・`var` なしへ統一する

## Changes

- `README.md` の `Minimal Example` を更新
  - `ProductModel[]` / `CompareResult<ProductModel>` / `ValueState` を明示
  - クラス定義を含む完全形を維持
- `README.md` の `Source Generator Example` を更新
  - `Dataset[]` 入力データ定義を追加
  - `CompareResult<Dataset>` を明示
  - `AsGeneratedView()` 利用例を `double?` / `ValueState` で明示

## Verification

- `README.md` 全体で `var` の使用がないことを確認
