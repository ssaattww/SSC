# Projection Switch At Result Stage

- Date: 2026-04-04
- Goal: `result.Root!...` より前段で dynamic / generated の投影切替を可能にする

## 変更概要

- runtime:
  - `src/SSC/ParallelDynamicAccessExtensions.cs` の入口を `CompareResult<T>.AsDynamic()` に統一
- source generator:
  - `AsGeneratedView(this CompareResult<T>)` のみ生成するよう整理
  - `ParallelCompareApi.Compare(models).AsGeneratedView()` を可能化
- 互換性:
  - `result.Root!...` 直アクセス導線は廃止し、投影切替は `CompareResult` 入口へ一本化

## テスト

- `Compare_WithEmptyModels_ResultAsDynamicReturnsNull` を追加
- `Compare_GeneratedProjection_WhenRootMissing_ResultExtensionReturnsNull` を追加
- `AsDynamic_WithNonCompareNode_ThrowsArgumentException` / `AsGeneratedView_WithNonCompareNode_ThrowsArgumentException` を `CompareResult` 入口版へ更新
- dynamic / generated の既存導線テストを result 入口版へ更新して回帰確認
