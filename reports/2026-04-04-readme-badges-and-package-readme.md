# README Badges and Package README Inclusion (2026-04-04)

## Summary

- Added status/distribution badges to root `README.md`.
- Configured NuGet package to include root `README.md` and expose it as package readme metadata.

## Changes

### 1) Root README badges

- File: `README.md`
- Added badges:
  - PR xUnit workflow
  - Publish NuGet workflow
  - NuGet version (`ssaattww.SSC`)
  - NuGet downloads (`ssaattww.SSC`)
  - MIT license

### 2) Package readme embedding

- File: `src/SSC/SSC.csproj`
- Added properties/items:
  - `<PackageReadmeFile>README.md</PackageReadmeFile>`
  - `<None Include="..\..\README.md" Pack="true" PackagePath="\" />`

## Verification

- Command:
  - `dotnet pack src/SSC/SSC.csproj --configuration Release --output /tmp/sscpack --verbosity minimal`
- Result:
  - Success
  - Output package: `/tmp/sscpack/ssaattww.SSC.1.0.0.nupkg`
  - Previous "missing readme" warning no longer appears.
