# GitHub Workflow 適合化レポート

- Date: 2026-04-03
- Scope: `.github/workflows/*.yml`

## 実施内容

1. PR テスト workflow (`pr-xunit-tests.yml`)
- `master` 前提を `main` へ変更
- 固定テストプロジェクト参照を廃止
- リポジトリ内の `*test*.csproj` / `*tests*.csproj` を自動検出して実行
- テストプロジェクトが無い場合は notice を出して成功終了

2. NuGet publish workflow (`publish-nuget.yml`)
- 初版まで無効化するため `release` トリガーを廃止
- `workflow_dispatch` のみ残し、`enable_release=true` 指定時だけ実行
- 固定 csproj パス依存を廃止
- `workflow_dispatch` 入力で `project_path` / `package_version` 指定可能化
- 未指定時はリポジトリ内 csproj から候補解決
  - 候補なし: fail
  - 候補複数: fail（明示指定を要求）

## 目的

- 現在のリポジトリ構成に依存しない CI/CD 基盤にする
- 将来の実装追加時にも workflow を再利用可能にする
- 初版までは「テストのみ有効」の運用を徹底する
