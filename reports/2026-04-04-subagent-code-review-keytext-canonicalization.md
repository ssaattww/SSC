# Subagent Code Review Report: KeyText Canonicalization (2026-04-04)

## Scope

- Reviewed changes after commit `2234ecf`.
- Focused on `OrdinalIgnoreCase` key handling, diagnostic `KeyText`, and regression risk.

## Findings (by severity)

### Critical
- No critical findings.

### High
- No high findings.

### Medium
- No medium findings.

### Low
- Canonicalization rule is intentionally ASCII/Unicode code-point order (`StringComparer.Ordinal`) and may not match user-perceived casing expectations in some locales.
  - Target: `src/SSC/ParallelCompareApi.cs`
  - Evidence: `SelectCanonicalKeyText` uses `StringComparer.Ordinal` minimum selection for `OrdinalIgnoreCase` string keys.
  - Impact: behavior is deterministic and consistent, but display might differ from locale-aware expectations.

## Reviewed Evidence

- `src/SSC/ParallelCompareApi.cs`
  - Added canonical key-text tracking for dictionary/sequence normalization.
  - Duplicate issue `KeyText` and child node `KeyText` now resolved from canonical map.
- `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - Strengthened assertions to require exact canonical `KeyText = "A"`.
- `doc/design/detail/03-ContainerRules.md`
- `doc/design/detail/05-ResultAndErrors.md`
- `doc/design/detail/07-NonFunctional.md`
- `doc/design/detail/08-ImplementationChecklist.md`

## Residual Risks

- If future requirements become locale-aware, current canonicalization rule must be reconsidered.

## Recommended Actions

1. Keep current rule as default deterministic policy for diagnostics.
2. If locale-aware display is requested later, add explicit configuration and separate tests to avoid changing default behavior.
