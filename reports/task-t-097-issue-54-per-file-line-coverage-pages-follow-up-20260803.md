# T-097 Issue #54 ファイル別行カバレッジページ対応レポート

- 対象repository: `ssaattww/SSC`
- 対象PR: #55
- 対象issue: #54
- 作業日: 2026-08-03
- 公開URL: `https://ssaattww.github.io/SSC/`

## 1. 背景

スマートフォン向けcoverage reportへsource行を追加した後、全source fileの行を1ページへ連結していたため、次の問題が残っていました。

- ファイル境界が分かりにくい。
- 目的のファイルを開いても、他ファイルの大量の行が同じdocument内に残る。
- source file単位のURLを共有できない。
- スマートフォンで長大なdocumentを移動する必要がある。

利用者から「それぞれのファイルをページに分割」「それぞれの行ごとの差分をページに分割」という要望があり、source fileごとに独立した行coverageページを生成する方式へ変更しました。

## 2. 採用設計

### 2.1 出力構成

```text
coverage/mobile/
├── code-coverage.html
└── files/
    ├── CompareConfiguration.cs-<hash>.html
    ├── ParallelNode.cs-<hash>.html
    └── ...
```

GitHub Pagesと`gh-pages` branchでは次の構成になります。

```text
/
├── index.html
├── files/*.html
├── source-head.txt
└── source-pr.txt
```

`code-coverage.html`は公開時に`index.html`として配置します。`files/*.html`はdirectoryごと保持します。

### 2.2 トップページの責務

トップページは次の情報に限定します。

- 全体line・branch coverage
- method coverage状態
- class・method一覧
- source file一覧
- 各ファイルの実行済み・未実行・対象外行数

source行tableはトップページへ埋め込みません。source fileを選ぶと`files/<page>.html`へ遷移します。

### 2.3 ファイル別ページの責務

各ファイルページには、そのsource fileだけを表示します。

- 行番号
- `実行済み`、`未実行`、`対象外`
- hit count
- source text
- source text検索
- 行状態filter
- 一覧へ戻るlink
- GitHub上のsource fileを開くlink

既存のiOS Safari向け高密度表示を維持しています。

- source font: `10px`
- line-height: `1.05`
- cell padding: `0 3px`
- `text-size-adjust: none`
- table viewport: `76vh`
- sticky header

### 2.4 link設計

- file一覧: `files/<basename>-<path-hash>.html`
- method link: `files/<page>.html#<source-line-anchor>`
- file pageから一覧: `../index.html`

同名source fileが別directoryに存在しても衝突しないよう、正規化source pathのSHA-1短縮値をfile名へ付与します。

### 2.5 物理HTML分割を採用した理由

query parameterとJavaScriptだけで擬似的に切り替える方式も検討できますが、今回は物理HTML分割を採用しました。

- 各ファイルに直接URLを持たせられる。
- 通常のbrowser navigationと戻る操作を利用できる。
- JavaScriptが無効でもsource行を表示できる。
- 1ページが保持するDOMを1ファイル分へ限定できる。
- 静的hostingであるGitHub Pagesと相性がよい。

## 3. TDD

### 3.1 Red

最初に、2個のsource fileを含むsample Coberturaから2個の個別HTMLが生成されることを要求する契約testを追加しました。

- Red HEAD: `ea2f6121fedfec64ce57f0f4712229a87be30121`
- matching workflow run: `30767528211`
- result: failure（期待どおり）
- E2E Tests: 88 passed
- 既存Unit Tests: 104 passed
- 新規契約test: 1 failed
- diagnostic artifact: `8839431941`
- artifact digest: `sha256:a778fd79306948615ec4580a43865697c06801d9349eb28f6a7bbc6af119e167`

失敗理由は`files/`directoryが生成されていないことでした。別SHAのrunは使用していません。

作業中に同目的の契約testが重複して追加されたため、最終実装では物理page生成、index link、戻るlink、source rowを確認する1系統へ整理しました。

### 3.2 Green実装

`generate-mobile-coverage-report.py`へ次を追加しました。

- source pathから安全な個別HTML file名を生成
- source fileごとのHTML生成
- indexの行別source sectionをfile一覧へ変更
- method linkを該当file pageと行anchorへ変更
- file pageに検索・状態filter・一覧へ戻るlinkを追加
- 既存のiOS高密度CSSを個別pageへ適用

workflowへ次を追加しました。

- `coverage/mobile/files/*.html`の生成検証
- Pages artifactへ`coverage/mobile/`全体を再帰copy
- `gh-pages/files/`を毎回置換
- 公開前に個別page directoryの存在を検証
- artifact manifestへ個別pageを含める

## 4. 検証snapshot

repository report保存前のGreen snapshotです。

- HEAD: `a9aeba58a4ce5cba81e9c5894ffe17e9490e1dff`
- matching workflow run: `30768687833`
- workflow conclusion: success
- `dotnet-tests`: success
- `Publish mobile coverage report`: success
- Unit Tests: 105 passed
- E2E Tests: 88 passed
- diagnostic/test artifact: `8839799824`
- artifact digest: `sha256:9acaaf82d784fef8d10936ad829e942226ac76063596e730ccd7e45e513e788e`
- GitHub Pages artifact: `8839799498`
- Pages artifact digest: `sha256:6755bcf64f127244e10db5d9dba1df656c50e5b959af9e1fe2bd8d00a78865e7`
- generated per-file pages: 19
- `gh-pages/source-head.txt`: `a9aeba58a4ce5cba81e9c5894ffe17e9490e1dff`

artifact内で次を確認しました。

- `coverage/mobile/code-coverage.html`
- `coverage/mobile/files/*.html` 19件
- file pageに`../index.html`への戻るlink
- file pageに`covered`、`uncovered`、`not-coverable`の行状態
- indexから`files/*.html`へのlink

`gh-pages/files/CompareConfiguration.cs-97dc2060ffbf.html`の実体もGitHub connectorで確認し、source行、hit count、状態label、一覧へ戻るlink、生成元HEADを確認しました。

このreportを保存するcommit後はPR HEADが変わるため、最終current HEAD一致runと最終Pages公開結果はPR本文および完了commentへ記録します。

## 5. CI loop防止

公開先は`gh-pages`だけです。PR branchへ生成HTMLをcommitしません。

- PR HEADはcoverage公開で変化しない。
- `gh-pages` pushはPR workflowのtriggerに一致しない。
- remote PR HEADがrun開始時のHEADから変化した場合は古いreportを公開しない。
- `gh-pages` concurrencyで公開競合を抑止する。

ファイル別pageの追加によってCI再実行条件は変更していません。

## 6. 変更対象

- `scripts/generate-mobile-coverage-report.py`
- `.github/workflows/pr-xunit-tests.yml`
- `tests/SSC.Unit.Tests/MobileCoveragePerFilePageUnitTests.cs`
- `tests/SSC.Unit.Tests/MobileCoverageReportGeneratorUnitTests.cs`
- `tests/SSC.Unit.Tests/Issue54CodeCoverageWorkflowContractUnitTests.cs`
- `doc/design/detail/13-CodeCoverageVisualization.md`

## 7. 非対象

- coverage thresholdによるmerge gate
- 過去PRごとのPages URL保持
- 外部coverage SaaS
- PRのmerge
