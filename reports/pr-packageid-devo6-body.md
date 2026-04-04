## Summary
- rename runtime package ID from `ssaattww.SSC` to `devo6.SSC`
- rename generator package ID from `ssaattww.SSC.Generators` to `devo6.SSC.Generators`
- update README NuGet badges and package-name references to `devo6.*`
- update tracking artifacts (`tasks/*`, `reports/*`) for this rename task

## Verification
- `dotnet test SSC.sln --configuration Release --verbosity minimal`
  - Passed: E2E 33 / Unit 4

## Notes
- repository URL remains `https://github.com/ssaattww/SSC` as-is
- nuget-side deletion/deprecation handling is out-of-band by maintainer
