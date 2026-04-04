# Phase 3: KeyText Canonicalization for OrdinalIgnoreCase (2026-04-04)

## Summary

- Tightened diagnostic determinism for case-insensitive string key handling.
- `KeyText` is now canonicalized when `StringComparison.OrdinalIgnoreCase` is active.

## Decision

- For string keys under `OrdinalIgnoreCase`, `KeyText` uses the lexicographically smallest representation by `StringComparer.Ordinal` among equivalent candidates.
- Example: candidates `"a"` and `"A"` produce canonical `KeyText = "A"`.

## Code Changes

- `src/SSC/ParallelCompareApi.cs`
  - Added canonical key-text tracking map for dictionary normalization and sequence normalization.
  - Added helper methods:
    - `UpdateCanonicalKeyText(...)`
    - `SelectCanonicalKeyText(...)`
  - Duplicate key issue text and child node key text now read from canonicalized key text.

## Test Changes (TDD)

- `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - Strengthened `Compare_StringDictionaryRespectsOrdinalIgnoreCaseConfiguration` to assert exact `KeyText = "A"`.
  - Strengthened `Compare_StringDictionaryDuplicateInSameModel_WithOrdinalIgnoreCase_RecordsDuplicateIssue` to assert exact `KeyText = "A"`.

## Verification

- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --verbosity minimal`
  - Passed: 17, Failed: 0
- `dotnet test SSC.sln --configuration Release --verbosity minimal`
  - E2E: Passed 17 / Failed 0
  - Unit: Passed 2 / Failed 0

## Design Synchronization

Updated design docs to keep implementation and specification aligned:

- `doc/design/detail/03-ContainerRules.md`
- `doc/design/detail/05-ResultAndErrors.md`
- `doc/design/detail/07-NonFunctional.md`
- `doc/design/detail/08-ImplementationChecklist.md`
