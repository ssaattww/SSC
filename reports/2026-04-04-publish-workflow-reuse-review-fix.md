# Publish Workflow Reuse: Review Fixes (2026-04-04)

## Background

- Subagent review (`reports/2026-04-04-subagent-code-review-publish-workflow-reuse.md`) reported a high risk:
  - Release created with `GITHUB_TOKEN` may not trigger downstream release workflow reliably.

## Fix Applied

- Refactored `.github/workflows/publish-nuget.yml` to a single `publish` job that runs on:
  - `push` to `main`
  - `release: published`
  - `workflow_dispatch`
- On `push` to `main`:
  - Compute pre-release package version.
  - Build/pack/publish to NuGet directly in the same run.
  - Create GitHub pre-release after publish.
- Added `gh --version` guard before release creation step.

## Effect

- Main-merge path no longer depends on release-event propagation for NuGet publish.
- Pre-release publish occurs deterministically in the same run as `push` to `main`.
- Existing release/manual publish paths remain available.
