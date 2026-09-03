## Why

The app targets .NET Framework 4.8 with legacy-style `.csproj` files, a deprecated Visual Studio Installer (`.vdproj`) project, and a `BinaryFormatter`-based settings format that is obsolete and unsupported going forward. Moving to .NET 10 keeps the app on a supported, actively updated runtime. Since JiraStopWatch is a Windows-only desktop tool with no cross-platform ambitions, the migration only needs to preserve Windows compatibility, not add any.

## What Changes

- Retarget `StopWatch.csproj` and `StopWatchTest.csproj` from `.NET Framework 4.8` / legacy project format to SDK-style projects targeting `net10.0-windows`, dropping the many `packages.config` polyfill packages (`System.Buffers`, `System.Memory`, etc.) that only existed to backfill APIs .NET Framework 4.8 lacked natively.
- During the retargeting work, set `TreatWarningsAsErrors` to `false` in Debug so new-compiler warnings don't block getting the app compiling and running on net10; re-enable it once the retarget is done and remaining warnings have been triaged.
- Replace `BinaryFormatter` (used to persist `List<PersistedIssue>` in `user.config`) with JSON (`System.Text.Json`, already referenced). **BREAKING** (data-format change, mitigated below): a one-time, forward-only migration reads any existing Base64 `BinaryFormatter` blob via `System.Formats.Nrbf` (a safe, non-executing binary reader, already referenced but unused in the codebase) and rewrites it as JSON. If the legacy blob can't be read, the app logs the failure and starts with an empty persisted-issue list rather than failing to start.
- Remove `StopWatchSetup.vdproj` and the `StopWatchSetup.sln` installer project entirely. **BREAKING**: there is no longer an installer/MSI. Distribution becomes a single framework-dependent `win-x64` executable (`dotnet publish ... -p:PublishSingleFile=true`, no embedded runtime) that users download and run directly, with no Start Menu shortcut or uninstall entry. **Requires the .NET 10 Desktop Runtime to already be installed on the machine** - if it's missing, Windows shows its own "install the runtime" prompt rather than the app just failing silently.

## Capabilities

No spec-level (user-facing behavior) requirements change: the app still tracks time, talks to Jira, and persists settings the same way from the user's point of view. This is a platform/runtime/build migration, tracked here without capability deltas (`skip_specs: true`).

## Impact

- `source/StopWatch/StopWatch.csproj`, `source/StopWatchTest/StopWatchTest.csproj` - retargeted to SDK-style / net10.0-windows.
- `source/StopWatch/Settings/Settings.cs` - `ReadIssues`/`WriteIssues` reworked for JSON + legacy NRBF fallback.
- `source/StopWatch/packages.config`, `source/StopWatchTest/packages.config` - replaced by `PackageReference`s (only `RestSharp` remains an explicit dependency) plus a root `Directory.Packages.props` for centrally managed versions.
- `StopWatchSetup.sln`, `source/StopWatchSetup/StopWatchSetup.vdproj` - removed.
- Release/distribution process - now produces one framework-dependent `.exe` instead of an installer, with the .NET 10 Desktop Runtime as a separate user-installed prerequisite.
- Existing users' `user.config` - read once via the legacy-format fallback path, then rewritten in the new JSON format.
