# GetState Mismatch Enum Extension

- Date: 2026-04-05
- Related: GitHub Issue #18

## Background

Issue #18 は、`GetState(modelIndex)` で一致/不一致を直接判定したい、という要望。  
最終方針は「`PresentNull`/`PresentValue` の区別を廃止し、3状態へ単純化」に確定した。

## Scope

- `ValueState` enum 再定義（3状態）
- `GetState` 判定ロジック更新（node / generated value path / dynamic value path）
- 既存テストの期待値更新と回帰追加
- README / 詳細設計の状態説明更新
- 動作仕様設計書の追記

## Implementation

1. `ValueState` を次の3状態に整理
   - `Missing`
   - `Matched`
   - `Mismatched`
2. `ValueStateExtensions` を追加
   - `IsMissing`, `IsMatched`, `IsMismatched`, `ToComparisonState` を提供
3. 内部表現 `NodePresenceState`（`Missing/PresentNull/PresentValue`）を追加
   - 公開状態と内部存在状態を分離
3. `ParallelNode<T>.GetState` を更新
   - 当該 slot が欠損なら `Missing`
   - 当該 slot が存在し、比較先が一致なら `Matched`
   - 当該 slot が存在し、比較先が不一致（比較先欠損含む）なら `Mismatched`
4. `ParallelGeneratedValue<TModel, TValue>.GetState` / `DynamicParallelValuePathView.GetState` を同一ルールへ統一
5. generated list の model 抽出ロジックを `GetState` 依存から内部存在状態依存へ変更
   - `SelectModel(modelIndex)` の意味を維持

## Behavior

- `Missing`:
  - 当該 slot が欠損
  - または比較対象が存在しない（モデルが1件のみ等）
- `Matched`:
  - 当該 slot が存在し、比較対象と一致
  - `null` 同士は一致扱い
- `Mismatched`:
  - 当該 slot が存在し、比較対象と不一致
  - 比較先欠損も不一致として扱う

## Design Document

- 仕様を `doc/design/detail/09-ValueStateBehavior.md` として新規追加
- `GetState` 判定表とモデル数別の例を記載

## Verification

- `dotnet test SSC.sln --configuration Release`
- Result:
  - E2E: 35 passed
  - Unit: 6 passed
  - failed: 0
