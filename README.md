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
