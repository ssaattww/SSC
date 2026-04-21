# T-073 実装レポート

- Date: 2026-04-21
- Task: dynamic runtime-derived container member access 修正
- Executor: main agent
- Related Skills:
  - `development-orchestrator`
  - `task-consistency-manager`
  - `design-doc-maintainer`
  - `tdd-executor`
  - `implementation-executor`

## 実装内容

- `AsDynamic()` の root view / list view / value-path view に `CompareConfiguration` を引き回し、runtime-derived container access の補助情報を参照できるようにした。
- `DynamicParallelValuePathView.TryGetMember` に runtime-derived container 判定を追加し、materialized node が無い経路でも `List` / `Dictionary` / `IEnumerable` を `DynamicParallelListView` として返すようにした。
- dynamic fallback 用に `ParallelCompareApi.TryBuildDynamicContainerChildren(...)` を追加し、runtime-derived container value から child node 群を再構築できるようにした。
- `ContainerAndSelectManyE2ETests` に、`a.b.c.d` 形式で辿った runtime-derived `List` member が `foreach` / index access できる回帰テストを追加した。
- review follow-up として、runtime-derived container の正規化で issue が発生した場合は silent に child を欠落させず、access 時に `CompareExecutionException` を返すようにした。
- `02-PublicApi` / `09-ValueStateBehavior` を更新し、runtime-derived container member は list view として access 継続できることを明記した。

## 変更ファイル

- `src/SSC/Contracts.cs`
- `src/SSC/ParallelCompareApi.cs`
- `src/SSC/ParallelDynamicAccessExtensions.cs`
- `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
- `doc/design/detail/02-PublicApi.md`
- `doc/design/detail/09-ValueStateBehavior.md`
- `tasks/tasks-status.md`
- `tasks/phases-status.md`

## ローカル確認

- 修正前に `Compare_DynamicProjection_RuntimeDerivedContainerMember_AllowsForeachAndIndexAccess` が `InvalidCastException` で失敗することを確認した。
- 修正後、runtime-derived scalar access、runtime-derived container access、runtime-derived container error path の targeted tests が成功した。
- `dotnet test SSC.sln --configuration Release` が成功した。

## メモ

- 問題の本質は「深い階層」ではなく、途中で materialized node を持たない runtime-derived fallback に入った後の member が container だった場合に list view へ昇格しないことだった。
