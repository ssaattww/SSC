# NuGet Workflow: Release Version Alignment (2026-04-04)

## Background

- Subagent review on workflow reactivation reported a medium issue:
  - Release-triggered publish used CI-style version (`<VersionPrefix>-ci.<run_number>`) instead of release tag version.

## Fix

- File: `.github/workflows/publish-nuget.yml`
- `Resolve package version` step updated:
  - Priority 1: `workflow_dispatch` input `package_version`
  - Priority 2: when `event_name == release`, use `github.event.release.tag_name` (strip leading `v`)
  - Priority 3: fallback to existing CI suffix generation (`<VersionPrefix>-ci.<run_number>`)

## Result

- Release publish now aligns NuGet package version with GitHub Release tag.
- Manual dispatch behavior remains unchanged.
