# Subagent Code Review Report: NuGet Workflow Reactivation (2026-04-04)

## Scope

- `.github/workflows/publish-nuget.yml` の再有効化差分
- 関連管理更新（`tasks/*.md`, `reports/2026-04-04-nuget-workflow-reactivation.md`）

## Findings (by severity)

### Critical

- No critical findings.

### High

- No high findings.

### Medium

- Release トリガー時のパッケージバージョンが release/tag と連動していない。
  - Target: `.github/workflows/publish-nuget.yml`
  - Evidence: `Resolve package version` は `package_version` 未指定時に `<VersionPrefix>-ci.${GITHUB_RUN_NUMBER}` を生成するため、`release: published` 実行でもタグ版数を採用しない。
  - Impact: GitHub Release 名/タグと NuGet 公開版数が一致しない運用になり、追跡性や再現性が下がる。

### Low

- No low findings.

## Residual Risks

- 期待運用が「Release タグ = NuGet 版数」の場合、現在のままでは運用ミスマッチが継続する。

## Recommended Actions

1. `release` イベント時は `github.ref_name`（タグ）を優先して `package_version` として採用する条件分岐を追加する。
2. 手動実行 (`workflow_dispatch`) は現行どおり `package_version` 入力優先 + 未指定時 CI サフィックス生成を維持する。
