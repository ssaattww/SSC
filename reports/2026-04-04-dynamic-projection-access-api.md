# 2026-04-04 Dynamic Projection Access API

## Feature Goal

- 概念表現ではなく、実際に次の書き方で値を参照できるようにする。
  - `root.Groups[0].Items[0].MetricA[0]`
- 左 `[]` を list index、右 `[]` を model index として扱う。

## Design Alignment

- 外部仕様（確定）:
  - 利用者視点のアクセス順は `要素 index -> モデル index`。
  - 欠損と値 `null` は `GetState(modelIndex)` で区別する。
- 設計反映:
  - `doc/design/detail/01-DomainModel.md`
  - `doc/design/detail/02-PublicApi.md`
  - `AsDynamic()` による動的投影アクセスを公開 API として追記。

## Implementation

### Added

- `src/SSC/ParallelDynamicAccessExtensions.cs`
  - `AsDynamic<T>(this ParallelNode<T>)`
  - `DynamicParallelNodeView`
  - `DynamicParallelListView`
  - `DynamicParallelValuePathView`

### Updated

- `src/SSC/Contracts.cs`
  - internal: `IParallelNodeInternal` を追加（動的投影層から型情報/子ノード参照に使用）
- `src/SSC/ParallelNode.cs`
  - `IParallelNodeInternal` 実装を追加
  - `ModelType` / `TryGetChildren(...)` を internal 提供

## Runtime Usage

```csharp
dynamic root = result.Root!.AsDynamic();
var leftMetricAt100 = root.Groups[0].Items[0].MetricA[0]; // 1.0
```

- 左側 `[]`: key union 後の list index
- 右側 `[]`: model index

## Test

- `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - `Compare_DynamicProjection_AllowsListIndexThenModelIndexAccess` を追加
  - 以下を検証
    - `root.Groups[0].Items[0].MetricA[0] == 1.0`
    - `root.Groups[0].Items[1].MetricA[0] == 2.0`
    - `root.Groups[0].Items[0].MetricA[1] == 10.0`
    - `root.Groups[0].Items[1].MetricA[1] == null`
    - `root.Groups[0].Items[1].MetricA.GetState(1) == Missing`

## Validation

- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release`
  - Passed: 19, Failed: 0
- `dotnet test tests/SSC.Unit.Tests/SSC.Unit.Tests.csproj --configuration Release`
  - Passed: 2, Failed: 0
