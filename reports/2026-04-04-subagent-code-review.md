# Subagent Code Review Report (2026-04-04)

## Scope
- Reviewed current uncommitted Phase 3 changes related to key collision handling and null diagnostic alignment.
- Primary focus: bug risk, regression risk, and spec deviation.

## Findings (by severity)

### Critical
- No critical findings.

### High
- No high findings.

### Medium
- No medium findings.

### Low
- Key text display canonicalization for case-insensitive merged keys is still implementation-defined.
  - Target: `src/SSC/ParallelCompareApi.cs`
  - Evidence: duplicate/merged keys under `OrdinalIgnoreCase` preserve encountered key text, so diagnostics may show `"a"` or `"A"` depending on source ordering.
  - Impact: comparison correctness is unaffected, but log/UX consistency can vary.

## Reviewed Evidence
- `src/SSC/ParallelCompareApi.cs`
  - Dictionary duplicate detection added before insert (`ContainsKey` -> `DuplicateCompareKeyDetected`).
  - Null sequence element now records `KeyText = "<null>"`.
- `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - Added duplicate dictionary key tests for non-strict and strict mode under `OrdinalIgnoreCase`.
- `tests/SSC.E2E.Tests/CompareApiE2ETests.cs`
  - Added null sequence element `KeyText` consistency test.

## Test Coverage Checked
- Duplicate dictionary key detection under `OrdinalIgnoreCase`.
- Strict-mode exception behavior for duplicate dictionary key.
- Null sequence element diagnostic key text behavior.

## Residual Risks
- Canonical key text policy for case-insensitive merged keys remains unspecified.

## Recommended Actions
1. If diagnostic display consistency is required, define and implement a key text normalization rule for case-insensitive merged keys.
2. Add one explicit E2E assertion for canonical display once the above rule is finalized.
