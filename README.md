# SSC

[![PR xUnit](https://github.com/ssaattww/SSC/actions/workflows/pr-xunit-tests.yml/badge.svg)](https://github.com/ssaattww/SSC/actions/workflows/pr-xunit-tests.yml)
[![Publish NuGet](https://github.com/ssaattww/SSC/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/ssaattww/SSC/actions/workflows/publish-nuget.yml)
[![NuGet Version](https://img.shields.io/nuget/v/ssaattww.SSC)](https://www.nuget.org/packages/ssaattww.SSC/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ssaattww.SSC)](https://www.nuget.org/packages/ssaattww.SSC/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](https://github.com/ssaattww/SSC/blob/main/LICENSE)

SSC is a .NET library for structural comparison of multiple models.

It compares object graphs by aligning member values into per-model slots, normalizing container members (Dictionary/List/IEnumerable), and returning both comparison data and diagnostic issues.

## Status

- Target framework: .NET 8
- Current phase: Phase 3 (implementation in progress)
- Test status (latest):
  - E2E: 18 passed
  - Unit: 2 passed

## Core Concepts

- `ParallelCompareApi.Compare<T>(IReadOnlyList<T> models, CompareConfiguration? configuration = null)`
- Result model:
  - `CompareResult<T>.Root` : comparison tree root
  - `CompareResult<T>.Issues` : accumulated diagnostics
  - `CompareResult<T>.HasError` : whether error-level issues exist
- Slot model:
  - `Parallel<T>[modelIndex]` for value access
  - `GetState(modelIndex)` with `ValueState` (`Missing`, `PresentNull`, `PresentValue`)

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
using SSC;

var models = new[]
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

var result = ParallelCompareApi.Compare(models);
var root = (ParallelNode<ProductModel>)result.Root!;
var items = root.GetChildren<ProductItem>(nameof(ProductModel.Items));

// items keys: 1, 2, 3
// key 1: [present, missing]
// key 2: [present, present]
// key 3: [missing, present]

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

## Documentation

- Design guide: `doc/design/`
- Draft notes: `doc/draft/`
- Progress tracking: `tasks/`
- Work reports: `reports/`

## License

MIT (`LICENSE`)
