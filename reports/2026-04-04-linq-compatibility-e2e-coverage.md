# 2026-04-04 LINQ Compatibility E2E Coverage

## Summary

- LINQ 互換性検証を既存ファイルから分離し、`LinqCompatibilityE2ETests` を新規追加した。
- Node 入口（`Children(...)`）と generated projection 入口（`AsGeneratedView()`）の両方で主要 LINQ 演算が連鎖利用できることを確認した。

## Added Test File

- `tests/SSC.E2E.Tests/LinqCompatibilityE2ETests.cs`
  - `Compare_NodeChildren_SupportsCoreLinqOperators`
    - `Where` / `Select` / `SelectMany` / `OrderBy` / `OrderByDescending`
    - `Any` / `All` / `First` / `Skip` / `Take` / `ToArray`
  - `Compare_GeneratedProjectionList_SupportsCoreLinqOperators`
    - generated list（`ParallelGeneratedList<,>`）に対する同系統 LINQ 演算の連鎖

## Verification

- `dotnet test SSC.sln --configuration Release --verbosity minimal`
  - Passed: E2E 30 / Unit 4
