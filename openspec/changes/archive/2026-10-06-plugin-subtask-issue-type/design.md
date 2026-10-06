# Design

## Context

See `proposal.md` for the motivation and the spec delta for the observable behavior. This covers how it is built.

- `IJiraApi` is implemented by `PluginJiraApi` (the single instance shared by all plugins) and by `CountingJiraApi`, the per-invocation decorator that counts successful writes for the time-load pipeline. Nothing else implements it; tests use Moq.
- `PluginJiraApi.CreateSubtaskAsync` already asks `IJiraOperations.GetSubtaskTypes(projectKey)` and takes `types.Value[0].Id`. The project key is the parent key up to its last dash. Failure is reported as `null`, exceptions are caught and logged through `Action<string, Exception>`.
- `JiraClient.GetSubtasks` already fills `JiraIssueInfo.IssueTypeName`, and `JiraIssueType` already has `Name`. The host has the data; the contract just drops it.
- Today a failure that is not an exception (no session, no types) returns `null` silently.

## Goals / Non-Goals

**Goals:**
- Additive contract 1.2 that existing 1.1 plugins load against without change.
- The two-argument `CreateSubtaskAsync` keeps its exact behavior and shares one implementation with the new overload.
- An unknown type is a visible, diagnosable failure, never a silent substitution.

**Non-Goals:**
- Identifying types by id, or creating subtasks with more than a type and a summary.
- Caching subtask types: each call reads them, as it does today.

## Decisions

### 1. The old overload delegates to the new one

`CreateSubtaskAsync(parent, summary)` calls `CreateSubtaskAsync(parent, summary, null)` in `PluginJiraApi`, so there is one code path for type resolution. A `null` or empty name selects `types[0]`, as before.

*Rejected:* duplicating the body. Two copies of the key parsing and failure handling would drift.

### 2. Name matching: trimmed, `OrdinalIgnoreCase`

The requested name is trimmed and compared with `StringComparison.OrdinalIgnoreCase` to each type's name. A name that is only whitespace counts as empty. Culture-sensitive comparison is avoided so a Turkish-locale machine does not change which type matches. If the project defines two types that differ only in case, the first one wins; Jira does not allow that in practice.

### 3. An unknown type is logged with the types the project offers

The existing log callback takes an exception, and this failure has none. It is called with the message and a `null` exception, and the message names the parent, the requested type and the names the project does offer, so a misconfigured plugin setting is diagnosable from the log alone. The implementation must check that the callback chain tolerates a `null` exception.

*Rejected:* throwing an internal exception just to log it; and a silent `null`, which is what the issue asks to avoid.

### 4. `PluginSubtask.IssueType` is never null

The new constructor `PluginSubtask(key, summary, issueType)` stores `issueType ?? ""`. The two-argument constructor stays and yields `""`. The host maps `JiraIssueInfo.IssueTypeName` into it.

### 5. `GetSubtaskTypesAsync` mirrors the other reads

Returns `IReadOnlyList<string>` of names; an empty list when the project has none, `null` when unreadable (no session, request failure, blank key) or on exception, which is logged like the other methods. It runs inside `Task.Run`, like the rest of `PluginJiraApi`.

### 6. `CountingJiraApi`: each overload forwards to its twin and counts once

The three-argument overload calls `inner.CreateSubtaskAsync(parent, summary, type)` and counts when the key is non-null. The two-argument overload keeps calling the two-argument one on `inner`, so each call counts exactly once and a Moq setup on either overload keeps working. `GetSubtaskTypesAsync` is a read and passes through uncounted.

## Risks / Trade-offs

- [A third-party class implementing `IJiraApi` stops compiling against 1.2] → The interface is meant to be consumed, not implemented; the same happened in 1.1. Documented in the version table.
- [A plugin gets `null` for an unknown type and cannot tell it from a network failure] → The log distinguishes them; `GetSubtaskTypesAsync` lets the plugin validate its setting before creating.
- [Type names can be renamed by a Jira admin, so a stored name stops matching] → The failure is explicit and logged, which is the intended behavior.
