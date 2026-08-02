# T-097 Issue #54 ソース行表示密度フォローアップ

## 1. 要望

スマートフォンの行別カバレッジで文字が大きく、一画面に表示できる行数が少ないため、約40行を確認できる密度へ調整する。

## 2. 対象

- Repository: `ssaattww/SSC`
- Issue: #54
- Pull Request: #55
- Branch: `feature/issue-54-code-coverage`
- 公開URL: `https://ssaattww.github.io/SSC/`

## 3. TDD Red

- HEAD: `c78981c0033d7eabb41289d122a22df1954fb5da`
- Matching run: `30765836714`
- 結果: failure（期待どおり）
- E2E Tests: 88件成功
- 既存Unit Tests: 102件成功
- 追加した密度契約test: 1件失敗
- 失敗理由: `.source-table-wrap { max-height:76vh;` が未実装
- 診断artifact: `8838902388`
- artifact digest: `sha256:1c43ae13af9824a25e382d97f706b318d156d2fcf66ea848f87e47af30b434ea`

失敗時もTRX、標準出力、標準エラー、coverage、ReportGenerator log、checkout sourceが診断artifactへ保存された。

## 4. 実装

coverage解析とHTML構築の既存実装は`generate-mobile-coverage-report-core.py`としてそのまま保持した。`generate-mobile-coverage-report.py`は薄い起動wrapperとし、生成HTMLへ行別表示専用CSSを末尾追加する。

- 行別tableのfont size: `10px`
- line-height: `1.15`
- cell padding: 上下`1px`、左右`4px`
- 行状態badge: `8px`
- source表示領域: `76vh`、内部scroll
- table header: sticky
- 見出し、集計card、検索control、method一覧の文字サイズ: 変更なし

`76vh`と約13.5pxの行高により、端末のviewportとbrowser chromeによる差はあるが、おおむね40行前後を同時に確認できる。

## 5. Implementation Green

- HEAD: `a8567f0aa63fa4567696871a765cf08f11987987`
- Matching run: `30766207703`
- Workflow conclusion: success
- Unit Tests: 103件成功
- E2E Tests: 88件成功
- Pages publication: success
- 診断artifact: `8839022756`
- artifact digest: `sha256:cea145b6e834dafaa4cef76d741c4bf53da54a93835313b9d84d487a6510698b`
- Pages artifact: `8839022494`
- Pages artifact digest: `sha256:ddd880db83b53b1a9c279b337a4d61fe2fc9ec8f6c4055d4e826ade9d85eba5a`
- `gh-pages/source-head.txt`: `a8567f0aa63fa4567696871a765cf08f11987987`

Pages artifactを展開し、次のCSSが実際の`index.html`へ入っていることを確認した。

- `.source-table-wrap { max-height:76vh; overflow:auto; ... }`
- `.source-table { ... font-size:10px; line-height:1.15; }`
- `.source-table th,.source-table td { padding:1px 4px; ... }`
- `.source-code code { ... padding:1px 4px; ... }`

## 6. CIループ対策

公開jobはPR branchを変更せず、`gh-pages`だけを更新する。PR workflowは`gh-pages` pushで起動しないため、表示変更後も無限CIは発生しない。

## 7. 最終HEAD検証

本レポートとhandoffの保存でPR HEADが進むため、保存後のcurrent HEADに一致するworkflow runを最終判定とする。別SHAのrunは代用しない。最終run、artifact、Pages公開結果はPR本文とPR完了コメントにも記録する。

## 8. マージ

mergeは実施しない。
