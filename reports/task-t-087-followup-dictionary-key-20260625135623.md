# Sub-agent実行レポート

## タスク
T-087 follow-up: Dictionary generated access を key 型 indexer にする

## sub-agentを使う理由
既存 PR #40 の follow-up として generator/runtime/tests の複数領域を TDD で修正するため。

## 対象範囲
- Dictionary member を `ParallelGeneratedDictionary<TKey, TElement, TView>` として生成する
- `dict[key]` を typed raw key access にする
- path bracket discriminator access は `ByPathKey(string)` で提供する
- key union 順 access は `AtIndex(int)` へ分離する
- string key と non-string key の E2E を追加する

## 対象外
- dynamic projection の key access 変更
- sequence/List の key access 全般の再設計
- object/composite key の path 復元
- PR 作成、最終 review、tracking 完了処理

## 実行コマンド
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_DictionaryIntKeyAccess_UsesRawKey"`
  - 実装前 failing proof: `root.Scores[100]` が key access ではなく `ParallelGeneratedList` の ordinal index access として解決され、`CompareExecutionException: list index '100' is out of range for count '3'.` で失敗することを確認。
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests|FullyQualifiedName~XmlCustomGeneratedCompareE2ETests"`
  - 実装後: Passed. Failed: 0, Passed: 13, Skipped: 0.
  - レビュー対応後: Passed. Failed: 0, Passed: 15, Skipped: 0.
  - Attribute value key access test 追加後: Passed. Failed: 0, Passed: 15, Skipped: 0.
  - 既存 warning: `ContainerAndSelectManyE2ETests.cs(34,47): warning CS8603`。
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_DictionaryStringKeyAccess_UsesConfiguredKeyComparison|FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_DictionaryDateTimeKeyAccess_UsesNormalizedKey"`
  - レビュー対応前 failing proof: case-insensitive string key の `root.Scores["a"]` と DateTime normalized key が `KeyNotFound` で失敗することを確認。
  - レビュー対応後: Passed. Failed: 0, Passed: 2, Skipped: 0.
- `git diff --check`
  - Passed.
- `dotnet test SSC.sln --configuration Release`
  - Passed. E2E: Failed 0, Passed 71, Skipped 0. Unit: Failed 0, Passed 29, Skipped 0.
- `dotnet format SSC.sln --verify-no-changes`
  - Passed.
- Markdown lint
  - Unsupported: repo-local `tools/lint/` が空で、`package.json` の lint script も未設定。

## 対象ファイル
- `src/SSC.Generators/ParallelViewGenerator.cs`
- `src/SSC/GeneratedProjectionRuntime.cs`
- `src/SSC/ParallelCompareApi.cs`
- `src/SSC/ParallelNode.cs`
- `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
- `tests/SSC.E2E.Tests/XmlCustomGeneratedCompareE2ETests.cs`
- `reports/task-t-087-followup-dictionary-key-20260625135623.md`

## 指摘事項
- Dictionary member の generated property を `ParallelGeneratedList<TElement,TView>` から `ParallelGeneratedDictionary<TKey,TElement,TView>` へ分離。
- Dictionary の `this[TKey]` は raw key 型アクセスとして扱い、key union 順の ordinal access は `AtIndex(int)` に分離。
- `GetDiffEntries().Path` の bracket 内 discriminator は `ByPathKey(string)` で unescape してアクセスする形に更新。
- `CompareIssueCode.KeyNotFound` は enum 末尾のまま変更なし。
- レビュー対応として、child node に compare runtime の normalized key object と key comparer を internal metadata として保持。
- `ParallelGeneratedDictionary<TKey,TElement,TView>` の raw key indexer は `KeyText` ではなく `KeyValue` cache で lookup。
- `ByPathKey(discriminator)` と `ParallelGeneratedList` の string keyText access は引き続き `KeyText` cache を使用。

## 結果
- `Dictionary<int, int>` の generated member で `root.Scores[100]` が int key としてアクセスできる E2E を追加。
- `Dictionary<string, GeneratedXmlAttribute>` の `root.Root.Attribute["id"]` raw key access は維持。
- CustomXML の `Attribute["source"].Value` と nested `Attribute["name"].Value` を E2E で直接検証。
- `StringKeyComparison.OrdinalIgnoreCase` の `Dictionary<string, int>` generated access で `root.Scores["A"]` / `root.Scores["a"]` が同じ child を引ける E2E を追加。
- `Dictionary<DateTime, int>` generated access で元 key と `DateTime.ToUniversalTime()` 相当 key のどちらでも同じ child を引ける E2E を追加。
- 既存 dictionary ordinal access は `AtIndex(...)` に更新。
- diff path selector access は `ByPathKey(selector)` に更新。
- missing key は `CompareExecutionException` / `CompareIssueCode.KeyNotFound` で検証。
- 指定 E2E フィルタ、full test、format verify、whitespace check は成功。

## リスク
- object/composite key の path 復元は対象外。
- `KeyValue` を持たない ordinal keyless sequence は対象外で、既存の list/index behavior を維持。
- Markdown lint は repo-local wiring がないため未実行。
