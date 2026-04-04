# Subagent Review: Projection Switch At Result Stage

- Date: 2026-04-04
- Reviewer: subagent (`019d57b1-da39-7631-afa9-86ec4152017f`)
- Scope:
  - `src/SSC/CompareResultProjectionExtensions.cs`
  - `src/SSC.Generators/ParallelViewGenerator.cs`
  - `tests/SSC.E2E.Tests/*`
  - `doc/design/detail/01-DomainModel.md`
  - `doc/design/detail/02-PublicApi.md`

## Findings

1. Medium: generated extension の実装 `internal` と設計書の `public` 表記が不一致
2. Low: nullability 節の dynamic サンプルが旧導線（`result.Root!.AsDynamic()`）のまま

## Resolutions

- `doc/design/detail/02-PublicApi.md` の generated API 署名を実装どおり `internal` 表記へ修正
- nullability 節の dynamic 例を `result.AsDynamic()` へ更新

## Validation

- `dotnet test SSC.sln --configuration Release` 成功
