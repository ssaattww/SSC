# 2026-04-04 Dynamic Projection Review Fixes

## Summary

Subagent code review follow-up (T-041) を実装し、dynamic projection の公開契約と状態解決の不整合を修正した。

## Implemented Changes

1. `AsDynamic` 入口契約の整合
- `AsDynamic<T>(this Parallel<T> node)` へ変更し、`CompareResult<T>.Root` から直接利用可能化。
- `IParallelNode` へ変換できない入力は `ArgumentException` で失敗させる。

2. dynamic 値パス `GetState` の意味整合
- 値パス解決を `ResolveValue(...)` に集約。
- `root....Member.GetState(modelIndex)` が最終参照先の値状態（`Missing` / `PresentNull` / `PresentValue`）を返すよう修正。

3. 予約名衝突 (`Count` / `KeyText`) の緩和
- dynamic member 解決順を変更し、子ノード/モデルプロパティを優先。
- node メタは `NodeCount` / `NodeKeyText` を追加して明示参照可能化。
- 既存の `Count` / `KeyText` は後方互換としてフォールバック。

4. dynamic list 範囲外 index の契約化
- `DynamicParallelListView` に境界チェックを追加。
- 範囲外時は `CompareExecutionException(ModelIndexOutOfRange)` を送出。

5. `ParallelNode.CreateLeaf(values, states)` 引数検証
- `values` / `states` null 検証を追加。
- 要素数不一致時に `ArgumentException` を送出。

6. 設計書更新
- `01-DomainModel.md`: 内部表現 `Children` 型を実装に合わせて修正。
- `01-DomainModel.md`, `02-PublicApi.md`: `AsDynamic` 契約、`NodeCount` / `NodeKeyText`、dynamic list 範囲外契約を反映。

## Test Coverage Added

- `Compare_DynamicProjection_AllowsListIndexThenModelIndexAccess`
  - `Parallel<T>` 入口から `AsDynamic()` 利用できることを確認。
- `Compare_DynamicProjection_ValuePathGetState_ReflectsMemberState`
  - 値パス `GetState` が `PresentNull` / `Missing` を正しく返すことを確認。
- `Compare_DynamicProjection_PrefersModelMember_WhenNameCollidesWithNodeMeta`
  - `Count` / `KeyText` 衝突時にモデルメンバー優先で解決し、node メタは `Node*` で取得できることを確認。
- `Compare_DynamicProjection_ListIndexOutOfRange_ThrowsExecutionException`
  - dynamic list 範囲外アクセスの契約例外を確認。
- `CreateLeaf_LengthMismatch_ThrowsArgumentException`
  - `CreateLeaf` の長さ不一致入力検証を確認。

## Verification

- `dotnet test SSC.sln --configuration Release`
  - Passed: Unit 3, E2E 22, Failed: 0

## Files

- `src/SSC/ParallelDynamicAccessExtensions.cs`
- `src/SSC/ParallelNode.cs`
- `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
- `tests/SSC.Unit.Tests/ParallelNodeUnitTests.cs`
- `doc/design/detail/01-DomainModel.md`
- `doc/design/detail/02-PublicApi.md`
