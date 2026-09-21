# Basic setup

## Connecting to Jira

The first time you run Jira StopWatch, the Settings window opens automatically. Fill in:

- **JIRA base url** — the base URL of your Jira instance (e.g. `https://yourcompany.atlassian.net`)
- **Username** — your Jira username or email address
- **API Token** — an API token for your account (for Jira Cloud, generate one from the ["Get an API Token"](https://id.atlassian.com/manage/api-tokens) link right next to the field; for Jira Server/Data Center, use your password)

These three fields are all that's needed to connect — Jira StopWatch authenticates with standard HTTP Basic auth, so the same setup works for both Jira Cloud and Jira Server/Data Center.

The API token is encrypted at rest using Windows DPAPI, tied to your Windows user account — it's never stored in plain text.

Click **OK** to save and connect (Cancel discards any changes). You can reopen this dialog any time from the settings button in the main window.

## Checking your connection

The status bar at the bottom of the main window shows **Connecting...**, **Connected**, or **Not connected**. If it shows **Not connected**, click the status text to see the underlying error message from Jira.

## Next steps

- [Basic usage](usage.md) — adding issues and tracking time
- [Advanced settings](advanced-settings.md) — themes, mini timer, taskbar widget, and more
