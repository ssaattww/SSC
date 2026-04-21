# Sub-agent実行レポート

## タスク

- 目的: T-042 の dynamic 値パス `GetState` 非侵襲化を実装し、追加した failing test を通す。
- タスク種別: 実装 / 検証

## sub-agentを使う理由

- 理由: 実装対象が複数コードファイルにまたがり、検証実行も含むため。

## 対象範囲

- 対象:
  - dynamic 値パス `GetState` の内部 state lookup 経路の変更
  - 必要最小限の関連テスト調整
  - 対象テスト実行と可能なら回帰確認

## 対象外

- 対象外:
  - generated nested value path parity 実装
  - README 更新
  - tasks/phases/feedback 更新
  - commit / PR 作成

## 実行コマンド

- 実行コマンド:
  - `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --filter "FullyQualifiedName~Compare_DynamicProjection_NestedValuePathGetState_DoesNotInvokeGetterDuringCall|FullyQualifiedName~Compare_DynamicProjection_NestedValuePathGetState_DoesNotRethrowGetterException"`
  - `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --filter "FullyQualifiedName~Compare_DynamicProjection_NestedValuePathGetState_DoesNotInvokeGetterDuringCall|FullyQualifiedName~Compare_DynamicProjection_NestedValuePathGetState_DoesNotRethrowGetterException|FullyQualifiedName~Compare_DynamicProjection_NestedValuePathIndexer_StillInvokesGetterOnAccess|FullyQualifiedName~Compare_DynamicProjection_NestedValuePath_AllowsRuntimeDerivedMemberAccess"`
  - `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --filter FullyQualifiedName~ContainerAndSelectManyE2ETests`
  - `dotnet test SSC.sln --configuration Release`
  - `git diff -- src/SSC/Contracts.cs src/SSC/ParallelNode.cs src/SSC/ParallelCompareApi.cs src/SSC/ParallelDynamicAccessExtensions.cs tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`

## 対象ファイル

- 変更または確認したファイル:
  - `src/SSC/Contracts.cs`
  - `src/SSC/ParallelNode.cs`
  - `src/SSC/ParallelCompareApi.cs`
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
  - dynamic access は member node が存在する場合にそれを優先して返すよう更新されており、state lookup 中に reflection で getter を辿らない。
  - compare/build 側では non-container member も `member node` として materialize され、getter 例外は compare 時に `ReflectionMetadataBuildFailed` として吸収される。
  - `IParallelNodeInternal` と `ParallelNode<T>` に member node 取得 API が追加され、既存の list / node access を壊さずに dynamic nested value path を node traversal に寄せている。
  - review 指摘を受け、`GetState` だけ materialized member node を参照し、indexer 読み取りは従来の runtime reflection 解決を維持するよう `DynamicParallelValuePathView` を調整した。
  - 併せて、declared type に無い runtime-derived member でも dynamic nested access が継続利用できる回帰テストを追加した。

## 結果

- 結果:
  - `ParallelCompareApi.BuildNodeGeneric` で scalar を含む non-container member を `SetMemberNode(...)` 経由で保持する実装が追加された。
  - `DynamicParallelNodeView.TryGetMember` は model property 名に対して、reflection ベースの value path より先に materialize 済み `member node` を返すようになった。
  - これにより `root.Items[0].Detail.Label.GetState(modelIndex)` は node-level state を使って判定でき、`GetState` 呼び出し時の getter 再実行が発生しない。
  - 追加した T-042 テスト 4 件は手元再実行で成功した。
  - `ContainerAndSelectManyE2ETests` 全体も手元再実行で成功した（16 passed）。
  - `dotnet test SSC.sln --configuration Release` は成功した（Unit 6 / E2E 39）。

## リスク

- 未解決のリスクまたは後続対応:
  - generated nested value path の non-invasive parity は T-042 の対象外として残る。
  - README は今回未更新のため、review 結果次第で公開説明の整合確認が必要になる。
