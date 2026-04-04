# Source Generator 型付き投影 API 実装

- Date: 2026-04-04
- Goal: 案3（Source Generator）を先行実装し、`AsDynamic()` 互換を維持したまま型付きアクセスを提供

## 1. 実装内容

- runtime 基盤を追加:
  - `GenerateParallelViewAttribute`
  - `ParallelGeneratedRuntime.RequireNode(...)`
  - `ParallelGeneratedList<TElement, TView>`
  - `ParallelGeneratedValue<TModel, TValue>`
  - `ParallelGeneratedMeta`
- generator プロジェクトを追加:
  - `src/SSC.Generators/SSC.Generators.csproj`
  - `src/SSC.Generators/ParallelViewGenerator.cs` (`IIncrementalGenerator`)
- 生成契約:
  - `[GenerateParallelView]` 付き root 型に対し `AsGeneratedView()` を生成
  - `SSC.Generated` namespace に型付き view 群を生成
  - naming は fully-qualified name 由来のサニタイズで一意化

## 2. 互換性方針

- `AsDynamic()` 実装は変更なし（後方互換維持）
- generated node メタ情報は `NodeMeta` 配下に分離し、モデルプロパティ衝突を回避
- non-compare node に対する `AsGeneratedView()` は `ArgumentException`

## 3. テスト

- 追加:
  - `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
    - list/dictionary/scalar/nested value path (`Select`) 検証
    - generated list index 範囲外例外
    - non-compare node ガード
- 既存 dynamic 系 E2E を保持し、回帰なしを確認

## 4. 実行結果

- `dotnet test SSC.sln --configuration Release` 成功
  - E2E: 25 passed
  - Unit: 4 passed
