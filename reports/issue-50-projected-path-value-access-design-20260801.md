# Issue #50 投影済み差分path値参照 設計報告

## メタデータ

- Repository: `ssaattww/SSC`
- Issue: `#50 ParallelDiffPathで値にアクセス`
- Branch: `design/issue-50-projected-path-value-access`
- Base: `main`
- Report type: design / implementation report
- Merge: 未実施

## 目的

既存の `IParallelDiffPathProjector` を維持し、利用側定義pathに対応する差分entryの検索と、各model slotの値・状態への自然なアクセス方法を設計する。

## 決定事項

1. 利用側定義pathの生成は既存どおり `IParallelDiffPathProjector` を使用する。
2. `ParallelDiffEntryPathProjection` に `Count`、インデクサー、`GetState(int)` を追加する。
3. `GetDiffEntryPathProjections()` に利用側定義pathの完全一致overloadを追加する。
4. `GetDiffEntryPathProjections()` に既存 `ParallelDiffPathPattern` を使用するpattern検索overloadを追加する。
5. 異なる標準pathが同じ利用側定義pathへ投影される可能性を考慮し、検索結果は常に複数件を返せる一覧型とする。
6. 同一pathの結果は重複排除しない。
7. wildcardとescapeの文法は既存 `ParallelDiffPathPattern` の契約を再利用し、新規文法を追加しない。

## 変更ファイル

- `doc/design/detail/12-DiffEntryProjectedPathValueAccess.md`
  - API契約、例外契約、検索契約、非対象、TDDテスト観点を追加。
- `doc/design/README.md`
  - 新設計書を索引へ追加。
- `reports/issue-50-projected-path-value-access-design-20260801.md`
  - 本報告書。

## 非対象

- 利用側定義pathからnodeへの逆引き
- 利用側定義pathから標準pathへの逆変換
- 同一pathの自動集約または重複排除
- 複数projectorの一括適用
- `ParallelDiffPathPattern` の文法拡張
- 標準pathの既存契約変更

## 診断artifact workflow

`.github/workflows/pr-xunit-tests.yml` と、そのartifact契約を検証する `GitHubActionsTestArtifactContractUnitTests` がリポジトリに存在することを確認した。今回の変更は設計書のみであり、workflow変更は行っていない。

## 検証

- 設計書の新規作成: 完了
- 設計索引への追加: 完了
- production code変更: なし
- test code変更: なし
- CI: PR作成後に対象HEADと一致するrunのみ確認する

## 残存事項

- 実装は未着手。
- 実装時は設計書のテスト観点に従い、先に失敗するtestを追加してTDDで進める。
- API名とXML documentationは実装reviewで最終確認する。

## 次のアクション

本設計をPRで確認後、同一PR上でTDDによる実装へ進む。
