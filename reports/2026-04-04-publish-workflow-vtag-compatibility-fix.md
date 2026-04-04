# Publish Workflow v-Tag Compatibility Fix (2026-04-04)

## Background

- Re-review reported that stable tag detection ignored `v`-prefixed tags.

## Fix

- File: `.github/workflows/publish-nuget.yml`
- In pre-release version resolution (`push` to `main`):
  - Stable tag regex now accepts both `1.2.3` and `v1.2.3`.
  - Base version uses normalized value with leading `v` stripped.

## Effect

- Pre-release version lineage now follows latest stable tag regardless of `v` prefix convention.
