# Publish Workflow Reuse and Main-PreRelease Automation (2026-04-04)

## Summary

Reused logic from `workflows/publish-nuget.yml` and integrated it into `.github/workflows/publish-nuget.yml`.

## What Changed

- Added `push` trigger for `main`.
- Added `create-prerelease` job (runs only on `push` to `main`):
  - Resolves publishable project path.
  - Calculates pre-release version from latest stable tag and commit distance.
  - Creates GitHub pre-release (`gh release create ... --prerelease`) if it does not exist.
- Kept `publish` job and constrained it to:
  - `release` event (published)
  - `workflow_dispatch`

## Publish Flow After Change

1. Merge to `main` -> `create-prerelease` creates a GitHub pre-release tag.
2. Release published event triggers `publish` job.
3. `publish` packs and pushes package to NuGet.

## Notes

- Release version resolution still strips leading `v` from release tags.
- Manual publish via `workflow_dispatch` remains supported.
