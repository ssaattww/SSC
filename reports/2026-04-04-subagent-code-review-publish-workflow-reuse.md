# Subagent Code Review Report: Publish Workflow Reuse (2026-04-04)

## Findings (by severity)

### Critical
- No critical findings.

### High
- `main` push で作成した pre-release が後続 publish を起動しない可能性が高い。
  - Target: `.github/workflows/publish-nuget.yml`
  - Evidence: `create-prerelease` は `GH_TOKEN: ${{ github.token }}` で `gh release create` を実行し、`publish` は `event_name == release || workflow_dispatch` の場合のみ実行される。`GITHUB_TOKEN` で生成したイベントは別 workflow を起動しない制約により、release run が発火せず publish がスキップされうる。
  - Impact: 「main マージで pre-release -> NuGet publish」の要件が満たされない可能性。

### Medium
- `create-prerelease` ジョブが `gh` CLI 前提だが、明示的な導入/検証ステップがない。
  - Target: `.github/workflows/publish-nuget.yml`
  - Evidence: `gh release view/create` を直接実行しており、CLI 利用可否を事前確認していない。
  - Impact: ランナーイメージ変更時に失敗するリスク。

### Low
- No low findings.

## Residual Risks
- release イベント伝播条件に依存したままだと、環境差異やトークン種別差異で publish 連携が不安定になる。

## Recommended Actions
1. `main` push run 内で publish まで完結させるか、release 作成を PAT ベースにして release workflow 発火を保証する。
2. `gh --version` などの事前チェック、または `actions/create-release` / `actions/github-script` 等の action ベース実装へ置換する。
