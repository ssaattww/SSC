# Issue #48 TDD 赤確認

## 対象

`ParallelDiffPathPattern` に祖先 path を指定した場合、その配下の子孫差分まで一致対象とする契約を追加する。

## 追加した再現テスト

- pattern 自身への完全一致
- 子 node、属性、値を含む子孫 path への一致
- `Root.A` と `Root.AA` を区別する path segment 境界
- wildcard selector を含む祖先 pattern
- LINQ filter による子孫差分の一括除外

## 赤確認結果

- Pull request: #49
- 対象PR HEAD SHA: `6554df35677f58f3bc62e2002beaa63a1ad94439`
- GitHub Actions run: `30635911042`
- Workflow run head SHA: `6554df35677f58f3bc62e2002beaa63a1ad94439`
- 結論: failure
- E2E: 88件成功、失敗0件
- Unit: 79件成功、6件失敗、合計85件

失敗はすべて新規 `ParallelDiffPathPatternAncestorUnitTests` に限定された。

- `IsMatch_WithAncestorPattern_MatchesExactAndDescendantPaths`: 子 node、属性、値の4ケース
- `IsMatch_WithWildcardSelectorAncestor_PreservesSelectorBoundary`: 1件
- `PathMatches_WithAncestorPattern_FiltersAllDescendantDiffs`: 1件

既存実装がpatternと候補pathのsegment数不一致を一律に不一致としているため、期待どおり子孫pathだけが失敗した。

## 診断artifact

- Artifact ID: `8795308803`
- Artifact名: `ssc-pr-test-results-30635911042-1`
- 保存ファイル数: 19
- SHA-256: `1dae134537650ddc5936320de837b5cccc5268146e3188e6b1fdd7d68f5d996e`

保存内容を展開して確認した。

- E2E / UnitのTRX
- 各test projectのrestore標準出力・標準エラー
- 各test projectのtest標準出力・標準エラー
- `dotnet --info`の標準出力・標準エラー
- git状態の標準出力・標準エラー
- runner情報
- test / generator project一覧
- manifest

以上により、production code変更前の失敗と失敗原因の診断情報を確保した。
