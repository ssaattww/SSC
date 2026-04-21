# T-071 実装レポート

- Date: 2026-04-21
- Task: 差分子要素探索 API の core 実装
- Executor: main agent
- Related Skills:
  - `development-orchestrator`
  - `task-breakdown-planner`
  - `task-consistency-manager`
  - `tdd-executor`
  - `codex-delegation-executor`
  - `implementation-executor`

## 実装内容

- `IParallelNode` に `HasDifferences()` / `GetDirectChildren()` を追加し、`ParallelChildSet` を公開契約へ追加した。
- `ParallelNode<T>` に direct child の順序保持、container member の presence 状態保持、subtree 差分判定を実装した。
- review follow-up として `ParallelChildSet.HasDifferences` を追加し、empty container 差分も direct traversal から観測可能にした。
- `ParallelCompareApi` の node 構築を整理し、container property の getter 結果を 1 回の member slot 構築から子 node 生成へ流す形に変更した。
- `CompareApiE2ETests` に traversal API の順序・shape・presence mismatch・subtree 伝播に加えて、list/dictionary の empty container 差分を検証するケースを追加した。

## 変更ファイル

- `src/SSC/Contracts.cs`
- `src/SSC/ParallelNode.cs`
- `src/SSC/ParallelCompareApi.cs`
- `tests/SSC.E2E.Tests/CompareApiE2ETests.cs`
- `tasks/tasks-status.md`
- `tasks/phases-status.md`
- `doc/design/detail/02-PublicApi.md`
- `reports/task-t-070-design-update-20260421171940.md`

## ローカル確認

- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --filter "FullyQualifiedName~CompareApiE2ETests.Compare_WithObjectAndContainerMembers_GetDirectChildrenPreservesPropertyOrderAndShapes|FullyQualifiedName~CompareApiE2ETests.Compare_WithEmptyObjectMissingOnOneSide_HasDifferencesTreatsPresenceMismatchAsDifference|FullyQualifiedName~CompareApiE2ETests.Compare_WithNestedLeafMismatch_HasDifferencesPropagatesToAncestorNodes|FullyQualifiedName~CompareApiE2ETests.Compare_WithEmptyContainerMissingOnOneSide_HasDifferencesUsesContainerPresenceMismatch|FullyQualifiedName~CompareApiE2ETests.Compare_WithEmptyDictionaryMissingOnOneSide_HasDifferencesUsesChildSetSignal"` 成功
- `dotnet test SSC.sln --configuration Release` 成功

## メモ

- empty container member の差分を親 node が見落とさないよう、container property の presence states を `ParallelNode<T>` に保持する形にした。
