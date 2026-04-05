# GitHub Actions Node 24 Readiness Update

- Date: 2026-04-05
- Scope: `.github/workflows/pr-xunit-tests.yml`, `.github/workflows/publish-nuget.yml`

## Background

GitHub Actions の実行ログで、Node.js 20 ベースの JavaScript Action 廃止予定に関する警告が表示された。
対象として `actions/checkout@v4` と `actions/setup-dotnet@v4` が示されていたため、
Node.js 24 対応版へ更新した。

## Change

- `pr-xunit-tests.yml`
  - `actions/checkout@v4` -> `actions/checkout@v5`
  - `actions/setup-dotnet@v4` -> `actions/setup-dotnet@v5`
- `publish-nuget.yml`
  - `actions/checkout@v4` -> `actions/checkout@v5`
  - `actions/setup-dotnet@v4` -> `actions/setup-dotnet@v5`

## Result

警告対象として示された action バージョンを更新し、
Node.js 24 への既定切替に向けた workflow 側の準備を反映した。
