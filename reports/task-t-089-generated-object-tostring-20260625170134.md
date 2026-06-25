# Sub-agent実行レポート

## タスク

- 目的: T-089 generated object view の `ToString()` で元モデルの表示を model slot 別に返す
- タスク種別: implementation

## sub-agentを使う理由

- 理由: 実装は generator への小変更と E2E 追加に限定されるため親 agent で実施。検証 evidence と review は sub-agent に委譲する。

## 対象範囲

- 対象: generated object view class の `ToString()` 生成、元モデル型 `ToString()` override を使う E2E、設計・tracking・breaking changes。

## 対象外

- 対象外: scalar generated value の表示規則変更、dynamic projection の `ToString()`、collection view の `ToString()` 再設計。

## 実行コマンド

- 実行コマンド:
  - `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_ObjectMember_GeneratesNestedViewMembers"`
    - 実装前 failing proof: generated object view の `ToString()` が生成 view class の型名表示になり失敗。
    - 実装後: Passed. Failed: 0, Passed: 1, Skipped: 0.
  - `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_ObjectMember_GeneratesNestedViewMembers|FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_ToStringMember_DoesNotConflictWithObjectViewToString"`
    - 追加実装後: Passed. Failed: 0, Passed: 2, Skipped: 0.
    - 共通化後: Passed. Failed: 0, Passed: 2, Skipped: 0.
  - `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_ObjectMember_GeneratesNestedViewMembers|FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_ToStringMember_DoesNotConflictWithObjectViewToString|FullyQualifiedName~GeneratedProjectionE2ETests.Compare_DynamicProjection_ToString_UsesMaterializedNodeDisplay"`
    - dynamic 経路追加後: Passed. Failed: 0, Passed: 3, Skipped: 0.

## 対象ファイル

- 変更または確認したファイル:
  - `src/SSC/GeneratedProjectionRuntime.cs`
  - `src/SSC.Generators/ParallelViewGenerator.cs`
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
  - `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
  - `doc/design/detail/02-PublicApi.md`
  - `Design/BreakingChanges.md`
  - `tasks/tasks-status.md`
  - `tasks/phases-status.md`

## 指摘事項

- 指摘要約または「指摘なし」: review は別 report で実施する。

## 結果

- 結果: generated object view class に `override ToString() => _node.ToString()` を生成し、`root.Root.Attribute["id"].ToString()` で元モデル `GeneratedXmlAttribute.ToString()` の結果を model slot 別 value/state 形式で確認できるようにした。direct scalar generated member は対応する member `IParallelNode` を保持し、`ToString()` / `GetState()` を node に委譲して `ParallelGeneratedValue` 側の表示 formatter 重複を削除した。`Select(...)` 由来の派生値は一時的な leaf `ParallelNode<TValue>` に変換してから `ToString()` に委譲する。dynamic projection も materialized node がある path では同じ node `ToString()` に委譲する。comparable member `ToString` がある型では生成名衝突を避けるため override を出さない。さらに object 由来名の generated member には `new` を付け、意図的な hiding として生成する。

## リスク

- 未解決のリスクまたは後続対応:
  - collection view の `ToString()` と、materialized node を持たない runtime-derived dynamic path の `ToString()` は対象外。
  - `ToString()` は人間確認用の便利表示であり、機械処理は structured API を使う前提。
