# Tasks

## 1. Contract 1.2 types and version

- [x] 1.1 In `StopWatch.Plugin.Abstractions`, add `PluginSubtask.IssueType` (never null) with a new `(key, summary, issueType)` constructor, keeping the two-argument one; add the `CreateSubtaskAsync(parentKey, summary, issueTypeName)` and `GetSubtaskTypesAsync(projectKey)` members to `IJiraApi` with their doc comments; verify the Abstractions project builds
- [x] 1.2 Bump `PluginContract.ContractVersion` and the Abstractions `<Version>` to 1.2 and update `PluginLoaderTest` (`ContractVersion_IsOnePointOne` and the 1.1/1.2 compatibility cases: a 1.1 plugin loads on a 1.2 host, a 1.2 plugin is rejected by a 1.1 host); verify those tests pass

## 2. Host implementation

- [x] 2.1 In `PluginJiraApi`, make the two-argument `CreateSubtaskAsync` delegate to the new overload and resolve the type by trimmed, case-insensitive name against `GetSubtaskTypes`; return `null` and log the parent, the requested type and the types offered when it is not found, creating nothing; verify with `PluginJiraApiTest` cases for a known type (the right id reaches `CreateSubtask`), different case, unknown type (no `CreateSubtask` call, logged), null, empty and whitespace names (first type, same as the two-argument overload), no session, and an exception
- [x] 2.2 In `PluginJiraApi`, map `IssueTypeName` into `PluginSubtask.IssueType` in `GetSubtasksAsync`; verify a `PluginJiraApiTest` case that checks the mapped type and one for an issue with no type name (empty string)
- [x] 2.3 In `PluginJiraApi`, implement `GetSubtaskTypesAsync` (names, empty list when none, `null` when unreadable, no session or an exception); verify with `PluginJiraApiTest` cases for each
- [x] 2.4 In `CountingJiraApi`, add the three-argument overload that counts once when a key comes back, and pass `GetSubtaskTypesAsync` through uncounted; verify in `TimeLoadPluginPartsTest` that the new overload counts on success, does not count on `null`, and that the two-argument overload still counts once
- [x] 2.5 Build the whole solution and run `dotnet test StopWatch.sln --settings .runsettings`; verify no implementer of `IJiraApi` (including `Samples/`) fails to compile and the suite is green

## 3. Documentation

- [x] 3.1 In `docs/plugins.md`, add the 1.2 row to the contract table and document the new overload, `GetSubtaskTypesAsync`, `PluginSubtask.IssueType`, the null-on-unknown-type rule and the name matching; verify the section matches the code and that the NuGet `Version` example still follows `contractVersion`
