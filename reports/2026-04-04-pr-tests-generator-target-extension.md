# PR Test Workflow: Generator Target Extension

- Date: 2026-04-04
- Target: `.github/workflows/pr-xunit-tests.yml`

## 変更概要

- PR テスト workflow の検出対象を拡張し、test project に加えて source generator project も検出するように変更。
- `has_generators` が true の場合、`dotnet restore` + `dotnet build --configuration Release` を実行。
- 既存のテスト実行（`dotnet test`）は維持しつつ、入力変数名を `test_projects` へ明確化。

## 期待効果

- `src/SSC.Generators/*.csproj` が PR CI の検証対象に必ず含まれる。
- generator のコンパイル不整合を test 実行前に検知できる。
- 既存の xUnit 実行フローとの互換を維持する。
