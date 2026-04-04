# 2026-04-04 Children Selector API Improvement

## Summary

- `GetChildren("memberName")` の文字列指定を減らすため、型付き selector で辿れる拡張 API `Children(...)` を追加。
- 深い階層も `Children(...)` を連鎖して参照できるようにした。

## Implemented Changes

- Added: `src/SSC/ParallelNodeExtensions.cs`
  - `Children<TParent, TElement>(Expression<Func<TParent, IEnumerable<TElement>>>)`
  - `Children<TParent, TKey, TElement>(Expression<Func<TParent, IReadOnlyDictionary<TKey, TElement>>>)`
  - selector は direct member access（`x => x.Items`）のみ受け付け。
- Updated tests:
  - `tests/SSC.E2E.Tests/CompareApiE2ETests.cs`
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - 既存の `GetChildren(...)` 利用箇所の主要部分を `Children(...)` へ置換。
- Updated design:
  - `doc/design/detail/02-PublicApi.md`
  - 現行アクセスパターンを `Children(...)` 優先、`GetChildren(...)` は後方互換として明記。

## Usage Example

```csharp
var root = Assert.IsType<ParallelNode<Dataset>>(result.Root);
var groups = root.Children(model => model.Groups);
var items = groups[0].Children(model => model.Items);

var value = items[0][1]?.MetricA;
var state = items[1].GetState(1);
```

## Validation

- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release`
  - Passed: 18, Failed: 0
- `dotnet test tests/SSC.Unit.Tests/SSC.Unit.Tests.csproj --configuration Release`
  - Passed: 2, Failed: 0
  - Note: build copy retry warning (`MSB3026`) が 1 回出たが最終結果は成功。
