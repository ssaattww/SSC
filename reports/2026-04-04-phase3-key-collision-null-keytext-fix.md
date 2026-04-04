# Phase 3: Dictionary Key Collision and Null Diagnostic Alignment (2026-04-04)

## Summary

- Resumed implementation from `reports/2026-04-04-chat-restart-handover.md`.
- Addressed the two pending review items with TDD (RED -> GREEN):
  - Detect case-insensitive duplicate dictionary keys in the same model when `StringComparison.OrdinalIgnoreCase` is configured.
  - Align `CompareIssue.KeyText` for null sequence elements to `"<null>"`.

## Implemented Changes

### 1. Dictionary duplicate detection under `OrdinalIgnoreCase`

- File: `src/SSC/ParallelCompareApi.cs`
- In dictionary normalization, added duplicate detection before insert:
  - `maps[modelIndex].ContainsKey(normalizedKey)` now records `CompareIssueCode.DuplicateCompareKeyDetected`.
  - The duplicate entry is skipped instead of silently overwriting prior data.

### 2. Null sequence element `KeyText` alignment

- File: `src/SSC/ParallelCompareApi.cs`
- For `sequence element is null`, changed issue record from `KeyText = null` to `KeyText = "<null>"`.

## Added/Updated Tests

### `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`

- Added: `Compare_StringDictionaryDuplicateInSameModel_WithOrdinalIgnoreCase_RecordsDuplicateIssue`
  - Validates duplicate issue path/model index/key text.
- Added: `Compare_StringDictionaryDuplicateInSameModel_WithOrdinalIgnoreCase_ThrowsInStrictMode`
  - Validates strict mode throws `CompareExecutionException` with `DuplicateCompareKeyDetected`.

### `tests/SSC.E2E.Tests/CompareApiE2ETests.cs`

- Added: `Compare_WhenSequenceElementIsNull_RecordsIssueWithNullKeyText`
  - Validates null sequence element records `KeyText = "<null>"`.

## TDD Trace

- RED (before implementation): 3 failures in E2E tests.
- GREEN (after implementation): all added tests passed.

## Verification

- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --verbosity minimal`
  - Passed: 17, Failed: 0
- `dotnet test SSC.sln --configuration Release --verbosity minimal`
  - E2E: Passed 17 / Failed 0
  - Unit: Passed 2 / Failed 0

## Remaining Notes

- Key text canonical display for case-insensitive merged keys remains implementation-defined (`"a"` or `"A"` depending on encounter order).
- Current behavior is deterministic enough for comparison and diagnostics, but if display normalization is required, a separate spec decision is needed.
