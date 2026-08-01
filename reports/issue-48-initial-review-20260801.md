# PR #49 初回レビュー報告

## 1. レビュー概要

- Repository: `ssaattww/SSC`
- Pull request: `#49`
- Issue: `#48`
- Review mode: 初回独立レビュー
- Reviewed base: `ce57238404db8e27e5ccb031885508a855d0895b`
- Reviewed implementation HEAD: `4ab67afbacb6de8f156b7f55619ac8359b16cf71`
- Reviewed range: `ce57238404db8e27e5ccb031885508a855d0895b...4ab67afbacb6de8f156b7f55619ac8359b16cf71`
- Reviewer: ChatGPT `GPT-5.6 Thinking`
- Review date: 2026-08-01（Asia/Tokyo）
- Verdict: **不合格（修正必須1件）**

このレビューでは実装修正を行っていない。変更差分、直接依存先、公開API契約、設計書、テスト、workflow、およびreview対象HEADと一致するCI evidenceを確認した。

## 2. 要求事項

Issue #48が要求する契約は次のとおりである。

- pattern自身への完全一致を維持する
- patternが祖先pathに一致した場合、その配下の子node・属性・値の差分にも一致する
- `Root.A` と `Root.AA` をpath segment境界で区別する
- 既存利用箇所への後方互換性上の影響を確認する
- 再現テストを先に追加し、失敗を確認してからproduction codeを修正する

追加のrepository作業方針として、テスト失敗時の原因調査に必要なtest結果、標準出力、標準エラー、および診断ログをartifactへ保存し、CI判定にはPR current HEAD SHAとrun head SHAが一致するrunだけを使用する。

## 3. 変更範囲

PR #49はmainに対して20 commit進んでおり、behindは0である。変更ファイルは次の8件である。

- `.github/workflows/pr-xunit-tests.yml`
- `Design/BreakingChanges.md`
- `doc/design/detail/10-DiffEntryPathFilter.md`
- `reports/issue-48-implementation-20260731.md`
- `reports/issue-48-tdd-red-20260731.md`
- `src/SSC/ParallelDiffPathPattern.cs`
- `tests/SSC.Unit.Tests/GitHubActionsTestArtifactContractUnitTests.cs`
- `tests/SSC.Unit.Tests/ParallelDiffPathPatternAncestorUnitTests.cs`

直接依存先として、少なくとも次も確認した。

- `src/SSC/ParallelDiffPathProjection.cs`
- `tests/SSC.Unit.Tests/ParallelDiffPathProjectionUnitTests.cs`
- `doc/design/detail/11-DiffEntryCustomPath.md`
- `tests/SSC.Unit.Tests/ParallelDiffPathPatternUnitTests.cs`
- `src/SSC/Internal/XPathLikePathParser.cs`
- `tasks/tasks-status.md`

## 4. 実装確認

`ParallelDiffPathPattern.IsMatch`のproduction code変更は、候補pathとpatternのsegment数を完全一致させる判定から、候補pathがpatternより浅い場合だけ不一致とする判定への変更である。

```csharp
// before
parsedPath.Segments.Count != _segments.Count

// after
parsedPath.Segments.Count < _segments.Count
```

その後の比較は従来どおり、patternの各segmentを候補pathの先頭から構造比較している。

- member名は`StringComparison.Ordinal`による完全一致
- selectorなしは候補側もselectorなしの場合だけ一致
- `[*]`はkeyまたはordinal selectorへ一致
- exact key selectorとexact ordinal selectorを区別
- selector escapeの既存parser契約を維持

文字列prefix比較ではなく解析済みsegment比較であるため、`Root.A`は`Root.AA`または`Root.AA.B`へ誤一致しない。候補pathがpatternより浅い場合も不一致となる。Issue #48の中心となるmatching実装は妥当である。

## 5. テスト確認

新規`ParallelDiffPathPatternAncestorUnitTests`は次を確認している。

- pattern自身への完全一致
- 子node、属性、値を含む子孫path
- `Root.A`と`Root.AA`のsegment境界
- patternより浅い候補path
- wildcard selectorを含む祖先pattern
- LINQ `Where`による子孫差分の一括除外

既存`ParallelDiffPathPatternUnitTests`により、exact selector、wildcard selector、key/ordinal区別、escape、不正path、null、既存LINQ filter、`CompareIgnore`およびresult state不変も継続して確認されている。

ただし、後述の修正必須事項にあるとおり、利用側定義pathの公開`PathMatches`に対する祖先一致テストが不足している。

## 6. TDD確認

production code変更前のHEAD `6554df35677f58f3bc62e2002beaa63a1ad94439`では、`ParallelDiffPathPattern.IsMatch`が従来のsegment数完全一致判定のままであることを確認した。

同HEADに対するrun `30635911042`では次の結果が記録されている。

- Unit: 79件成功、6件失敗
- E2E: 88件成功、0件失敗
- 失敗は新規`ParallelDiffPathPatternAncestorUnitTests`だけ
- artifact ID: `8795308803`
- artifact SHA-256: `1dae134537650ddc5936320de837b5cccc5268146e3188e6b1fdd7d68f5d996e`

失敗内容は、子孫path4ケース、wildcard selector祖先一致1件、LINQ filter1件であり、変更前の仕様との差分を正しく赤確認している。production code変更前に再現テストを追加するTDD順序は確認できた。

## 7. Current-HEAD CIと診断artifact

レビュー対象implementation HEADは`4ab67afbacb6de8f156b7f55619ac8359b16cf71`である。このSHAに紐づくworkflow runだけをCI evidenceとして使用した。

- Workflow: `PR .NET Tests`
- Run ID: `30637346747`
- Run head SHA: `4ab67afbacb6de8f156b7f55619ac8359b16cf71`
- Status: completed
- Conclusion: success
- Artifact ID: `8795907479`
- Artifact SHA-256: `6324b3d7b0610267e915eefda2ef5be123f961f0a3db90694016c118fbe2761c`
- Artifact files: 23件

artifactを展開して次を確認した。

- manifestにPR HEAD SHAが記録され、レビュー対象implementation HEADと一致
- Unit TRXとE2E TRXが存在
- generator restore/buildの標準出力・標準エラーが存在
- Unit/E2E restore/testの標準出力・標準エラーが存在
- `dotnet --info`、git状態、runner情報、project一覧が存在
- generator build: warning 0、error 0
- Unit: 85件成功、失敗0件
- E2E: 88件成功、失敗0件
- 合計: 173件成功、失敗0件
- stderr logはすべて空

E2E標準出力には既存`ContainerAndSelectManyE2ETests.cs(34,47)`の`CS8603` warningが1件記録されている。Issue #48による新規test failureまたはbuild errorはない。

## 8. 指摘事項

### PR49-R1 [Medium][Required] 利用側定義pathの公開`PathMatches`が影響範囲・設計・テストから漏れている

#### 該当箇所

- `src/SSC/ParallelDiffPathPattern.cs`
- `src/SSC/ParallelDiffPathProjection.cs`
- `tests/SSC.Unit.Tests/ParallelDiffPathProjectionUnitTests.cs`
- `doc/design/detail/11-DiffEntryCustomPath.md`
- `Design/BreakingChanges.md`
- `reports/issue-48-implementation-20260731.md`

#### 内容

PR #49は`ParallelDiffPathPattern.IsMatch`の意味をexact matchからancestor/subtree matchへ変更している。このpublic methodは標準差分entryの`ParallelDiffEntryPathExtensions.PathMatches`だけでなく、利用側定義pathを扱う次の公開extensionからも直接呼ばれる。

```csharp
public static bool PathMatches(
    this ParallelDiffEntryPathProjection projection,
    ParallelDiffPathPattern pattern)
{
    ArgumentNullException.ThrowIfNull(projection);
    ArgumentNullException.ThrowIfNull(pattern);
    return pattern.IsMatch(projection.ProjectedPath);
}
```

したがって、利用側定義pathでも従来は不一致だった子孫pathが新たに一致し、filter結果が変化する。

一方、PR内の影響範囲記録は次の2 APIだけを対象としている。

- `ParallelDiffPathPattern.IsMatch(string)`
- `ParallelDiffEntryPathExtensions.PathMatches(...)`

`ParallelDiffEntryPathProjectionExtensions.PathMatches(...)`が次から漏れている。

- `Design/BreakingChanges.md`の対象APIと互換性説明
- `doc/design/detail/11-DiffEntryCustomPath.md`の利用側定義path matching契約
- `src/SSC/ParallelDiffPathProjection.cs`のXML documentation
- ancestor patternを利用側定義pathへ適用するunit test
- 実装報告の影響範囲と検証内容

既存projection testは`Entry[*].Name`と`Entry[0].Name`の完全一致だけを確認しており、`Entry[*]`が`Entry[0].Name`へ祖先一致する新契約と、類似する別memberへ誤一致しない境界を固定していない。

#### 影響

利用側定義pathで差分を分類・除外する利用者にもruntime behavior変更が発生するにもかかわらず、公開compatibility記録から読み取れない。将来、標準pathだけを対象にした変更として誤って回帰させる可能性もある。

#### 必須対応

1. `ParallelDiffEntryPathProjectionExtensions.PathMatches(...)`を`Design/BreakingChanges.md`の対象APIへ追加する。
2. `src/SSC/ParallelDiffPathProjection.cs`のXML documentationを、pattern自身または祖先が`ProjectedPath`へ一致する契約に更新する。
3. `doc/design/detail/11-DiffEntryCustomPath.md`へ利用側定義pathにも祖先一致規則が適用されることを明記する。
4. `ParallelDiffPathProjectionUnitTests`へ少なくとも次のテストを追加する。
   - projected ancestor patternがprojected descendant pathへ一致する
   - segment境界が異なるprojected sibling pathへ一致しない
   - 標準path側の判定を変更しない
5. 実装報告の影響範囲と検証内容を更新する。
6. 修正後のPR current HEAD SHAと一致するworkflow runおよびartifactを確認する。

## 9. 観点別結果

| 観点 | 結果 | 根拠 |
|---|---|---|
| 要求事項との一致 | 条件付き適合 | Issue #48の中心matching動作は実現。公開projection APIの影響記録に漏れ |
| production correctness | 適合 | segment構造比較と深度判定は妥当 |
| 境界条件 | 適合 | shorter candidate、類似member、selector境界を確認 |
| parser/escape互換性 | 適合 | parserロジック未変更、既存test成功 |
| scope discipline | 適合 | Issue #48と診断artifact方針に限定 |
| 直接依存先 | 不適合 | projected path公開extensionの契約更新漏れ |
| public API/互換性 | 不適合 | runtime behavior変更対象APIの列挙不足 |
| tests | 不適合 | projected path祖先一致の回帰test不足 |
| TDD | 適合 | production変更前の新規6件失敗を確認 |
| workflow/artifact | 適合 | test結果、stdout、stderr、診断情報をcurrent-HEAD artifactで確認 |
| current-HEAD CI | 適合 | implementation HEADとrun head SHAが一致し173件成功 |
| security | 該当なし | path matchingとCIログ保存の変更で新規外部入力実行・権限拡張なし |
| documentation | 不適合 | custom path設計・XML・breaking changeの影響範囲漏れ |
| maintainability | 適合 | production変更は局所的で既存segment比較を再利用 |

## 10. 保留・未実施項目

- connectorとGitHub Actions artifactを用いたレビューであり、reviewer環境での独立した`dotnet test`、`dotnet format --verify-no-changes`、`git diff --check`は実行していない。
- current-HEAD workflowはUnit/E2Eとgenerator buildを成功させているが、formatおよびdiff checkを実行するworkflowではない。
- repositoryにMarkdown lintのcurrent-HEAD実行証跡は確認できなかった。

上記は今回の修正必須判定の原因ではない。PR49-R1の修正後、repositoryで要求される追加検証がある場合は合わせて実施する。

## 11. 結論

Issue #48の中心となる祖先path matching実装、segment境界、TDD赤確認、およびcurrent-HEAD CIは妥当である。

ただし、同じ`ParallelDiffPathPattern.IsMatch`を使用する公開`ParallelDiffEntryPathProjectionExtensions.PathMatches(...)`にもbehavior変更が波及する。この直接依存先について、公開契約、breaking change記録、設計、XML documentation、回帰testが不足している。

そのため、PR #49の初回レビュー結果は**不合格**とする。PR49-R1を修正し、新しいimplementation HEADに対して同一レビュアーによる指摘対応確認を行う必要がある。mergeは利用者が行うため、このレビューでは実施しない。

## 12. Handoff packet

```yaml
schema_version: 3
producer:
  skill: chat-review-worker
  mode: initial_review
  reviewer_identity: GPT-5.6 Thinking / ChatGPT review chat
  repository: ssaattww/SSC
  pull_request: 49
  issue: 48
  branch: agent/issue-48-ancestor-path-match
  base_sha: ce57238404db8e27e5ccb031885508a855d0895b
  reviewed_implementation_head: 4ab67afbacb6de8f156b7f55619ac8359b16cf71
  independence:
    inherited_conversation: false
    implementation_or_fix_actions_performed: false
scope:
  changed_files: 8
  direct_dependencies_checked:
    - src/SSC/ParallelDiffPathProjection.cs
    - tests/SSC.Unit.Tests/ParallelDiffPathProjectionUnitTests.cs
    - doc/design/detail/11-DiffEntryCustomPath.md
    - tests/SSC.Unit.Tests/ParallelDiffPathPatternUnitTests.cs
    - src/SSC/Internal/XPathLikePathParser.cs
validation:
  tdd_red:
    head_sha: 6554df35677f58f3bc62e2002beaa63a1ad94439
    run_id: 30635911042
    unit_passed: 79
    unit_failed: 6
    e2e_passed: 88
    artifact_id: 8795308803
  current_head_ci:
    head_sha: 4ab67afbacb6de8f156b7f55619ac8359b16cf71
    run_id: 30637346747
    conclusion: success
    unit_passed: 85
    e2e_passed: 88
    total_passed: 173
    artifact_id: 8795907479
findings:
  - id: PR49-R1
    severity: medium
    disposition: required
    origin: coverage_miss
    summary: 利用側定義pathの公開PathMatchesが影響範囲・設計・テストから漏れている
    required_actions:
      - BreakingChangesへParallelDiffEntryPathProjectionExtensions.PathMatchesを追加
      - projection PathMatchesのXML documentationを祖先一致契約へ更新
      - custom path設計へ祖先一致規則を追加
      - projected path祖先一致と境界のunit testを追加
      - 実装報告を更新
      - 修正後current HEADと一致するCI runを確認
verdict: fail
next_action:
  mode: implementation_fix
  follow_up_review: same_reviewer_fix_verification
  merge_allowed: false
```
