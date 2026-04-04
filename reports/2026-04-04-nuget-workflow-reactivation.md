# NuGet Publish Workflow Reactivation (2026-04-04)

## Summary

Re-enabled GitHub Actions workflow for NuGet publishing.

## Changes

- File: `.github/workflows/publish-nuget.yml`
- Updated workflow name:
  - `Publish NuGet Package (Disabled Until v1)` -> `Publish NuGet Package`
- Re-enabled release trigger:
  - Added `on.release.types: [published]`
- Kept manual trigger:
  - `workflow_dispatch` remains available with optional `project_path` and `package_version`
- Removed disable guard:
  - Deleted `enable_release` input
  - Deleted job-level `if: github.event.inputs.enable_release == 'true'`

## Resulting Behavior

- Publishing now runs automatically when a GitHub Release is published.
- Publishing can also be run manually from Actions UI via `workflow_dispatch`.
