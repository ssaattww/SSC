# 2026-04-04 PackageId Rename To devo6

## Summary

- NuGet PackageId を `ssaattww.*` から `devo6.*` へ変更した。
- README の NuGet バッジとパッケージ名表記を新しい PackageId に合わせて更新した。

## Changed Files

- `src/SSC/SSC.csproj`
  - `<PackageId>devo6.SSC</PackageId>`
- `src/SSC.Generators/SSC.Generators.csproj`
  - `<PackageId>devo6.SSC.Generators</PackageId>`
- `README.md`
  - NuGet version/downloads バッジ URL を `devo6.*` へ更新
  - NuGet Packages セクションの表示名を `devo6.*` へ更新

## Verification

- `dotnet test SSC.sln --configuration Release --verbosity minimal`
  - Passed: E2E 33 / Unit 4
