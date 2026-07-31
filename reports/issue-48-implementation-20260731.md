# Issue #48 実装報告

## 1. 概要

`ParallelDiffPathPattern` がpattern自身に完全一致する場合だけでなく、patternが差分pathの祖先に一致する場合も、その配下の子孫差分へ一致するよう修正した。

```text
Pattern: Root.A

一致:
Root.A
Root.A.B
Root.A.B.C
Root.A.Attribute[Width].Value

不一致:
Root
Root.AA
Root.AA.B
```

Pull requestは #49、作業branchは `agent/issue-48-ancestor-path-match` である。mergeは実施していない。

## 2. 使用したSKILL

この会話で利用可能なSKILL一覧を確認し、次を使用した。

- `github`: Issue、repository、branch、PR、差分、workflow run、artifactの参照と更新
- `yeet`: 小さな論理単位のcommit、push済みbranch、Draft PR作成と公開手順の方針

`chat-implementation-worker` は利用可能なSKILL一覧およびrepository内の参照から確認できなかったため、使用していない。

## 3. 要件

Issue #48の確認事項を次のように実装・検証した。

- 完全一致時の既存動作を維持する
- 祖先pattern配下の子node、属性、値の差分へ一致する
- `Root.A` と `Root.AA` をpath segment境界で区別する
- selector wildcardを含む祖先patternでも同じ規則を適用する
- 既存parser、selector、escape、例外契約を維持する
- 再現テストを先に追加して失敗を確認してからproduction codeを変更する

## 4. 原因

既存の `ParallelDiffPathPattern.IsMatch` は、候補pathとpatternのsegment数が異なる場合を一律に不一致としていた。

```csharp
parsedPath.Segments.Count != _segments.Count
```

そのため、pattern `Root.A` と候補 `Root.A.B` は先頭2 segmentが一致していても、segment数が異なるだけで不一致になっていた。

## 5. 診断artifact workflow

作業開始時に `.github/workflows/pr-xunit-tests.yml` を確認した。既存workflowはTRXをartifactへ保存していたが、restore/testの標準出力・標準エラー、および失敗原因調査用の実行環境情報を個別ファイルとして保存していなかった。

次を追加した。

- source generator restoreの標準出力・標準エラー
- source generator buildの標準出力・標準エラー
- test project restoreの標準出力・標準エラー
- test project testの標準出力・標準エラー
- Unit/E2EのTRX
- `dotnet --info` の標準出力・標準エラー
- `git status --short --branch` の標準出力・標準エラー
- repository、workflow commit、PR HEAD SHA、run ID、runner OS/architecture
- test projectとgenerator projectの一覧
- artifact内ファイル一覧を含むmanifest

artifactは失敗時を含め `always()` で作成し、7日間保持する。workflow契約は `GitHubActionsTestArtifactContractUnitTests` で固定した。

## 6. TDD赤確認

production codeを変更する前に `ParallelDiffPathPatternAncestorUnitTests` を追加した。

### 6.1 追加したテスト観点

- pattern自身への完全一致
- 子nodeへの祖先一致
- 属性・値への祖先一致
- `Root.A` と `Root.AA` のsegment境界
- patternより候補pathが浅い場合の不一致
- wildcard selectorを含む祖先一致
- selectorなし・別memberへの不一致
- LINQ `Where` による子孫差分の一括除外

### 6.2 赤確認結果

- PR HEAD SHA: `6554df35677f58f3bc62e2002beaa63a1ad94439`
- Workflow run: `30635911042`
- Workflow run head SHA: `6554df35677f58f3bc62e2002beaa63a1ad94439`
- 結論: failure
- E2E: 88件成功、失敗0件
- Unit: 79件成功、6件失敗、合計85件

失敗は新規祖先一致テスト6件だけに限定された。詳細は `reports/issue-48-tdd-red-20260731.md` に記録した。

### 6.3 赤確認artifact

- Artifact ID: `8795308803`
- Artifact名: `ssc-pr-test-results-30635911042-1`
- SHA-256: `1dae134537650ddc5936320de837b5cccc5268146e3188e6b1fdd7d68f5d996e`
- 保存ファイル数: 19

TRX、各test projectのrestore/test標準出力・標準エラー、.NET・git・runner診断情報を展開して確認した。

## 7. 実装

`ParallelDiffPathPattern.IsMatch` の深度判定を次のように変更した。

```csharp
parsedPath.Segments.Count < _segments.Count
```

候補pathがpatternより浅い場合だけ不一致とし、patternの全segmentを候補pathの先頭から既存の構造比較で照合する。

比較は引き続き次の単位で行う。

- member名: `StringComparison.Ordinal` の完全一致
- selectorなし: 候補側もselectorなしの場合だけ一致
- `[*]`: keyまたはordinal selectorへ一致
- exact key selector: key文字列の完全一致
- exact ordinal selector: ordinal値の一致
- key selectorとordinal selectorは区別

文字列の `StartsWith` は使用していない。このため、`Root.A` と `Root.AA` は別segmentとして不一致になる。

## 8. 設計・互換性

`doc/design/detail/10-DiffEntryPathFilter.md` を次の契約へ更新した。

- patternの全segmentが候補pathの先頭から一致した場合、残りのsegmentを子孫pathとして許容する
- patternより浅い候補pathは不一致
- segment、selector境界を維持する
- member wildcard、任意深度wildcardは追加しない
- 比較結果そのもの、`CompareIgnore`、`Issues`、`HasDifferences()` は変更しない

public API shape、完全一致、parser、selector、escape、例外契約は変更していない。

一方、従来は不一致だった子孫pathが一致するため、外部利用者が子孫を意図的に残していた場合はfilter結果が変化する。これをpublic runtime behaviorの拡張として `Design/BreakingChanges.md` に記録した。

## 9. 変更ファイル

- `.github/workflows/pr-xunit-tests.yml`
- `Design/BreakingChanges.md`
- `doc/design/detail/10-DiffEntryPathFilter.md`
- `reports/issue-48-tdd-red-20260731.md`
- `reports/issue-48-implementation-20260731.md`
- `src/SSC/ParallelDiffPathPattern.cs`
- `tests/SSC.Unit.Tests/GitHubActionsTestArtifactContractUnitTests.cs`
- `tests/SSC.Unit.Tests/ParallelDiffPathPatternAncestorUnitTests.cs`

Issue #48以外の既存設計記述へ混入した意図しない差分は最終点検で除去した。

## 10. 実装検証

詳細report追加前の実装・設計・workflow HEADで検証した。

- PR HEAD SHA: `8aaab35d2258bbb181af9aa364a272e92aa4a3b6`
- Workflow run: `30637137705`
- Workflow run head SHA: `8aaab35d2258bbb181af9aa364a272e92aa4a3b6`
- 結論: success
- source generator build: 成功、warning 0、error 0
- E2E: 88件成功、失敗0件
- Unit: 85件成功、失敗0件
- 合計: 173件成功、失敗0件

既存の `ContainerAndSelectManyE2ETests.cs(34,47)` に `CS8603` warningが1件ある。Issue #48の変更による新規errorまたはtest failureはない。

### 10.1 実装検証artifact

- Artifact ID: `8795815749`
- Artifact名: `ssc-pr-test-results-30637137705-1`
- SHA-256: `b14a4a11bd05340f1696a456d972d000fd044faadf846bbc0b88e34c26c2e526`
- 保存ファイル数: 23

artifactを展開し、次を確認した。

- manifestのPR HEAD SHAが実装検証HEADと一致
- Unit/E2E TRXが存在
- generator restore/buildの標準出力・標準エラーが存在
- test restore/testの標準出力・標準エラーが存在
- 診断情報が存在
- すべてのstderr logが空

本report追加後の最終PR HEADについても、そのHEAD SHAと一致するworkflow runのみを最終CIとして確認し、run情報をPRコメントへ記録する。

## 11. 状態

- Issue #48対応: 実装済み
- PR #49: 作成済み
- 詳細設計: 更新済み
- TDD赤確認: 完了
- 全テスト: 成功
- 診断artifact: 保存・内容確認済み
- merge: 未実施
