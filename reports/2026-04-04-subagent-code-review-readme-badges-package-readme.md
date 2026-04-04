# Code Review Report: README Badges / PackageReadme (2026-04-04)

## Scope
- README.md
- src/SSC/SSC.csproj
- reports/2026-04-04-readme-badges-and-package-readme.md

## Findings

### Critical
- None.
- Evidence: `dotnet pack src/SSC/SSC.csproj -c Release` succeeds, and package metadata includes `<readme>README.md</readme>`.

### High
- None.
- Evidence: Generated package contains root-level `README.md` (`unzip -l ...nupkg` confirms entry), so `PackageReadmeFile` and packing item are functionally consistent.

### Medium
- None.
- Evidence: Badge URLs target current repository/package naming (`ssaattww/SSC`, `ssaattww.SSC`) and are structurally valid markdown links.

### Low
1. `README.md` のライセンスバッジが相対リンク `LICENSE` を参照しているため、NuGet の package readme 表示ではリンク切れになる可能性が高い。
- Evidence:
  - `README.md` badge: `[![License](...)](LICENSE)`
  - Packed `.nupkg` content does not include `LICENSE` file (contains `README.md`, dll, nuspec, metadata files only).
- Impact:
  - NuGet ページ上でライセンスバッジ遷移が期待通り動作しない可能性。

## Residual Risk
- README を package readme として再利用しているため、GitHub向け相対リンクや将来追加される相対アセット（画像・ドキュメント）が NuGet 表示で不整合になるリスクが継続する。
- バッジの対象（workflow名、package id、repo名）変更時に README 更新漏れが発生すると表示不整合が起きる。

## Recommended Actions
1. ライセンスバッジリンクを絶対URLへ変更する（例: `https://github.com/ssaattww/SSC/blob/main/LICENSE`）。
2. package readme運用ルールとして「相対リンクを使わない」または「NuGet向けREADMEを分離する」を設計書に明記する。
3. CIにREADMEリンク検証（少なくとも外部URLの死活確認）を追加し、将来のリンク不整合を早期検出する。
