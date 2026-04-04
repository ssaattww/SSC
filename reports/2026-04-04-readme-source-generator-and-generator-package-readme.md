# README Update For Source Generator And Generator Package Readme

- Date: 2026-04-04
- Goal:
  - ルート README に Source Generator 対応を明記
  - `ssaattww.SSC.Generators` パッケージへルート README を同梱

## 変更内容

- `README.md`
  - `ssaattww.SSC.Generators` の NuGet バッジを追加
  - `NuGet Packages` 節を追加し、runtime/generator の2パッケージ構成を明記
  - Source Generator の利用例（`[GenerateParallelView]` + `AsGeneratedView()`）を追加
  - テスト件数表記を最新に更新（E2E 27 / Unit 4）
- `src/SSC.Generators/SSC.Generators.csproj`
  - `<PackageReadmeFile>README.md</PackageReadmeFile>` を追加
  - ルート `README.md` の pack 同梱設定を追加

## 検証

- ローカル pack で `ssaattww.SSC` / `ssaattww.SSC.Generators` を生成
- `SSC.Generators` の readme 未設定警告は解消
- 残る警告は `NU1900`（vulnerability feed 参照失敗）で、README 同梱とは無関係
