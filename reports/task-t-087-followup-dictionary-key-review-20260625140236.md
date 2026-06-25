# Sub-agent実行レポート

## タスク
T-087 follow-up: Dictionary key 型 indexer 化のコードレビュー

## sub-agentを使う理由
review-enforcer によりレビューは mandatory sub-agent work。Dictionary wrapper 分離が通常利用パスを壊さないか独立確認するため。

## 対象範囲
- `ParallelGeneratedDictionary<TKey,TElement,TView>` 追加
- generator の Dictionary member property 型変更
- `dict[key]` / `AtIndex(index)` / `ByPathKey(discriminator)` の E2E
- 設計書・tracking との整合
- Markdown lint disposition: unsupported

## 対象外
- dynamic projection の key access 変更
- object/composite key の path 復元
- PR 作成、commit、progress sync

## 実行コマンド
- `git diff origin/main...HEAD -- src/SSC/GeneratedProjectionRuntime.cs tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs tests/SSC.E2E.Tests/XmlCustomGeneratedCompareE2ETests.cs doc/design/detail/01-DomainModel.md doc/design/detail/02-PublicApi.md tasks/tasks-status.md tasks/phases-status.md`
- `git diff -- src/SSC.Generators/ParallelViewGenerator.cs src/SSC/GeneratedProjectionRuntime.cs tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs tests/SSC.E2E.Tests/XmlCustomGeneratedCompareE2ETests.cs doc/design/detail/01-DomainModel.md doc/design/detail/02-PublicApi.md tasks/tasks-status.md tasks/phases-status.md`
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests|FullyQualifiedName~XmlCustomGeneratedCompareE2ETests"`
  - Passed. Failed: 0, Passed: 13, Skipped: 0.
- `git diff --check`
  - Passed.
- `npm run lint:md`
  - Unsupported: `Missing script: "lint:md"`.

## 対象ファイル
- `src/SSC.Generators/ParallelViewGenerator.cs`
- `src/SSC/GeneratedProjectionRuntime.cs`
- `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
- `tests/SSC.E2E.Tests/XmlCustomGeneratedCompareE2ETests.cs`
- `doc/design/detail/01-DomainModel.md`
- `doc/design/detail/02-PublicApi.md`
- `tasks/tasks-status.md`
- `tasks/phases-status.md`
- 周辺確認: `src/SSC/ParallelCompareApi.cs`, `doc/design/detail/03-ContainerRules.md`, `doc/design/detail/07-NonFunctional.md`, `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`

## 指摘事項
- Blocking findings
  - `src/SSC/GeneratedProjectionRuntime.cs:42` / `src/SSC/GeneratedProjectionRuntime.cs:84` / `src/SSC/GeneratedProjectionRuntime.cs:250`: `ParallelGeneratedDictionary<TKey,TElement,TView>` の typed indexer は `TKey.ToString()` を `StringComparer.Ordinal` の `KeyText` cache に照合しているため、既存の `CompareConfiguration.StringKeyComparison = OrdinalIgnoreCase` パスと一致しない。比較本体は `src/SSC/ParallelCompareApi.cs:217` の `KeyComparer` と `src/SSC/ParallelCompareApi.cs:795` の canonical `KeyText` 選択で `"a"` / `"A"` を同一 key に統合できるが、generated dictionary は canonical 表記だけを ordinal lookup する。結果として、比較では存在する string dictionary key でも `root.Scores["a"]` が `KeyNotFound` になり得る。`doc/design/detail/03-ContainerRules.md:73` と既存 E2E `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs:721` がこの設定を通常の supported path として扱っているため、typed key indexer の通常パス blocker。
- User-confirmation-required capability gap
  - `src/SSC/GeneratedProjectionRuntime.cs:47`: typed key lookup は実キーではなく `TKey.ToString()` と `KeyText` の一致に依存している。`doc/design/detail/03-ContainerRules.md:104` の DateTime key UTC 正規化など、比較本体側で `NormalizeKey` / `KeyComparer` が `ToString()` 以上の意味を持つ key 型では、同値 key を typed indexer で引ける保証がない。この follow-up で全 `Dictionary<TKey,TValue>` の比較 key semantics まで generated access に持ち込むか、今回の正常系を string/int の `KeyText` 安定型に限定するかは明示確認が必要。
- Non-blocking concerns
  - `ParallelGeneratedList` 側の sequence/List access、LINQ enumeration、`SelectModel` は focused E2E 13 件で維持されている。List の既存 string key text indexer は従来どおり `KeyText` lookup であり、Dictionary typed key blocker とは別扱い。

## 結果
- Review completed with blocking finding.
- Dictionary member が generator で `ParallelGeneratedDictionary<TKey,TElement,TView>` として生成される差分は確認した。
- `Dictionary<int, TValue>` の `root.Scores[100]`、Dictionary ordinal access の `AtIndex(index)`、diff path discriminator access の `ByPathKey(discriminator)`、string key dictionary の `root.Root.Attribute["id"]` は focused E2E 上は通っている。
- ただし `StringKeyComparison.OrdinalIgnoreCase` の supported path で generated dictionary typed key lookup が比較本体と同じ key semantics を持てないため、完了扱いにはしない。

## リスク
- Blocking finding を直す場合、generated wrapper が `CompareConfiguration.StringKeyComparison` もしくは compare runtime と同じ key comparer/canonicalization 情報へアクセスできる設計が必要になる可能性がある。
- DateTime 正規化、nullable key annotation、culture-sensitive `ToString()`、object/composite key は今回の focused E2E では未検証。
- Markdown lint は repo-local script が無いため unsupported。
