# Sub-agent実行レポート

## タスク
T-087 follow-up: KeyValue/comparer 対応後の再レビュー

## sub-agentを使う理由
review-enforcer によりレビュー指摘対応後の再レビューを同じ reviewer sub-agent で確認するため。

## 対象範囲
- `ParallelNode` への normalized key object / comparer metadata 追加
- generated dictionary の `KeyValue` lookup
- `OrdinalIgnoreCase` string key access
- `DateTime` normalized key access
- T-087 follow-up 差分全体の regression

## 対象外
- object/composite key の path 復元
- dynamic projection の key access 変更
- PR 作成、commit、progress sync

## 実行コマンド
- `git status --short`
- `git diff --stat origin/main...HEAD`
- `git diff --stat`
- `git diff -- src/SSC/ParallelNode.cs src/SSC/ParallelCompareApi.cs src/SSC/GeneratedProjectionRuntime.cs src/SSC.Generators/ParallelViewGenerator.cs tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs tests/SSC.E2E.Tests/XmlCustomGeneratedCompareE2ETests.cs`
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests|FullyQualifiedName~XmlCustomGeneratedCompareE2ETests"`
  - Passed. Failed: 0, Passed: 15, Skipped: 0.
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_DictionaryStringKeyAccess_UsesConfiguredKeyComparison|FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_DictionaryDateTimeKeyAccess_UsesNormalizedKey"`
  - Passed. Failed: 0, Passed: 2, Skipped: 0.
- `git diff --check`
  - Passed.
- `npm run lint:md`
  - Unsupported: `Missing script: "lint:md"`.

## 対象ファイル
- `src/SSC/ParallelNode.cs`
- `src/SSC/ParallelCompareApi.cs`
- `src/SSC/GeneratedProjectionRuntime.cs`
- `src/SSC.Generators/ParallelViewGenerator.cs`
- `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
- `tests/SSC.E2E.Tests/XmlCustomGeneratedCompareE2ETests.cs`
- `doc/design/detail/01-DomainModel.md`
- `doc/design/detail/02-PublicApi.md`
- `tasks/tasks-status.md`
- `tasks/phases-status.md`

## 指摘事項
- No findings.
- Blocking findings: none.
- User-confirmation-required capability gap: none.
- Non-blocking concerns: Markdown lint remains unsupported because the repository has no `lint:md` script.

## 結果
- Previous blocking finding is resolved.
- `src/SSC/ParallelNode.cs:13` now stores internal `KeyValue` and `KeyComparer` metadata on child nodes.
- `src/SSC/ParallelCompareApi.cs:318` and `src/SSC/ParallelCompareApi.cs:499` pass normalized key object and compare runtime key comparer into dictionary/keyed sequence child nodes.
- `src/SSC/GeneratedProjectionRuntime.cs:43` uses `NormalizeKey(key)` and `ResolveByKeyValue(...)` for raw generated dictionary key access instead of `KeyText` lookup.
- `src/SSC/GeneratedProjectionRuntime.cs:144` builds a comparer-backed `KeyValue` cache, so configured string key comparison and DateTime normalization are reused for generated dictionary access.
- `ByPathKey(discriminator)` remains `KeyText`/diff path discriminator based at `src/SSC/GeneratedProjectionRuntime.cs:58`, preserving path selector compatibility.
- Focused tests cover `root.Scores["A"]` / `root.Scores["a"]`, DateTime original/UTC key access, `ByPathKey`, `AtIndex`, and existing generated sequence/list paths.

## リスク
- object/composite key path復元は引き続き対象外。
- `KeyValue` を持たない ordinal keyless sequence は generated dictionary の対象外で、既存 list/index behavior を維持。
- Markdown lint は repo-local script が無いため unsupported。
