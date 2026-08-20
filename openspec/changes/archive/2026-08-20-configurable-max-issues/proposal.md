## Why

Users want flexibility in how many issues they can track simultaneously. The current hardcoded limit of 20 is restrictive for some workflows. Making this configurable allows each user to choose a limit that fits their needs while maintaining bounds to prevent UI performance degradation.

## What Changes

- Remove hardcoded `maxIssues` constant from MainForm
- Add `MaxIssues` to user-configurable settings (stored in app config)
- Add numeric input control to SettingsForm for users to set their preferred limit
- Default: 20 issues (current limit)
- Constraints: minimum 1, maximum 40
- Setting takes effect immediately when user closes settings dialog

## Capabilities

### New Capabilities

- `issue-tracking/max-issues-limit`: User-configurable maximum number of issues that can be tracked simultaneously. Settings persist per user.

### Modified Capabilities

<!-- None - this is a new feature, not a behavior change to existing capabilities -->

## Impact

- **Settings storage**: New `MaxIssues` property in application settings file
- **UI**: New numeric field in SettingsForm for configuring the limit
- **MainForm**: Replace 5 references to hardcoded `maxIssues` constant with `settings.MaxIssues`
- **Settings.Designer.cs**: Add new `MaxIssues` setting property
- **Settings.cs**: Add property and syncing logic

No breaking changes. Fully backward compatible - existing users keep the default of 20.
