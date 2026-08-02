# t-097 / Issue #54 ファイル別カバレッジページ対応レポート

## 概要

スマートフォン向けカバレッジレポートの行別表示を、全sourceを1つの巨大なHTMLへ埋め込む方式から、source fileごとに個別HTMLを生成する方式へ変更しました。

公開URLは従来どおり `https://ssaattww.github.io/SSC/` です。rootの`index.html`はcoverage summary、class、method、source file一覧を表示し、各source fileの全行は`files/*.html`で確認します。

## 要求

- source fileごとに行別coverageを別pageへ分割する
- index pageから対象fileを選択できる
- methodから対象fileの該当行へ直接移動できる
- iOS Safari向けのcompact表示と文字自動拡大防止を維持する
- `gh-pages`保存、GitHub Pages公開、PR HEAD不変、無限CI防止を維持する

## 実装

### generator

`scripts/generate-mobile-coverage-report.py`を次の構成へ変更しました。

- `coverage/mobile/code-coverage.html`
  - 全体coverage summary
  - class / method一覧
  - source file一覧
  - source code本文は含めない
- `coverage/mobile/files/<basename>-<path-hash>.html`
  - source file 1件分の全行
  - 行番号
  - `実行済み` / `未実行` / `対象外`
  - hit count
  - source text
  - source検索、行状態filter
  - indexへ戻るlink
  - GitHub sourceへのlink

file名にはbasenameとsource pathのSHA-1先頭12文字を使用し、同名fileでも衝突しない安定したURLにしています。

method linkは次の形式へ変換します。

```text
files/<file-page>.html#source-<path-hash>-L<line-number>
```

### workflow

`.github/workflows/pr-xunit-tests.yml`を更新しました。

- generator実行後に`coverage/mobile/files/*.html`が1件以上存在することを検査
- Pages artifactへ`coverage/mobile`全体を再帰copy
- `code-coverage.html`だけを`index.html`へrename
- `gh-pages`更新時に既存`files`を削除してから新しい`files`一式をcopy
- stale file pageが残らないようにした
- test/diagnostic artifactのmanifestへfile別pageを記録

PR branchへの自動commitは行わず、公開先は`gh-pages`だけです。したがってPR HEADは変わらず、`gh-pages` pushでPR workflowが再帰実行されることもありません。

### tests

次を検証しています。

- source fileごとに個別HTMLが生成される
- indexにsource本文を埋め込まない
- indexから個別pageへlinkする
- method linkが個別page内の該当行anchorを指す
- 個別pageに行状態、hit数、source text、戻るlinkがある
- iOS Safari向け`text-size-adjust:none`とcompact CSSが個別pageに適用される
- workflowがmobile directory全体をartifactと`gh-pages`へcopyする

同じ目的のRed契約が2件追加されていたため、`MobileCoveragePerFilePageUnitTests`へ一本化し、重複した`MobileCoveragePerFilePagesUnitTests`は削除しました。

## TDD証跡

### Red

- HEAD: `09c82f60d4d0aeb8418a4673e1fb7971f2de1cab`
- matching run: `30767757724`
- conclusion: failure（期待どおり）
- E2E Tests: 88 passed
- Unit Tests: 104 passed / 2 failed
- failure: `files/*.html`が未生成
- diagnostic artifact: `8839507110`
- digest: `sha256:0b291035b1396329ddd69509bde42bae27fb783c6c57a9834b4003b7d6f9a0a6`

2件のfailureは、同じ要求を検証する重複契約でした。実装前の仕様不足を確認後、契約を1件へ整理しています。

### Green / 実装・設計検証

- HEAD: `aefee6d8acc25a2b0f1f35c97e2787b826e40975`
- matching run: `30768798263`
- workflow conclusion: success
- test and coverage job: success
- Pages publication job: success
- Unit Tests: 105 passed
- E2E Tests: 88 passed
- test/diagnostic artifact: `8839838209`
- test artifact digest: `sha256:4832aede1f0d65656f9f553c202515eed21da535cbd7bbc950120fa9b2479c72`
- GitHub Pages artifact: `8839837898`
- Pages artifact digest: `sha256:b2ea6b80bed48ba9cd6b78ddcba601faf72abda3e2e34c7677647129aac9beac`
- generated source file pages: 19
- index内のsource row: 0
- `gh-pages/source-head.txt`: `aefee6d8acc25a2b0f1f35c97e2787b826e40975`

workflow runの`head_sha`、PR HEAD、公開元HEADが一致しています。別SHAのrunは代用していません。

## 公開構成

```text
gh-pages/
├── index.html
├── files/
│   ├── CompareConfiguration.cs-97dc2060ffbf.html
│   ├── ParallelDiffPathPattern.cs-15b7bf249517.html
│   └── ... 19 files
├── source-head.txt
├── source-pr.txt
└── .nojekyll
```

## 変更file

- `scripts/generate-mobile-coverage-report.py`
- `.github/workflows/pr-xunit-tests.yml`
- `tests/SSC.Unit.Tests/MobileCoveragePerFilePageUnitTests.cs`
- `tests/SSC.Unit.Tests/MobileCoverageReportGeneratorUnitTests.cs`
- `tests/SSC.Unit.Tests/MobileCoverageIosTextScalingUnitTests.cs`
- `tests/SSC.Unit.Tests/Issue54CodeCoverageWorkflowContractUnitTests.cs`
- `doc/design/detail/13-CodeCoverageVisualization.md`

## 非対象

- coverage threshold gateの追加
- external coverage SaaSの導入
- PRのmerge
