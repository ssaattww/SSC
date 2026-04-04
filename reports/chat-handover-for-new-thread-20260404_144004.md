【1. このチャットの目的】
- このチャットで達成しようとしていたこと:
  - SSC リポジトリの Phase 3 実装を、ユーザー指示（厳密運用、設計書同時更新、subagent レビュー運用、こまめな commit/push）に沿って継続すること。
  - 特に dynamic projection 周辺の仕様明確化と不整合修正（T-041）を完了し、次チャットでも同一前提で再開できる状態にすること。
- 最終的に目指していたゴール:
  - `root.Groups[0].Items[0].MetricA[0]` 形式のアクセスを維持しつつ、公開契約・エラー契約・テスト・設計書の整合を取る。
  - 未解決課題を Backlog に明示し、次作業の着手点を固定する。

【2. 新しいチャットに切り替える理由】
- 理由:
  - 会話が長くなり、設計/実装/CI/レビュー/ドキュメントの論点が混在しているため。
  - 今後は dynamic 依存低減（T-040）と `GetState` 非侵襲化（T-042）など、別テーマを切り分けて進める必要があるため。
- 次チャットでしやすくしたいこと:
  - 現在の確定事項と残課題を前提として、追加確認なしで実装を再開すること。
  - まず何を触るか（対象ファイル、検証コマンド、完了条件）を固定した状態で開始すること。

【3. 背景・前提条件】
- 作業背景:
  - リポジトリ: `ssaattww/SSC`
  - 現在ブランチ: `codex/phase3-e2e-hardening-comments`
  - PR: `#9`（open, draft=false, mergeable=true）
  - PR URL: `https://github.com/ssaattww/SSC/pull/9`
- 技術/環境:
  - .NET 8
  - テスト: xUnit（`tests/SSC.E2E.Tests`, `tests/SSC.Unit.Tests`）
  - CI: `.github/workflows/pr-xunit-tests.yml`, `.github/workflows/publish-nuget.yml`
- 共有済みルール/制約:
  - 設計漏れがあれば `doc/design/detail/*.md` を同一作業で更新する。
  - subagent はレビュー用途で活用し、結果を `reports/` に残す。
  - subagent の結果回収は「再依頼」ではなく `wait_agent` ポーリング方式で運用。
  - 作業は機能単位で `reports/*.md` を1ファイル作成する。
  - `tasks/tasks-status.md`, `tasks/phases-status.md`, `tasks/feedback-points.md` を常に最新化する。
- 補足:
  - NuGet publish で過去に 403（API key invalid/expired/permission）が発生しており、実公開は secret 側の状態に依存する。

【4. ここまでの経緯】
- 1) dynamic projection 機能を導入し、`root.Groups[0].Items[0].MetricA[0]` と要素全体アクセス（`...[index][model]`）を実装。
  - 関連コミット: `7b20966`, `f6da184`
- 2) subagent レビューを実施し、以下の論点を抽出。
  - `AsDynamic` 契約不整合（`Root` から直接使いにくい）
  - 値パス `GetState` の意味不整合
  - 予約名衝突（`Count`/`KeyText`）
  - list 範囲外アクセス契約
  - `CreateLeaf(values, states)` 長さ未検証
  - 設計書内部表現型の不一致
  - レポート: `reports/2026-04-04-subagent-code-review-dynamic-projection-followup.md`
- 3) T-041 として上記を実装修正。
  - `AsDynamic<T>(this Parallel<T> node)` へ拡張
  - 値パス `GetState` を最終参照メンバー状態で返すよう修正
  - node メタを `NodeCount` / `NodeKeyText` として明示（衝突回避）
  - dynamic list 範囲外で `CompareExecutionException(ModelIndexOutOfRange)`
  - `CreateLeaf` の null/長さ検証追加
  - 設計書 `01-DomainModel.md`, `02-PublicApi.md` 更新
  - レポート: `reports/2026-04-04-dynamic-projection-review-fixes.md`
- 4) テストを追加し回帰防止。
  - `AsDynamic` 入口（`Parallel<T>`）
  - 値パス `GetState`（Missing/PresentNull/PresentValue）
  - 予約名衝突
  - list index 範囲外（正/負）
  - `CreateLeaf` 長さ不一致
  - 不正レシーバ `AsDynamic` ガード
- 5) subagent 再レビューを実施。
  - High: なし
  - Medium: 新規なし
  - 残留リスクとして `value path GetState` が getter 実行依存である点を継続課題化
- 6) 進捗管理更新。
  - `T-041` を Done
  - `T-042`（`GetState` 非侵襲化）を Backlog 登録
  - `T-040`（型付き投影 API）は Backlog 継続

【5. 決定事項】
- dynamic projection は現行仕様として維持する。
- `AsDynamic` は `Parallel<T>` から呼べる契約とする。
- モデルメンバー名と node メタ名衝突回避のため、node メタは `NodeCount` / `NodeKeyText` を正規参照とする。
- dynamic list 範囲外アクセスは契約例外（`ModelIndexOutOfRange`）で扱う。
- `CreateLeaf` は入力不整合（values/states 長不一致）を即時検証する。
- 設計漏れが発生したら設計書を同一作業で更新する運用を継続する。
- subagent レビュー回収は `wait_agent` ポーリング方式を継続する。

【6. 未解決事項・保留事項】
- T-040: dynamic 依存を減らす型付き投影 API の設計/実装（IDE 補完改善）。
- T-042: 値パス `GetState` の非侵襲化（getter 実行に依存しない状態判定方式）。
- PR #9 の title/body は初期スコープ寄りで、現在の実装範囲（T-041 反映後）とのズレがあるため、必要なら更新。
- NuGet pre-release/stable publish 実行時は secret 側の有効 API key が前提（403 再発時は secret/権限確認が必要）。

【7. 次のチャットで最初に依頼すべき内容】
- そのまま貼れる依頼文:

```text
reports/chat-handover-for-new-thread-20260404_144004.md を読み、記載前提で作業再開してください。
優先順位は T-042（dynamic 値パス GetState の非侵襲化）→ T-040（型付き投影 API 設計）です。

要件:
1) まず T-042 の設計を `doc/design/detail/02-PublicApi.md` と必要章へ反映してから実装すること
2) 実装後は E2E/Unit テストを追加し、`dotnet test SSC.sln --configuration Release` を通すこと
3) subagent レビューを実施し、結果を reports に1ファイルで出力すること
4) tasks/tasks-status.md, tasks/phases-status.md, tasks/feedback-points.md を更新すること
5) こまめに commit/push すること

判断が必要な場合は止まらず、まず妥当な案で進めてから差分と理由を報告してください。
```

【8. 引き継ぎ本文】
以下を次チャット先頭に貼り付けること。

```text
このスレッドは SSC リポジトリ（branch: codex/phase3-e2e-hardening-comments）の継続作業です。
前提は reports/chat-handover-for-new-thread-20260404_144004.md に記載済みで、要約ではなく再開可能粒度で整理してあります。

現在状態（2026-04-04 時点）:
- PR: https://github.com/ssaattww/SSC/pull/9 （open, draft=false）
- In Progress: なし
- Backlog:
  - T-040: dynamic 依存を減らす型付き投影 API
  - T-042: dynamic 値パス GetState の非侵襲化
- 直近完了:
  - T-041（dynamic projection 指摘対応）完了
  - subagent 再レビュー結果: Highなし、Medium新規なし
- 直近コミット:
  - 64ce058 test: harden AsDynamic guard and add follow-up coverage
  - b2fb901 feat: align dynamic projection contract and state semantics

作業ルール:
- 設計漏れがあれば同一作業で doc/design/detail を更新
- subagent レビュー結果は reports に出力
- subagent 回収は wait_agent ポーリングで行い、再依頼しない
- tasks/tasks-status.md, tasks/phases-status.md, tasks/feedback-points.md を最新化
- 機能単位で reports を1ファイル作成

次にやること:
1) T-042 の設計更新（まず設計）
2) T-042 実装と回帰テスト追加
3) dotnet test SSC.sln --configuration Release 実行
4) subagent レビュー実施と report 出力
5) tasks/phases/feedback 更新、commit/push
```
