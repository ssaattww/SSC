# 2026-04-04 README Generated Where Filter Example Update

## Summary

- Source Generator 例の LINQ サンプルを、単純列挙だけでなく不一致抽出の実用例へ更新した。
- `AsGeneratedView()` から `SelectMany(...).Where(...)` で差分要素を絞り込む例を追加した。

## Changed File

- `README.md`
  - `mismatchedItemIds` 例を追加
  - 判定条件:
    - `GetState(0)` と `GetState(1)` が異なる
    - または `MetricA[0]` と `MetricA[1]` が異なる
