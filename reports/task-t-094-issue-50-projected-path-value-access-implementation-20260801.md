# T-094 Issue #50 投影済み差分pathからの値参照API 実装レポート

## 1. 対象

- Repository: `ssaattww/SSC`
- Issue: #50
- Pull Request: #51
- Branch: `design/issue-50-projected-path-value-access`
- Base: `main`
- 実装開始時HEAD: `5129a2dcbbc5943c987d8a8d081c626263c329b6`
- Green確認HEAD: `7caa70ecdd1294ab497d8f03b101774386ba99ab`

## 2. 目的と範囲

`IParallelDiffPathProjector` によって生成した利用側定義pathから、差分entryのmodel slot値と状態を直接参照し、完全一致または既存の `ParallelDiffPathPattern` で検索できるAPIを実装した。

対象外は、利用側定義pathからnodeを逆引きする機能、標準pathへの逆変換、重複排除、pattern文法拡張である。

## 3. TDD

先に `tests/SSC.Unit.Tests/Issue50ProjectedPathValueAccessTddTests.cs` を追加した。

Red確認時のPR HEADは `5129a2dcbbc5943c987d8a8d081c626263c329b6`、workflow runは `30684890028` である。

Unit test projectは次の未実装APIによりcompile errorとなった。

- `ParallelDiffEntryPathProjection.Count`
- `ParallelDiffEntryPathProjection` のmodel indexer
- `ParallelDiffEntryPathProjection.GetState(int)`

E2E testは88件成功した。Red runのartifactは `8813546513` である。

## 4. 実装

### 4.1 値と状態の直接参照

`ParallelDiffEntryPathProjection` に次を追加した。

- `Count`
- `this[int modelIndex]`
- `GetState(int modelIndex)`

値と状態は新たに複製せず、既存の `Entry.Values` へ委譲する。負数および `Count` 以上のindexは `ArgumentOutOfRangeException` とする。

### 4.2 投影済みpath検索

`ParallelProjectedPathSearchExtensions` を追加し、次のoverloadを実装した。

- `string projectedPath` によるordinal完全一致検索
- `ParallelDiffPathPattern` によるpattern検索

既存の全件投影APIを再利用し、元の順序と重複を保持する。

### 4.3 診断artifact

`.github/workflows/pr-xunit-tests.yml` を更新し、失敗原因調査用artifactに次を保存するようにした。

- TRX test result
- restore/testの標準出力
- restore/testの標準エラー
- checkout済みソース一式
- checkout HEADとgit status
- manifest

## 5. 変更ファイル

- `.github/workflows/pr-xunit-tests.yml`
- `src/SSC/ParallelDiffPathProjection.cs`
- `src/SSC/ParallelProjectedPathSearchExtensions.cs`
- `tests/SSC.Unit.Tests/Issue50ProjectedPathValueAccessTddTests.cs`
- `reports/task-t-094-issue-50-projected-path-value-access-implementation-20260801.md`

設計ファイル `doc/design/detail/12-DiffEntryProjectedPathValueAccess.md` はPR #51の既存設計を使用した。

## 6. 検証

Green確認時のPR HEADは `7caa70ecdd1294ab497d8f03b101774386ba99ab`、一致するworkflow runは `30685041733` である。

- Workflow: `PR .NET Tests`
- Conclusion: success
- E2E: 88件成功
- Unit: 80件成功
- Artifact: `8813591782` (`ssc-pr-test-diagnostics-30685041733-1`)

artifactを取得して、TRX、stdout、stderr、ソース一式が含まれることを確認した。artifact metadataの `head_sha` はGreen確認HEADと一致する。workflow内のcheckoutはGitHubのPR merge refであるため、保存された `checked-out-head.txt` はmerge commit `0fad15f569b3b5e282c3cee48f86d1762319efb7` を示す。

## 7. 未実施・残留事項

- このレポート追加後のHEADには、新しい一致CI runが必要である。
- `tasks/tasks-status.md` はartifactから全文取得済みだが、GitHub connectorの既存ファイル更新APIは全文送信が必要なため、本レポート作成時点では未commitである。
- 独立レビューは実装者とは別のreview workerで行う必要がある。

## 8. マージ境界

mergeは実施していない。利用者がレビュー完了後に判断する。
