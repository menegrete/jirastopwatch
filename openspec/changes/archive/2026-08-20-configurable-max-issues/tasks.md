## 1. Add Setting to Application Configuration

- [x] 1.1 Add MaxIssues property to Settings.Designer.cs with default value "20" (following the pattern of existing properties like IssueCount)
- [x] 1.2 Add MaxIssues property to Settings.cs class with get/set accessor

## 2. Wire Settings Persistence

- [x] 2.1 Add read logic in Settings.ReadSettings(): `this.MaxIssues = Properties.Settings.Default.MaxIssues;`
- [x] 2.2 Add save logic in Settings.Save(): `Properties.Settings.Default.MaxIssues = this.MaxIssues;`

## 3. Add Settings Form UI

- [x] 3.1 Add NumericUpDown control to SettingsForm.Designer.cs (set Name to "nudMaxIssues", Minimum to 1, Maximum to 40, DecimalPlaces to 0)
- [x] 3.2 Add label "Maximum issues to display:" next to the NumericUpDown
- [x] 3.3 Initialize NumericUpDown in SettingsForm constructor: `nudMaxIssues.Value = this.settings.MaxIssues;`
- [x] 3.4 Add logic to SettingsForm_FormClosed to save the value: `this.settings.MaxIssues = (int)nudMaxIssues.Value;`

## 4. Update MainForm to Use Setting

- [x] 4.1 Remove hardcoded constant: Delete line 767 `private const int maxIssues = 20;`
- [x] 4.2 Replace maxIssues reference in IssueAdd() (line 332): Change `maxIssues` to `settings.MaxIssues`
- [x] 4.3 Replace maxIssues reference in InitializeIssueControls() condition (line 346): Change `maxIssues` to `settings.MaxIssues`
- [x] 4.4 Replace maxIssues reference in InitializeIssueControls() assignment (line 349): Change `maxIssues` to `settings.MaxIssues`
- [x] 4.5 Replace maxIssues reference in InitializeIssueControls() tooltip (line 354): Change `maxIssues.ToString()` to `settings.MaxIssues.ToString()`

## 5. Testing & Verification

- [x] 5.1 Test: Open Settings, set MaxIssues to 30, click OK, verify "Add Issue" button enables up to 30 issues
- [x] 5.2 Test: Set MaxIssues to 15, close and reopen Settings, verify value persists as 15
- [x] 5.3 Test: Close application completely, reopen, verify MaxIssues setting is still 15
- [x] 5.4 Test: Try to set NumericUpDown below 1 or above 40, verify values are rejected by control
- [x] 5.5 Test: With 25 issues loaded and MaxIssues set to 20, verify "Add Issue" button is disabled with correct tooltip
- [x] 5.6 Test: Start with default installation, verify MaxIssues defaults to 20
