# Sub-agent実行レポート

## タスク

- 目的: T-081 README 利用例と public API ドキュメントの同期
- タスク種別: docs 実装

## sub-agentを使う理由

- 理由: ユーザー指示により docs 更新は `gpt-5.5 medium` の sub-agent に委譲する。親 agent は scope 管理、report 確認、Git 操作を担当する。

## 対象範囲

- 対象: README の XPath-like path access / diff entry 最小利用例、public API 設計書と実装後 API 名・例外・表示例の同期、必要に応じた package readme 同期

## 対象外

- 対象外: コード実装変更、Markdown 検査、Markdown whitelist/tooling、破壊的変更、PR 操作、commit/push

## 実行コマンド

- 実行コマンド:
  - `rg -n "PackageReadmeFile|README.md" src/SSC/SSC.csproj src/SSC.Generators/SSC.Generators.csproj`: 成功。runtime / source generator package とも root `README.md` を package readme として同梱する設定を確認。
  - `git diff --check`: 成功。
  - `git diff --check`: 成功（review fix 後）。
  - `git diff --check`: 成功（再レビュー fix 後）。
  - Markdown 検査: ユーザー指示により未実行。

## 対象ファイル

- 変更または確認したファイル:
  - `README.md`
  - `doc/design/detail/02-PublicApi.md`
  - `src/SSC/ParallelPathAccessExtensions.cs`
  - `src/SSC/ParallelDiffContracts.cs`
  - `tests/SSC.E2E.Tests/XPathLikePathAccessE2ETests.cs`
  - `tests/SSC.E2E.Tests/XPathLikeDiffEntriesE2ETests.cs`
  - `reports/task-t-081-implementation-20260623100427.md`
  - `tasks/tasks-status.md`
  - `AGENTS.md`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。
  - review fix: [P2] `README.md` の `ContainerPresence` example が前段の `entries = result.GetDiffEntries()` を再利用しているように読め、Minimal Example の data では `ContainerPresence` entry が存在しない。
  - 再レビュー fix: [P2] `README.md` の「parent node が missing の場合に `Items: [0]=null(Mismatched), [1]=<missing>(Missing)` が表示される」という説明は、通常 `GetDiffEntries()` 経路では parent `Kind == Node` entry で return する実装と一致しない。

## 結果

- 結果:
  - `README.md` に XPath-like path access の最小例を追加し、`GetNodeByPath`、`GetValueByPath`、`GetStateByPath` の使い方を記載した。
  - `README.md` に `GetDiffEntries()` の最小例を追加し、`Path`、`Kind`、`Values`、`ToString()`、`Kind == Node` の path round-trip、`Kind == ContainerPresence` の `Node == null` と node 解決保証なしを記載した。
  - `README.md` の diff entry 例に、`ValueState` で Missing と実値 `null` を区別する表示例を追加した。
  - `doc/design/detail/02-PublicApi.md` の public API contract を実装後の extension method signature に合わせ、`GetDiffEntries()` が `ContainerPresence` も返す説明と表示例を補足した。
  - package readme は root `README.md` が `src/SSC/SSC.csproj` と `src/SSC.Generators/SSC.Generators.csproj` で同梱されるため、追加ファイルは不要と確認した。
  - review fix として、`ContainerPresence` example を `emptyContainerResult` / `containerEntries` を使う別 comparison の self-contained snippet に修正し、前段の `entries` と誤読されないようにした。
  - root-level property null の実装挙動と整合するよう、実行 snippet の表示例を `Items: [0]=null(Mismatched), [1]=null(Mismatched)` に変更し、`<missing>(Missing)` は親 node missing 由来の一般表示例として別の text block に分離した。
  - 再レビュー fix として、parent node missing 由来の `Items: [0]=null(Mismatched), [1]=<missing>(Missing)` text block を `README.md` から削除し、実装と一致する self-contained `ContainerPresence` example だけを残した。
  - コード実装変更、BreakingChanges 追記、Markdown 検査、commit/push/PR 操作には踏み込んでいない。

## リスク

- 未解決のリスクまたは後続対応:
  - 未解決リスクなし。Markdown 検査はユーザー指示により未実行。
