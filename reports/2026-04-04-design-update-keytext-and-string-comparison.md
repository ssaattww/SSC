# Design Update: String Key Comparison and KeyText Display Rule (2026-04-04)

## Background

- Subagent code review (`reports/2026-04-04-subagent-code-review.md`) pointed out that key text display under case-insensitive key merge was not explicitly specified.
- Per project rule, detected consideration gaps must be reflected in design documents.

## Updated Design Decisions

1. Dictionary key collision handling
- In the same model, if keys are equivalent under the active comparer (e.g., `OrdinalIgnoreCase`), treat as `DuplicateCompareKeyDetected`.

2. String key comparison policy
- String key comparison follows `CompareConfiguration.StringKeyComparison`.
- Default remains `StringComparison.Ordinal`.
- `OrdinalIgnoreCase` treats case-only differences as the same key.

3. `KeyText` display policy
- For null-related key issues, `KeyText` is `"<null>"`.
- For string key case differences, keep input text representation; do not force canonical case normalization.

## Updated Files

- `doc/design/detail/03-ContainerRules.md`
- `doc/design/detail/05-ResultAndErrors.md`
- `doc/design/detail/07-NonFunctional.md`
- `doc/design/detail/08-ImplementationChecklist.md`

## Notes

- This update is design alignment only; no runtime behavior change was required.
