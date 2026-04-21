# T-072 実装レポート

- Date: 2026-04-21
- Task: object node の `GetState` 誤判定修正
- Executor: main agent
- Related Skills:
  - `development-orchestrator`
  - `task-consistency-manager`
  - `design-doc-maintainer`
  - `tdd-executor`
  - `implementation-executor`

## 実装内容

- `ParallelNode<T>.GetState(int modelIndex)` の non-scalar node 判定を、object 参照の既定 equality ではなく self presence と subtree 差分から導くよう変更した。
- object/container node では、node 自身の presence mismatch、container presence mismatch、member node 差分、child node 差分のいずれかがあれば `Mismatched`、それ以外は `Matched` とした。
- `CompareApiE2ETests` に、child/member が等しい別インスタンス object node は `Matched`、member 差分がある object node は `Mismatched` になる回帰テストを追加した。
- `doc/design/detail/09-ValueStateBehavior.md` を更新し、node-level `GetState` の scalar / object-container 判定ルールを分離して明記した。

## 変更ファイル

- `src/SSC/ParallelNode.cs`
- `tests/SSC.E2E.Tests/CompareApiE2ETests.cs`
- `doc/design/detail/09-ValueStateBehavior.md`
- `tasks/tasks-status.md`
- `tasks/phases-status.md`

## ローカル確認

- 修正前に追加テスト `Compare_WithEquivalentObjectMembers_NodeLevelGetStateReturnsMatched` が `Expected: Matched / Actual: Mismatched` で失敗することを確認した。
- 修正後、追加した object node 向け targeted tests が成功した。
- `dotnet test SSC.sln --configuration Release` が成功した。

## メモ

- scalar node の既存挙動は維持しつつ、object/container node の `GetState` だけを構造比較ベースへ切り替えた。
