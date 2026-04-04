# README Badge Link Fix for Package Readme (2026-04-04)

## Background

- Subagent review for README badge and package-readme configuration reported a low issue:
  - License badge link used relative path (`LICENSE`), which can break on NuGet package readme rendering.

## Fix

- File: `README.md`
- Updated license badge target:
  - from: `(LICENSE)`
  - to: `(https://github.com/ssaattww/SSC/blob/main/LICENSE)`

## Effect

- License badge link now works consistently in both GitHub and NuGet package readme contexts.
