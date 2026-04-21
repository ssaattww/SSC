# T-069 Implementation Report

- Date: 2026-04-21
- Task: compare 実行 trace ログ出力の追加

## Summary

- `CompareConfiguration.TraceLog` を追加し、`Compare(...)` 実行中の内部 trace 行を callback で受け取れるようにした。
- metadata / node construction / container normalization / issue 記録の各段階で trace を出力するようにした。
- 特に `List` / `Array` / `IEnumerable` / `Dictionary` の分類、runtime type、materialized count、compare key 解決結果を確認できるようにした。

## Changed Files

- `src/SSC/CompareConfiguration.cs`
- `src/SSC/ParallelCompareApi.cs`
- `tests/SSC.E2E.Tests/CompareApiE2ETests.cs`
- `doc/design/detail/02-PublicApi.md`
- `doc/design/detail/03-ContainerRules.md`
- `doc/design/detail/06-ExecutionPipeline.md`
- `README.md`

## Commands

- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --filter "FullyQualifiedName~Compare_WhenTraceLogEnabled_EmitsListContainerClassification|FullyQualifiedName~Compare_WhenTraceLogEnabled_EmitsEnumerableMaterializationDetail"`
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --filter FullyQualifiedName~CompareApiE2ETests`
- `dotnet test SSC.sln --configuration Release`

## Result

- 新規 trace 用 E2E 4 件は成功した。
- `CompareApiE2ETests` は 15 件成功した。
- `SSC.sln --configuration Release` は成功した（Unit 6 / E2E 39）。

## Notes

- trace は `CompareResult` へ保持せず、実行中 callback のみに流す。
- `Type` 表記は assembly-qualified name ではなく `Type.ToString()` ベースに抑え、ユーザーが判読しやすい形にした。
- review 指摘を受け、trace off 既定時の不要な `params` 割り当てを避けるため、hot path 側で `IsTraceEnabled` ガードを追加した。
- `Array` / `Dictionary` の trace 契約も E2E で追加検証した。
