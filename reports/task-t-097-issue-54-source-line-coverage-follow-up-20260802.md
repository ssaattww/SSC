# T-097 Issue #54 行単位カバレッジ表示 フォローアップ実装レポート

## 1. 対象

- Repository: `ssaattww/SSC`
- Issue: #54
- Pull Request: #55
- Branch: `feature/issue-54-code-coverage`
- Base: `main`
- 利用者要望: スマートフォン向けレポートで、どのソース行が実行済みか判別できるようにする

## 2. 問題

従来のスマートフォン向けレポートは、method単位で `行カバー済み`、`一部カバー`、`未実行` を表示していた。しかし、methodを開いてもソースコードの各行とcoverage結果が同一画面に並ばず、利用者が「どの行がカバーされているか」を直接確認できなかった。

## 3. 実装範囲

`scripts/generate-mobile-coverage-report.py` を拡張し、Coberturaとcheckout済みsourceからファイル別の行一覧を生成した。

各行は次の3状態で表示する。

- `実行済み`: Coberturaの`hits`が1以上
- `未実行`: Coberturaのcoverage対象行で`hits`が0
- `対象外`: Coberturaにcoverage対象として記録されていない行

各source fileには次を表示する。

- 行番号
- 行状態
- hit数
- source code
- 実行済み／未実行／対象外の件数
- GitHub上の同一行へのリンク

method名をタップすると、同じ単一HTML内の対応source行へ移動し、対象fileの折りたたみを自動的に開く。source textとfile名による検索、および行状態による絞り込みも追加した。

## 4. TDD

### 4.1 Red

先に `MobileCoverageReportGeneratorUnitTests.GenerateReport_ShowsCoverageStateForEverySourceLine` を追加した。

要求した契約は次のとおり。

- `行 / 行状態 / Hits / Source` のtable header
- `covered`、`uncovered`、`not-coverable` の各行状態
- `実行済み`、`未実行`、`対象外` の表示
- HTML escape済みsource code表示

Red evidence:

- HEAD: `0eab3f8769d209278d27c021d1294904327cf48e`
- Matching workflow run: `30750577204`
- Conclusion: failure
- 新規test: 1件失敗
- 既存Unit Tests: 102件成功
- E2E Tests: 88件成功
- 失敗理由: generatorが新しい`--source-root`引数を未実装
- Diagnostic artifact: `8834311159`
- Artifact digest: `sha256:c65ac882ac11a3b7992222b370ca6a8621c8ed7660de0eea05c0180b0f73adc0`

### 4.2 Green

実装後、同じ契約testを含む全testとPages公開を確認した。

- Implementation HEAD: `013a2d1487dd7b9db89bfcfbb32f8ad4978cd377`
- Matching workflow run: `30750770391`
- Conclusion: success
- Unit Tests: 103件成功
- E2E Tests: 88件成功
- Test/diagnostic artifact: `8834369194`
- Artifact digest: `sha256:18bcafc5fd84e65fb1fac266523427428781d58bc1f4c90b70dc34cc9f1c6b81`
- GitHub Pages artifact: `8834368847`
- Pages artifact digest: `sha256:df806e545484d15f92ea0e6aafb2528d94203a2390711fbf94fbfeb344ce5a79`
- Pages deployment: success
- `gh-pages/source-head.txt`: `013a2d1487dd7b9db89bfcfbb32f8ad4978cd377`

別SHAのrunは代用していない。

## 5. 生成物の実体確認

matching runのdiagnostic artifactを取得し、`coverage/mobile/code-coverage.html`を検査した。

- 単一HTML size: 3,396,326 bytes
- source files: 19
- `実行済み` rows: 1,748
- `未実行` rows: 244
- `対象外` rows: 2,767
- coverable rows合計: 1,992
- overall line coverage: `1,748 / 1,992 = 87.8%`

HTML内で次を確認した。

- `行別カバレッジ` section
- `<th>行</th><th>行状態</th><th>Hits</th><th>Source</th>`
- `data-line-status="covered"`
- `data-line-status="uncovered"`
- `data-line-status="not-coverable"`
- source codeのHTML escape
- file別summaryと行filter

## 6. 変更ファイル

- `tests/SSC.Unit.Tests/MobileCoverageReportGeneratorUnitTests.cs`
  - 行単位表示のRed/Green契約test
- `scripts/generate-mobile-coverage-report.py`
  - source file読込、file単位hit統合、行table、検索、filter、内部anchor
- 本レポート
- handoff packet

production APIとSSC本体のproduction sourceは変更していない。

## 7. 注意事項

- `対象外`は「テストされていない」という意味ではなく、Coberturaが実行可能行として記録していない行を示す。
- `実行済み`のhit数は、その行を通過した回数であり、assertの妥当性や入力網羅性を保証しない。
- branch coverageは行coverageとは別である。行が実行済みでも、同じ行の条件分岐が未網羅の場合がある。
- source fileがcheckout内に存在しない場合は、単一HTMLで`ソース取得不可`を表示し、GitHub linkのみ提供する。
- 単一HTMLは約3.4MBへ増加したが、ZIP展開不要でスマートフォンから直接閲覧できる要件を優先した。

## 8. 公開先

- GitHub Pages: `https://ssaattww.github.io/SSC/`
- 永続保存branch: `gh-pages`
- PR branchへcoverage publication commitは行わないため、公開によるPR HEAD更新や無限CIは発生しない。

## 9. マージ境界

mergeは実施していない。独立レビューと利用者判断後に利用者がmergeする。
