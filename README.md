# SSC

[![PR xUnit](https://github.com/ssaattww/SSC/actions/workflows/pr-xunit-tests.yml/badge.svg)](https://github.com/ssaattww/SSC/actions/workflows/pr-xunit-tests.yml)
[![Publish NuGet](https://github.com/ssaattww/SSC/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/ssaattww/SSC/actions/workflows/publish-nuget.yml)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](https://github.com/ssaattww/SSC/blob/main/LICENSE)

**Runtime Package**
[![NuGet Version](https://img.shields.io/nuget/v/devo6.SSC)](https://www.nuget.org/packages/devo6.SSC/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/devo6.SSC)](https://www.nuget.org/packages/devo6.SSC/)

---

**Source Generator Package**
[![NuGet Version](https://img.shields.io/nuget/v/devo6.SSC.Generators)](https://www.nuget.org/packages/devo6.SSC.Generators/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/devo6.SSC.Generators)](https://www.nuget.org/packages/devo6.SSC.Generators/)

SSC is a .NET library for structural comparison of multiple models.

It compares object graphs by aligning member values into per-model slots, normalizing container members (Dictionary/List/IEnumerable), and returning both comparison data and diagnostic issues.

## Status

- Target framework: .NET 8

## NuGet Packages

- Runtime package:
  - `devo6.SSC`
- Source Generator package:
  - `devo6.SSC.Generators`

Install both packages when you want typed generated projection API (`AsGeneratedView()`).

## Core Concepts

- `ParallelCompareApi.Compare<T>(IReadOnlyList<T> models, CompareConfiguration? configuration = null)`
- Result model:
  - `CompareResult<T>.Root` : comparison tree root
  - `CompareResult<T>.Issues` : accumulated diagnostics
  - `CompareResult<T>.HasError` : whether error-level issues exist
- Slot model:
  - `Parallel<T>[modelIndex]` for value access
  - `GetState(modelIndex)` for slot state access
  - `ValueState.Missing` : this slot is missing, or comparison target does not exist
  - `ValueState.Matched` : there are comparison targets and all compared slots are equal
  - `ValueState.Mismatched` : this slot exists and at least one compared slot is different (including target-side missing)

## Container Behavior

- Dictionary:
  - Union keys across models
  - Build child nodes by key
  - Duplicate-equivalent keys in same model are recorded as `DuplicateCompareKeyDetected`
- List / Array / IEnumerable:
  - Element type requires `[CompareKey]`
  - Missing compare key is recorded as issue (strict mode throws)
  - Duplicate compare key is recorded as issue (strict mode throws)

## Strict Mode

- `StrictMode = false` (default): collect issues and continue where possible
- `StrictMode = true`: throw immediately on error
  - `CompareInputException` for input errors
  - `CompareExecutionException` for execution errors

## Trace Logging

- `CompareConfiguration.TraceLog` に callback を渡すと、compare 実行中の内部 trace を受け取れます
- 特に container 判定では `path`, `declaredType`, `container`, `runtimeType`, `materializedCount`, `compareKey` などが出力されます

```csharp
List<string> traceLines = [];

CompareConfiguration configuration = new()
{
    TraceLog = traceLines.Add,
};

CompareResult<ProductModel> result = ParallelCompareApi.Compare(models, configuration);
```

trace 例:

```text
phase=metadata path=ProductModel.Items declaredType=System.Collections.Generic.List`1[ProductItem] container=List
phase=container path=ProductModel.Items container=List elementType=ProductItem compareKey=Id
phase=container path=ProductModel.Items modelIndex=0 container=List runtimeType=System.Collections.Generic.List`1[ProductItem] materializedCount=2
```

## String Key Policy

- Default string key comparison is `StringComparison.Ordinal`
- `StringKeyComparison = StringComparison.OrdinalIgnoreCase` is supported
- Under `OrdinalIgnoreCase`, diagnostic `KeyText` is canonicalized to the `StringComparer.Ordinal` minimum representation among equivalent candidates

## Quick Start

```bash
dotnet build SSC.sln -c Release
dotnet test SSC.sln -c Release --verbosity minimal
```

## Minimal Example

```csharp
using System.Collections.Generic;
using SSC;

ProductModel[] models =
{
    new ProductModel
    {
        Items =
        [
            new ProductItem { Id = 1, Price = 100 },
            new ProductItem { Id = 2, Price = 200 },
        ],
    },
    new ProductModel
    {
        Items =
        [
            new ProductItem { Id = 2, Price = 250 },
            new ProductItem { Id = 3, Price = 300 },
        ],
    },
};

CompareResult<ProductModel> result = ParallelCompareApi.Compare(models);
dynamic root = result.AsDynamic()!;

// item keys are normalized as union: 1, 2, 3
decimal? leftPriceAtKey1 = root.Items[0].Price[0];   // 100
decimal? rightPriceAtKey1 = root.Items[0].Price[1];  // null (missing)
decimal? leftPriceAtKey2 = root.Items[1].Price[0];   // 200
decimal? rightPriceAtKey2 = root.Items[1].Price[1];  // 250
ValueState stateAtKey3Left = (ValueState)root.Items[2].GetState(0); // Missing

public sealed class ProductModel
{
    public List<ProductItem> Items { get; init; } = [];
}

public sealed class ProductItem
{
    [CompareKey]
    public int Id { get; init; }

    public decimal Price { get; init; }
}
```

## Path and Diff APIs

SSC provides three related path APIs. Choose the API according to whether you need to address the comparison tree, enumerate differences, or assign consumer-defined labels.

| Purpose | API | Path meaning |
| --- | --- | --- |
| Read a comparison node, value, or state | `GetNodeByPath()`, `GetValueByPath()`, `GetStateByPath()` | Standard XPath-like node address |
| Enumerate structured differences | `GetDiffEntries()` | Standard diff-entry path generated by SSC |
| Relabel and search differences for reports or filters | `GetDiffEntryPathProjections()` | Consumer-defined projected path; not a node address |

### Standard XPath-like Path Access

Use standard XPath-like paths when you want to read a node, value, or state without dynamic or generated projections. Paths are root-relative by default, and a root type prefix is optional.

```csharp
IParallelNode? priceNode = result.GetNodeByPath("Items[2].Price");

object? leftPrice = result.GetValueByPath("ProductModel.Items[2].Price", 0); // 200
object? rightPrice = result.GetValueByPath("Items[2].Price", 1);             // 250
ValueState rightState = result.GetStateByPath("Items[1].Price", 1);          // Missing
```

These APIs address the comparison tree. They do not accept a projected path created by `IParallelDiffPathProjector`.

### Structured Diff Entries

`GetDiffEntries()` returns only detected differences as `ParallelDiffEntry` objects. Each entry contains its standard `Path`, optional `ParentPath`, `Kind`, associated node, and one `ParallelDiffValue` per model slot.

```csharp
IReadOnlyList<ParallelDiffEntry> entries = result.GetDiffEntries();
ParallelDiffEntry priceDiff = entries.Single(entry => entry.Path == "Items[2].Price");

foreach (ParallelDiffValue value in priceDiff.Values)
{
    Console.WriteLine($"slot={value.ModelIndex}, value={value.Value}, state={value.State}");
}

Console.WriteLine(priceDiff);
// Items[2].Price: [0]=200(Mismatched), [1]=250(Mismatched)
```

For `ParallelDiffEntryKind.Node`, `GetNodeByPath(entry.Path)` normally resolves the same node. The legacy base-compatible empty compare-key selector (`[]`) preserves standard path text but is not parseable for lookup, so node and parent-path lookup are not guaranteed for that case.

For `ParallelDiffEntryKind.ContainerPresence`, `Node` is `null`. The path identifies the container member for display, while each slot state distinguishes an existing container from a missing container.

```csharp
CompareResult<OptionalProductModel> emptyContainerResult = ParallelCompareApi.Compare(
[
    new OptionalProductModel { Items = [] },
    new OptionalProductModel { Items = null },
]);

ParallelDiffEntry containerDiff = emptyContainerResult
    .GetDiffEntries()
    .Single(entry => entry.Kind == ParallelDiffEntryKind.ContainerPresence);

IParallelNode? unresolved = emptyContainerResult.GetNodeByPath(containerDiff.Path); // null

public sealed class OptionalProductModel
{
    public List<ProductItem>? Items { get; init; }
}
```

### Consumer-defined Projected Diff Paths

Use `GetDiffEntryPathProjections()` when reports, exports, or filters need a path vocabulary different from SSC's standard path. An `IParallelDiffPathProjector` receives each standard segment with traversal context and chooses to keep, replace, or omit it.

```csharp
public sealed class ReportPathProjector : IParallelDiffPathProjector
{
    public ParallelDiffPathSegmentProjection Project(ParallelDiffPathProjectionContext context)
    {
        return context.Current.StandardSegment.MemberName == "Items"
            ? ParallelDiffPathSegmentProjection.Replace(
                context.Current.StandardSegment.WithMemberName("Product"))
            : ParallelDiffPathSegmentProjection.KeepStandard();
    }
}

ReportPathProjector projector = new();
IReadOnlyList<ParallelDiffEntryPathProjection> projections =
    result.GetDiffEntryPathProjections(projector);
```

A projection retains the original `Entry` and adds `ProjectedPath` and `ProjectedParentPath`. It also exposes model-slot values and states directly.

```csharp
ParallelDiffEntryPathProjection projection = projections[0];

int modelCount = projection.Count;
object? before = projection[0];
object? after = projection[1];
ValueState afterState = projection.GetState(1);

string standardPath = projection.Entry.Path;
string reportPath = projection.ProjectedPath;
```

`ProjectedPath` is a display, classification, and filtering label. It is not accepted by `GetNodeByPath()` and is not converted back to a standard path.

#### Search projected paths

Use the string overload for ordinal exact matching and the `ParallelDiffPathPattern` overload for wildcard matching.

```csharp
IReadOnlyList<ParallelDiffEntryPathProjection> exact =
    result.GetDiffEntryPathProjections(projector, "Product[2].Price");

ParallelDiffPathPattern pattern =
    ParallelDiffPathPattern.Parse("Product[*].Price");

IReadOnlyList<ParallelDiffEntryPathProjection> matched =
    result.GetDiffEntryPathProjections(projector, pattern);
```

Different standard entries may project to the same path. Search results therefore preserve duplicates and the original diff-entry order. Use `projection.Entry.Path` when the standard location is also required.

If a projector omits every segment of an entry, projection throws `InvalidOperationException`. Exceptions raised by the projector are propagated to the caller. A negative model index or an index greater than or equal to `Count` throws `ArgumentOutOfRangeException`.

## Source Generator Example

```csharp
using System.Collections.Generic;
using System.Linq;
using SSC;
using SSC.Generated;

[GenerateParallelView]
public sealed class Dataset
{
    public List<Group> Groups { get; init; } = [];
}

public sealed class Group
{
    [CompareKey]
    public int GroupId { get; init; }

    public List<Item> Items { get; init; } = [];
}

public sealed class Item
{
    [CompareKey]
    public int ItemId { get; init; }

    public double MetricA { get; init; }
}

Dataset[] models =
{
    new Dataset
    {
        Groups =
        [
            new Group
            {
                GroupId = 1,
                Items =
                [
                    new Item { ItemId = 100, MetricA = 1.0 },
                    new Item { ItemId = 200, MetricA = 2.0 },
                ],
            },
            new Group
            {
                GroupId = 2,
                Items =
                [
                    new Item { ItemId = 210, MetricA = 21.0 },
                    new Item { ItemId = 220, MetricA = 22.0 },
                ],
            },
        ],
    },
    new Dataset
    {
        Groups =
        [
            new Group
            {
                GroupId = 1,
                Items =
                [
                    new Item { ItemId = 100, MetricA = 10.0 },
                    new Item { ItemId = 300, MetricA = 30.0 },
                ],
            },
            new Group
            {
                GroupId = 2,
                Items =
                [
                    new Item { ItemId = 210, MetricA = 21.0 },
                    new Item { ItemId = 230, MetricA = 23.0 },
                ],
            },
        ],
    },
    new Dataset
    {
        Groups =
        [
            new Group
            {
                GroupId = 1,
                Items =
                [
                    new Item { ItemId = 100, MetricA = 100.0 },
                    new Item { ItemId = 400, MetricA = 40.0 },
                ],
            },
            new Group
            {
                GroupId = 2,
                Items =
                [
                    new Item { ItemId = 210, MetricA = 21.0 },
                    new Item { ItemId = 240, MetricA = 24.0 },
                ],
            },
        ],
    },
};

CompareResult<Dataset> result = ParallelCompareApi.Compare(models);
double? leftMetricAt100 = result.AsGeneratedView()!.Groups[0].Items[0].MetricA[0];
ValueState rightStateAt200 = result.AsGeneratedView()!.Groups[0].Items[1].MetricA.GetState(1);

int[] groupIds = result.AsGeneratedView()!.Groups
    .Select(group => group.GroupId[0] ?? group.GroupId[1] ?? group.GroupId[2] ?? -1)
    .ToArray();

int[] itemIds = result.AsGeneratedView()!.Groups
    .SelectMany(group => group.Items)
    .Select(item => item.ItemId[0] ?? item.ItemId[1] ?? item.ItemId[2] ?? -1)
    .ToArray();

int[] mismatchedItemIds = result.AsGeneratedView()!.Groups
    .SelectMany(group => group.Items)
    .Where(item => item.MetricA.GetState(0) == ValueState.Mismatched
        || item.MetricA.GetState(1) == ValueState.Mismatched
        || item.MetricA.GetState(2) == ValueState.Mismatched)
    .Select(item => item.ItemId[0] ?? item.ItemId[1] ?? item.ItemId[2] ?? -1)
    .ToArray();
```

## Documentation

- Design guide: `doc/design/`
- Draft notes: `doc/draft/`
- Progress tracking: `tasks/`
- Work reports: `reports/`

## License

MIT (`LICENSE`)
