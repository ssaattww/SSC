# 2026-04-04 README Generated LINQ Example Update

## Summary

- `README.md` の Source Generator 例（`AsGeneratedView()`）に、LINQ でのアクセス例を追加した。
- `Select` と `SelectMany` による取得例を追記し、generated list を列挙可能にした今回の対応を利用者向けサンプルへ反映した。

## Changed File

- `README.md`
  - Source Generator Example に `using System.Linq;` を追加
  - `groupIds` 取得例（`Select` + `ToArray`）を追加
  - `itemIds` 取得例（`SelectMany` + `Select` + `ToArray`）を追加
