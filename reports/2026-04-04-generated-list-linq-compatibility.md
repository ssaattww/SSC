# 2026-04-04 Generated List LINQ Compatibility

## Summary

- `ParallelGeneratedList<TElement, TView>` に `IReadOnlyList<TView>` を実装し、`IEnumerable<TView>` として LINQ 拡張メソッドを利用可能にした。
- 既存の `Count` / indexer アクセス契約は維持し、範囲外アクセス時の `CompareExecutionException` も維持した。

## Changed Files

- `src/SSC/GeneratedProjectionRuntime.cs`
  - `ParallelGeneratedList<TElement, TView>` に `IReadOnlyList<TView>` を付与
  - `GetEnumerator()` / 非ジェネリック `IEnumerable.GetEnumerator()` を追加
- `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
  - generated view list に対する `Select(...)` 利用の E2E テストを追加

## Verification

- `dotnet test SSC.sln --configuration Release --verbosity minimal`
  - Passed: E2E 28 / Unit 4
