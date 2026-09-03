## Context

See [proposal.md](proposal.md) for motivation. Relevant current-state facts that shape this design:

- `Settings.cs` persists `List<PersistedIssue>` (a `[Serializable]` POCO with only `string`/`bool`/`DateTimeOffset?`/`DateTime`/`TimeSpan`/enum members - no cycles, no polymorphism) as a Base64-encoded `BinaryFormatter` blob inside the `PersistedIssues` string setting in `user.config`.
- `System.Formats.Nrbf` is already referenced in `packages.config` but unused anywhere in the codebase - it appears a prior attempt to move off `BinaryFormatter` was started and abandoned.
- `Settings.Username`/`ApiToken` use Windows DPAPI (`Helpers/DPApi.cs`) for encryption - unrelated to `BinaryFormatter` and unaffected by this change.
- `CheckForUpdate` (`UI/MainForm.cs`) only polls the GitHub releases API and notifies the user; it does not replace the running executable. Switching the distribution artifact to a self-contained single-file exe does not require changes to this mechanism.
- The app is Windows-only today (P/Invoke to `user32`/`dwmapi`/`uxtheme`/`kernel32`/`crypt32`) and stays Windows-only after migration - target is `net10.0-windows`, not `net10.0`.

## Goals / Non-Goals

**Goals:**
- Compile and run on .NET 10, Windows only, with SDK-style projects.
- Replace `BinaryFormatter` with a supported serialization format without losing existing users' in-flight timer state on upgrade, and without executing arbitrary types from untrusted/legacy data.
- Ship a single framework-dependent `win-x64` executable, relying on a separately-installed .NET 10 Desktop Runtime rather than embedding it.

**Non-Goals:**
- No cross-platform support (macOS/Linux) - explicitly out of scope per the proposal.
- No new installer/updater mechanism (MSIX, WiX, in-place auto-update) - the app remains "download the exe, run it."
- No IL trimming or ReadyToRun optimization in this change - can be revisited later as a pure build-output tweak, independent of everything else here.
- No change to how `Username`/`ApiToken` are encrypted (DPAPI stays as-is).

## Decisions

### 1. SDK-style projects, `net10.0-windows`, `UseWindowsForms=true`
Legacy-style `.csproj` (MSBuild `ToolsVersion="4.0"`, hand-written `<Reference>`/`<HintPath>` entries) has no reason to survive the jump to .NET 10 - the SDK-style format is required for modern multi-targeting/tooling anyway and is far shorter. `packages.config` entries convert to `PackageReference`. Most of the polyfill packages in the old `packages.config` (`System.Buffers`, `System.Memory`, `System.Numerics.Vectors`, `System.Runtime.CompilerServices.Unsafe`, `System.Threading.Tasks.Extensions`, `System.ValueTuple`, `Microsoft.Bcl.*`, etc.) existed only to backfill APIs missing from .NET Framework 4.8 and are dropped entirely - net10 already has them in the BCL. Verified during implementation: `System.Configuration.ConfigurationManager` and `System.Formats.Nrbf` also turned out to need no explicit `PackageReference` for a `net10.0-windows`/`UseWindowsForms=true` project - NuGet's package-pruning reports them (NU1510) as already available via the Windows Desktop shared framework, and removing the explicit references still builds clean. The only package either project still references directly is `RestSharp`.

Alternative considered: keep the legacy csproj format and just bump `TargetFrameworkVersion`. Rejected - legacy-style projects can't target `net10.0` at all; the SDK-style format is mandatory, not a style preference.

Central Package Management (`Directory.Packages.props` at the repo root, `ManagePackageVersionsCentrally=true`) is adopted as part of this same conversion, since both projects are already having their package references rewritten. It also surfaces version drift that already exists between the two `packages.config` files for shared transitive dependencies (e.g. `System.Buffers` 4.6.1 vs 4.6.0, `System.Memory` 4.6.2 vs 4.5.5, `System.Numerics.Vectors` 4.6.1 vs 4.6.0, `System.Resources.Extensions` 9.0.3 vs 8.0.0, `System.Runtime.CompilerServices.Unsafe` 6.1.2 vs 6.1.0, `System.Threading.Tasks.Extensions` 4.6.3 vs 4.5.4, `System.ValueTuple` 4.6.1 vs 4.5.0) - each pair is aligned to the higher of the two versions.

### 2. `TreatWarningsAsErrors`: off during retarget, on after
Retargeting alone introduces warnings that don't exist today - most notably `BinaryFormatter` is `[Obsolete]` as a hard-coded diagnostic (SYSLIB0011) under modern SDKs, and `[DllImport]` usages likely trigger a `LibraryImport` source-generator suggestion (SYSLIB1054). Flipping straight to `net10.0-windows` with `TreatWarningsAsErrors=true` still on would block the build on an unpredictable mix of expected and incidental new warnings at once, making it hard to tell "real problem" from "compiler noise."

Decision: set `TreatWarningsAsErrors=false` for the duration of the retargeting work itself. The `BinaryFormatter` obsolescence is still fixed for real (per Decision 3) - not suppressed - because it's one of the three things this change exists to address. Once the app compiles and runs cleanly on net10, re-enable `TreatWarningsAsErrors=true` and triage whatever warnings remain (fix, or explicit `<NoWarn>` with justification) as a distinct, later step.

### 3. `BinaryFormatter` → JSON, with a one-time legacy-format read via `System.Formats.Nrbf`
Going forward, `PersistedIssues` is serialized with `System.Text.Json` (already a project dependency); the JSON path is detected by the stored string starting with `[`. If that check fails, the read path falls back to decoding the existing Base64 blob with `NrbfDecoder` - which parses the binary record graph without ever instantiating/executing the serialized types (unlike `BinaryFormatter`, this is safe to run against untrusted/legacy data). Because `PersistedIssue` is a flat POCO, decoding means walking the resulting `ClassRecord` graph field-by-field and mapping into a fresh `PersistedIssue`. Verified empirically against a real BinaryFormatter-produced blob: `DateTime`/`TimeSpan`/`bool`/`string`/enum-backing `Int32` come back as plain NRBF primitives via `GetRawValue`; only `DateTimeOffset?` needs extra work, since `DateTimeOffset` itself isn't an NRBF primitive - it decodes as a nested `System.DateTimeOffset` record whose `DateTime` member is (counterintuitively) the UTC instant rather than the local/offset-adjusted value, alongside an `OffsetMinutes` member; reconstructing it is `new DateTimeOffset(DateTime.SpecifyKind(utcMember, DateTimeKind.Utc)).ToOffset(TimeSpan.FromMinutes(offsetMinutes))`. Enum members (`EstimateUpdateMethod`) similarly decode as a nested record with a single `value__` Int32 member. After a successful read via either path, the list is immediately re-saved as JSON, so the legacy fallback path only ever runs once per installation.

If the legacy blob can't be decoded (corrupted, unexpected shape), the failure is logged (existing `WriteLog`/`LoggingEnabled` mechanism) and the app starts with an empty persisted-issue list instead of failing to start. This is an acceptable loss because `PersistedIssues` only holds active/paused timer state, not historical worklog data (that lives in Jira itself, posted via the API).

Alternatives considered:
- *Keep `BinaryFormatter` alive via the `System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization` AppContext switch*: rejected - it's an escape hatch that Microsoft has been actively narrowing (already removed outright in ASP.NET Core, disabled by default elsewhere) and isn't a durable fix; the codebase already shows a prior, unfinished attempt to move away from it.
- *Version-tag the setting and support both formats indefinitely*: rejected as unnecessary complexity - once every existing installation has upgraded once, the legacy path is dead code with no way to know when it's safe to delete. A one-time, self-migrating read is simpler and self-obsoletes naturally (it just stops being exercised).

### 4. Distribution: single framework-dependent `win-x64` exe, no installer, runtime installed separately
`dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`. Framework-dependent means the .NET 10 Desktop Runtime must already be present on the target machine - if it's missing, the OS shows Microsoft's own "you need to install .NET Desktop Runtime X" prompt (standard behavior for framework-dependent apps, not something this app needs to implement itself). This trades a runtime prerequisite for a much smaller download than a self-contained build. IL trimming isn't relevant here regardless (trimming only applies to self-contained deployments). `StopWatchSetup.vdproj`/`StopWatchSetup.sln` are deleted rather than replaced - confirmed with the user that "download the exe and run it" is the desired experience, with no Start Menu entry or uninstaller.

Alternative considered: self-contained single-file (embeds the runtime, no separate install needed). Rejected per explicit user preference for a smaller, framework-dependent download over a larger self-contained one.

## Risks / Trade-offs

- [Legacy `PersistedIssues` blob fails to decode for some users] → Logged and treated as "start empty," matching existing first-run behavior; no historical/worklog data is at risk, only active timer state.
- [Re-enabling `TreatWarningsAsErrors` after the retarget surfaces a long tail of unrelated new warnings] → Triaged as its own follow-up pass, separate from this change's three core concerns; individual warnings can be fixed or explicitly suppressed with justification rather than blocking this migration.
- [No installer means no Start Menu shortcut, no Add/Remove Programs entry, no per-machine install option] → Accepted trade-off per explicit user decision; can be revisited later as a separate, independent concern if desired.
- [Framework-dependent exe requires the .NET 10 Desktop Runtime installed separately; a machine without it can't run the app until that's installed] → Windows surfaces its own runtime-install prompt automatically; README/release notes link directly to the .NET 10 Desktop Runtime download to set expectations upfront.

## Migration Plan

1. Convert `StopWatch.csproj`/`StopWatchTest.csproj` to SDK-style, target `net10.0-windows`, `TreatWarningsAsErrors=false`.
2. Get the app compiling and running on net10 (address only what's needed to build/run, not warning cleanup).
3. Replace `BinaryFormatter` read/write in `Settings.cs` with the JSON + legacy-NRBF-fallback scheme described above.
4. Re-enable `TreatWarningsAsErrors=true`; triage remaining warnings.
5. Remove `StopWatchSetup.vdproj`/`StopWatchSetup.sln`; document/script the `dotnet publish` framework-dependent single-file command as the release artifact, noting the .NET 10 Desktop Runtime prerequisite.

No rollback mechanism beyond normal version control - this is a build/runtime migration with no server-side or shared-state component. Users on the old .NET Framework build are unaffected until they install the new version; the settings migration path only triggers when it encounters a legacy-format blob.
