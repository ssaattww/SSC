# Generated List Model-Axis Selection

- Date: 2026-04-05
- Related: GitHub Issue #16

## Background

`AsGeneratedView()` の List コンテナは key union 後の一覧を返すため、`root.Groups[0]` は「統合後 index」の意味になる。  
Issue #16 では、統合一覧とは別に「指定モデルで存在する要素だけの List」を選択できる導線が必要だった。

## Investigation Summary

- 対象実装:
  - `src/SSC/GeneratedProjectionRuntime.cs`
  - `src/SSC.Generators/ParallelViewGenerator.cs`
- 現状:
  - `ParallelGeneratedList<TElement, TView>` は `IReadOnlyList<TView>` のみを提供。
  - モデル軸での絞り込み API は未提供。
  - 生成コードは `ParallelGeneratedList` を `nodes + viewFactory` で生成し、親 node の `Count`（モデル数）を渡していなかった。

## Changes

1. `ParallelGeneratedList<TElement, TView>` に `SelectModel(int modelIndex)` を追加。  
2. `ParallelGeneratedModelList<TElement, TView>` を追加し、次を提供:
   - `IReadOnlyList<TView>`
   - `Count`
   - `this[int index]`
   - `IEnumerable<TView>` 列挙
3. `SelectModel` は指定 `modelIndex` で `ValueState.Missing` ではない要素のみを抽出。  
4. `ParallelGeneratedList` のモデル index 検証を追加（範囲外は `CompareExecutionException(ModelIndexOutOfRange)`）。  
5. Generator 生成コードを更新し、`ParallelGeneratedList` 生成時に親 node の `Count` を渡すよう変更。  
6. 設計書 `02-PublicApi.md` に `SelectModel` 記法と意味を追記。  

## Behavior Notes

- `SelectModel` の返却順は key union 順を維持する（元モデルの入力順復元は行わない）。
- `SelectModel` 後の `[index]` は「モデル別 List 内の index」として扱える。

## Verification

- 実行コマンド:
  - `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release`
  - `dotnet test SSC.sln --configuration Release`
- 結果:
  - E2E: 34 passed
  - Unit: 4 passed
  - 失敗なし

