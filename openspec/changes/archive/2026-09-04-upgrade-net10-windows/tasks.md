## 1. Setup

- [x] 1.1 Create and check out a feature branch for this change (e.g. `feature/upgrade-net10-windows`) off `main`

## 2. Retarget StopWatch.csproj

- [x] 2.1 Convert `source/StopWatch/StopWatch.csproj` from legacy-style to SDK-style (`Microsoft.NET.Sdk` / `Microsoft.NET.Sdk.WindowsDesktop` as appropriate)
- [x] 2.2 Set `<TargetFramework>net10.0-windows</TargetFramework>` and `<UseWindowsForms>true</UseWindowsForms>`
- [x] 2.3 Convert `packages.config` entries to `PackageReference` items; drop `HintPath`-based references
- [x] 2.4 ~~Add explicit `PackageReference` for `System.Configuration.ConfigurationManager`~~ - turned out unnecessary: for `net10.0-windows`/`UseWindowsForms=true`, NuGet reports it (NU1510) as already available via the Windows Desktop shared framework, and the build stays green without it
- [x] 2.5 Set `TreatWarningsAsErrors` to `false` for now (both configurations)
- [x] 2.6 Remove now-redundant legacy MSBuild boilerplate (`Import`s for `Microsoft.Common.props`/`Microsoft.CSharp.targets`, `EnsureNuGetPackageBuildImports` target, manual `System.ValueTuple`/`System.Resources.Extensions` imports)
- [x] 2.7 Confirm all existing `.resx`, icon, and content items are still included under the SDK's default globbing (or re-add explicitly if excluded)
- [x] 2.8 Create `Directory.Packages.props` at the repo root with `ManagePackageVersionsCentrally=true` and move `StopWatch.csproj`'s package versions there as `<PackageVersion>` entries (dropping version attributes from its own `PackageReference` items) - in practice `StopWatch.csproj` ended up with only `RestSharp` as an explicit reference; the rest of the old `packages.config` entries were .NET-Framework-only polyfills no longer needed at all (see 2.3)

## 3. Retarget StopWatchTest.csproj

- [x] 3.1 Convert `source/StopWatchTest/StopWatchTest.csproj` to SDK-style, targeting `net10.0-windows`
- [x] 3.2 Convert `packages.config` entries (NUnit, NUnit3TestAdapter, Moq, Castle.Core) to `PackageReference`
- [x] 3.3 Keep the `ProjectReference` to `StopWatch.csproj`
- [x] 3.4 Update `StopWatch.sln` project references/GUIDs if the SDK-style conversion changes them - not needed: `dotnet build StopWatch.sln` resolves both projects correctly unchanged
- [x] 3.5 Move `StopWatchTest.csproj`'s package versions into the same `Directory.Packages.props` - in practice none of the version-drifted polyfill packages (System.Buffers, System.Memory, etc.) survived at all (see 2.8); `StopWatchTest.csproj` ended up with `Castle.Core`, `Microsoft.NET.Test.Sdk`, `Moq`, `NUnit`, `NUnit3TestAdapter`, and `RestSharp` as its centrally versioned references

## 4. Get it compiling and running on net10

- [x] 4.1 Build `StopWatch.sln` targeting net10.0-windows and fix compile errors that block the build (not warnings)
- [x] 4.2 Run the app manually and confirm the main window opens, dark mode/theme still applies, and the single-instance mutex/`WM_SHOWME` focus-stealing behavior still works (verified process launch, main window title, no crash log, and single-instance behavior programmatically; visual dark-mode rendering not screenshot-verified - no native desktop screenshot tool available)
- [x] 4.3 Run the existing NUnit test suite (`StopWatchTest`) against net10 and fix any breakage caused purely by the retarget (50 passed, 5 pre-existing skips, 0 failed)

## 5. Replace BinaryFormatter with JSON in Settings.cs

- [x] 5.1 Add a JSON read/write path for `PersistedIssues` using `System.Text.Json` (new format going forward)
- [x] 5.2 Add a legacy-format reader that decodes the existing Base64 `BinaryFormatter` blob via `System.Formats.Nrbf`'s `NrbfDecoder`, walking the `ClassRecord` graph field-by-field into `PersistedIssue` instances (verified empirically: `DateTime`/`TimeSpan` are NRBF primitives read directly, but `DateTimeOffset?` decodes as a nested `System.DateTimeOffset` record with `DateTime`/`OffsetMinutes` members that must be reconstructed manually)
- [x] 5.3 Wire `ReadIssues` to try JSON first, fall back to the legacy NRBF reader, and on failure of both log via the existing `WriteLog`/`LoggingEnabled` mechanism and return an empty list
- [x] 5.4 Ensure a successful legacy-format read immediately triggers a re-save in the new JSON format (so the fallback path only runs once per installation)
- [x] 5.5 Remove the `System.Runtime.Serialization.Formatters.Binary` usage and `using` from `Settings.cs` entirely
- [x] 5.6 Add/update unit tests in `StopWatchTest` covering: JSON round-trip, legacy-blob decoding (using a real Base64 blob captured from the old format), and the empty/corrupt-blob fallback (`SettingsTest.cs`, 4 new tests, all passing)

## 6. Re-enable warning-as-error and triage

- [x] 6.1 Set `TreatWarningsAsErrors` back to `true` in `StopWatch.csproj`
- [x] 6.2 Rebuild and collect the resulting warning list (CA1416 disappeared entirely once the assembly was correctly marked `[SupportedOSPlatform("windows")]`; remaining: NU1507, SYSLIB0014, 2x CS8073)
- [x] 6.3 Fix or explicitly suppress (`<NoWarn>` with a comment explaining why) each remaining warning: NU1507 suppressed centrally in `Directory.Packages.props` (machine-local NuGet source count, not a code issue); SYSLIB0014 fixed by deleting the dead `ServicePointManager.SecurityProtocol` line in `Program.cs` (RestSharp's `HttpClient` doesn't consult it, and it excluded TLS 1.3); both CS8073 fixed by removing genuinely-dead `!= null` checks against non-nullable `DateTimeOffset`/`TimeSpan` values in `WorklogForm.cs`. Build is now 0 warnings, 0 errors; full test suite still green (54 passed, 5 skipped)

## 7. Remove the installer, switch to framework-dependent publish

- [x] 7.1 Delete `source/StopWatchSetup/StopWatchSetup.vdproj` and `StopWatchSetup.sln`
- [x] 7.2 Document (e.g. in `README.md` or a release script) the publish command: `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`
- [x] 7.3 Produce a test publish output and confirm the single `.exe` runs on a machine with the .NET 10 Desktop Runtime installed, and that Windows shows its standard runtime-install prompt on a machine without it (published via `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`; produces a single ~620KB `StopWatch.exe`, verified it launches and opens its main window on this machine, which has the runtime installed. The "no runtime installed" prompt is standard out-of-the-box .NET behavior, not something this change implements - not independently verified here since that would require a clean machine/VM without the .NET 10 runtime)
- [x] 7.4 Update any release/build documentation or CI references that pointed at the old `.vdproj`/MSI output (removed `.github/workflows/dotnet-desktop.yml`, which built the now-deleted `StopWatchSetup.sln` and targeted a nonexistent `master` branch; bumped `dotnet-version` from 3.1.x/6.0.x to 10.0.x and dropped the legacy `nuget restore` step in `build.yml` and `codeql.yml`, both now-unnecessary/broken for an SDK-style, CPM, net10-only solution)
- [x] 7.5 Add a link to the .NET 10 Desktop Runtime download in README (or release notes) as a stated prerequisite
