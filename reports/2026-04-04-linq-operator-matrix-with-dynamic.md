# 2026-04-04 LINQ Operator Matrix With Dynamic

## Summary

- LINQ 演算子のマトリクス検証を新規 E2E ファイルへ追加した。
- 検証軸は 3 入口（Node / Generated / Dynamic）。
- Dynamic 入口で LINQ 拡張を使うため、`DynamicParallelListView` に `IReadOnlyList<object?>` 実装を追加した。

## Added / Changed Files

- `src/SSC/ParallelDynamicAccessExtensions.cs`
  - `DynamicParallelListView` に `IReadOnlyList<object?>` と列挙子を実装
- `tests/SSC.E2E.Tests/LinqMatrixE2ETests.cs`
  - Node / Generated / Dynamic の各入口で下記演算子を検証
    - `Where`, `Select`, `SelectMany`
    - `OrderBy`, `OrderByDescending`
    - `First`, `Any` 相当, `All`
    - `Skip`, `Take`, `ToArray`

## Dynamic LINQ Usage Note

- Dynamic 入口では `root.Groups` 自体は dynamic 参照のため、LINQ 拡張利用時は
  `IEnumerable<object?> groups = (IEnumerable<object?>)root.Groups;`
  のように明示キャストしてから演算する。

## Verification

- `dotnet test SSC.sln --configuration Release --verbosity minimal`
  - Passed: E2E 33 / Unit 4
