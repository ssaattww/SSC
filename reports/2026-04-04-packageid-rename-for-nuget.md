# PackageId Update for NuGet Publish (2026-04-04)

## Summary

Changed package identity from implicit `SSC` to explicit `ssaattww.SSC` to avoid NuGet package ownership/name collision.

## Change

- File: `src/SSC/SSC.csproj`
- Added:
  - `<PackageId>ssaattww.SSC</PackageId>`

## Verification

- Command:
  - `dotnet pack src/SSC/SSC.csproj --configuration Release --output /tmp/sscpack --verbosity minimal`
- Result:
  - Output package: `/tmp/sscpack/ssaattww.SSC.1.0.0.nupkg`

## Notes

- Pack emitted a warning about package readme metadata; this does not block package creation.
