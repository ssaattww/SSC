# NuGet Metadata And Generator Badges

- Date: 2026-04-04
- Goal:
  - NuGet ページに repository / license 情報を表示できるようにする
  - Source Generator パッケージのバッジを README に追加する

## Changes

- `src/SSC/SSC.csproj`
  - `PackageProjectUrl`
  - `RepositoryUrl`
  - `RepositoryType`
  - `PackageLicenseExpression`
- `src/SSC.Generators/SSC.Generators.csproj`
  - `PackageProjectUrl`
  - `RepositoryUrl`
  - `RepositoryType`
  - `PackageLicenseExpression`
- `README.md`
  - `ssaattww.SSC.Generators` の downloads バッジを追加

## Verification

- `dotnet pack` で生成した両 nupkg の `.nuspec` を確認
  - `<license type="expression">MIT</license>`
  - `<projectUrl>https://github.com/ssaattww/SSC</projectUrl>`
  - `<repository type="git" url="https://github.com/ssaattww/SSC" ... />`
