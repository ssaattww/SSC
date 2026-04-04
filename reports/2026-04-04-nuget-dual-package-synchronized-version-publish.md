# NuGet Dual Package Synchronized Version Publish

- Date: 2026-04-04
- Goal: `ssaattww.SSC` と `ssaattww.SSC.Generators` を同時配布し、同一 `PackageVersion` を適用する

## 変更内容

- `.github/workflows/publish-nuget.yml` を更新
  - 単一 `project_path` 運用を廃止
  - 対象プロジェクトを固定で2件に設定
    - `src/SSC/SSC.csproj`
    - `src/SSC.Generators/SSC.Generators.csproj`
  - `Resolve package version` で決定した単一 `package_version` を2プロジェクトの pack に共通適用
  - `Restore` / `Build` / `Pack` を2プロジェクトループ実行へ変更

## 検証

- ローカル dry-run:
  - `dotnet pack` を2プロジェクトへ同一 `PackageVersion=0.1.0-ci.local` で実行
  - 生成物:
    - `ssaattww.SSC.0.1.0-ci.local.nupkg`
    - `ssaattww.SSC.Generators.0.1.0-ci.local.nupkg`

## 補足

- `SSC.Generators` package には readme 未設定の警告（NU best-practice）が出るため、必要に応じて後続で package readme 同梱を追加する。
