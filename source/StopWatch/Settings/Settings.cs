/**
 * Copyright 2023 Y. Meyer-Norwood
 * Copyright 2020 Dan Tulloh
 * Copyright 2016 Carsten Gehling
 *
 * For a full list of contributing authors, see:
 *
 *     https://jirastopwatch.com/contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at:
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Formats.Nrbf;
using System.IO;
using System.Text.Json;
using StopWatch.Logging;

namespace StopWatch
{
    public enum SaveTimerSetting
    {
        NoSave,
        SavePause,
        SaveRunActive
    }

    public enum PauseAndResumeSetting
    {
        NoPause,
        Pause,
        PauseAndResume
    }

    public enum WorklogCommentSetting
    {
        WorklogOnly,
        CommentOnly,
        WorklogAndComment
    }

    /// <summary>
    /// How much room each issue row takes. Compact is what the WinForms window
    /// looked like; spacious trades height for legibility. See the issue-list
    /// spec, "El usuario elige la densidad de la lista".
    /// </summary>
    public enum ListDensity
    {
        Compact = 0,
        Spacious = 1
    }

    /// <summary>
    /// Where minimizing the main window with the native Windows control sends
    /// it. See the minimize-behavior spec, "El usuario elige adónde va la
    /// ventana principal al minimizarla".
    /// </summary>
    public enum MinimizeBehavior
    {
        MiniView = 0,
        Tray = 1,
        TaskbarWidget = 2
    }

    internal sealed class Settings
    {
        public static readonly Settings Instance = new Settings();

        #region public members
        public string JiraBaseUrl { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool MinimizeToTray { get; set; }

        /// <summary>
        /// Where minimizing the main window sends it. Replaces
        /// <see cref="MinimizeToTray"/>, which stays only as a one-time
        /// migration source - see <see cref="ReadSettings"/>.
        /// </summary>
        public MinimizeBehavior MinimizeBehavior { get; set; }
        public int IssueCount { get; set; }
        public bool AllowMultipleTimers { get; set; }

        /// <summary>
        /// The most timers that may run at once when <see cref="AllowMultipleTimers"/>
        /// is on. Ignored when it is off, since at most one timer runs then anyway.
        /// </summary>
        public int MaxConcurrentTimers { get; set; }

        public bool IncludeProjectName { get; set; }

        public SaveTimerSetting SaveTimerState { get; set; }
        public ThemeMode Theme { get; set; }
        public PauseAndResumeSetting PauseOnSessionLock { get; set; }
        public WorklogCommentSetting PostWorklogComment { get; set; }

        public string Username { get; set; }
        public string ApiToken { get; set; }
        public bool FirstRun { get; set; }

        public List<PersistedIssue> PersistedIssues { get; private set; }

        public string StartTransitions { get; set; }

        public bool LoggingEnabled { get; set; }

        public int MaxIssues { get; set; }

        /// <summary>
        /// Where the mini timer view was left, as "x,y" in virtual screen
        /// coordinates. Empty until the user has moved it at least once.
        /// Run it through <see cref="ScreenPlacement"/> before using it: the
        /// screen it refers to may not exist any more.
        /// </summary>
        public string MiniViewLocation { get; set; }

        /// <summary>How much room each issue row takes.</summary>
        public ListDensity ListDensity { get; set; }

        /// <summary>
        /// The width the user left the main window at. The height is not saved:
        /// it is derived from the rows. Run it through
        /// <see cref="ScreenPlacement"/> before using it - the screen it was
        /// saved on may be smaller now.
        /// </summary>
        public int MainWindowWidth { get; set; }

        /// <summary>
        /// Whether the app checks for, downloads and stages newer releases on
        /// its own. On by default; turning it off skips the startup check
        /// entirely. See the auto-update spec, "El usuario puede desactivar
        /// el chequeo de actualizaciones".
        /// </summary>
        public bool CheckForUpdates { get; set; }

        /// <summary>
        /// Which single monitor (0 = primary, 1+ = secondary, in the same
        /// order <see cref="System.Windows.Forms.Screen.AllScreens"/> would
        /// once sorted by position) shows the taskbar widget when
        /// <see cref="MinimizeBehavior"/> is <see cref="MinimizeBehavior.TaskbarWidget"/>.
        /// Falls back to the primary monitor (0) if that monitor is not
        /// currently connected. See the taskbar-widget-view spec, "El
        /// usuario elige en qué monitor aparece el widget".
        /// </summary>
        public int TaskbarWidgetMonitor { get; set; }

        /// <summary>
        /// Where the main window was left, as "x,y" in virtual screen
        /// coordinates. Empty until the user has moved it at least once. Run
        /// it through <see cref="ScreenPlacement"/> before using it: the
        /// screen it refers to may not exist any more.
        /// </summary>
        public string MainWindowLocation { get; set; }

        /// <summary>
        /// Whether the main window was left maximized. Minimized is never
        /// recorded here - see the main-window-placement spec, "Minimizing
        /// does not change the remembered state".
        /// </summary>
        public bool MainWindowMaximized { get; set; }
        #endregion


        #region public methods
        public bool Load()
        {
            try
            {
                ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            }
            catch (ConfigurationErrorsException ex)
            {
                string filename = ex.Filename;
                if (File.Exists(filename))
                    File.Delete(filename);

                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.Reload();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.FirstRun = true;
                Properties.Settings.Default.Save();

                ReadSettings();
                return false;
            }

            // Check for upgrade because of application version change
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            ReadSettings();

            return true;
        }

        private void ReadSettings()
        {
            this.JiraBaseUrl = Properties.Settings.Default.JiraBaseUrl ?? "";

            this.AlwaysOnTop = Properties.Settings.Default.AlwaysOnTop;
            this.IncludeProjectName = Properties.Settings.Default.IncludeProjectName;
            this.MinimizeToTray = Properties.Settings.Default.MinimizeToTray;
            if (!Properties.Settings.Default.MinimizeBehaviorMigrated)
            {
                this.MinimizeBehavior = MigrateMinimizeBehavior(this.MinimizeToTray);
                Properties.Settings.Default.MinimizeBehavior = (int)this.MinimizeBehavior;
                Properties.Settings.Default.MinimizeBehaviorMigrated = true;
                Properties.Settings.Default.Save();
            }
            else
            {
                this.MinimizeBehavior = (MinimizeBehavior)Properties.Settings.Default.MinimizeBehavior;
            }
            this.IssueCount = Properties.Settings.Default.IssueCount;
            this.Username = Properties.Settings.Default.Username;
            if (Properties.Settings.Default.ApiToken != "")
                this.ApiToken = DPAPI.Decrypt(Properties.Settings.Default.ApiToken);
            else
                this.ApiToken = "";
            this.FirstRun = Properties.Settings.Default.FirstRun;
            this.SaveTimerState = (SaveTimerSetting)Properties.Settings.Default.SaveTimerState;
            this.Theme = (ThemeMode)Properties.Settings.Default.Theme;
            this.PauseOnSessionLock = (PauseAndResumeSetting)Properties.Settings.Default.PauseOnSessionLock;
            this.PostWorklogComment = (WorklogCommentSetting)Properties.Settings.Default.PostWorklogComment;

            this.PersistedIssues = ReadIssues(Properties.Settings.Default.PersistedIssues);

            this.AllowMultipleTimers = Properties.Settings.Default.AllowMultipleTimers;
            this.MaxConcurrentTimers = Properties.Settings.Default.MaxConcurrentTimers;

            this.StartTransitions = Properties.Settings.Default.StartTransitions;

            this.LoggingEnabled = Properties.Settings.Default.LoggingEnabled;

            this.MaxIssues = Properties.Settings.Default.MaxIssues;

            this.MiniViewLocation = Properties.Settings.Default.MiniViewLocation ?? "";

            this.ListDensity = (ListDensity)Properties.Settings.Default.ListDensity;

            this.MainWindowWidth = Properties.Settings.Default.MainWindowWidth;

            this.CheckForUpdates = Properties.Settings.Default.CheckForUpdates;

            this.TaskbarWidgetMonitor = Properties.Settings.Default.TaskbarWidgetMonitor;

            this.MainWindowLocation = Properties.Settings.Default.MainWindowLocation ?? "";

            this.MainWindowMaximized = Properties.Settings.Default.MainWindowMaximized;
        }


        public void Save()
        {
            lock (_writeLock)
            {
                Properties.Settings.Default.JiraBaseUrl = this.JiraBaseUrl;

                Properties.Settings.Default.AlwaysOnTop = this.AlwaysOnTop;
                Properties.Settings.Default.MinimizeToTray = this.MinimizeToTray;
                Properties.Settings.Default.MinimizeBehavior = (int)this.MinimizeBehavior;
                Properties.Settings.Default.IssueCount = this.IssueCount;
                Properties.Settings.Default.IncludeProjectName = this.IncludeProjectName;

                Properties.Settings.Default.Username = this.Username;
                if (this.ApiToken != "")
                    Properties.Settings.Default.ApiToken = DPAPI.Encrypt(this.ApiToken);
                else
                    Properties.Settings.Default.ApiToken = "";

                Properties.Settings.Default.FirstRun = this.FirstRun;
                Properties.Settings.Default.SaveTimerState = (int)this.SaveTimerState;
                Properties.Settings.Default.Theme = (int)this.Theme;
                Properties.Settings.Default.PauseOnSessionLock = (int)this.PauseOnSessionLock;
                Properties.Settings.Default.PostWorklogComment = (int)this.PostWorklogComment;

                Properties.Settings.Default.PersistedIssues = WriteIssues(this.PersistedIssues);

                Properties.Settings.Default.AllowMultipleTimers = this.AllowMultipleTimers;
                Properties.Settings.Default.MaxConcurrentTimers = this.MaxConcurrentTimers;

                Properties.Settings.Default.StartTransitions = this.StartTransitions;

                Properties.Settings.Default.LoggingEnabled = this.LoggingEnabled;

                Properties.Settings.Default.MaxIssues = this.MaxIssues;

                Properties.Settings.Default.MiniViewLocation = this.MiniViewLocation ?? "";

                Properties.Settings.Default.ListDensity = (int)this.ListDensity;

                Properties.Settings.Default.MainWindowWidth = this.MainWindowWidth;

                Properties.Settings.Default.CheckForUpdates = this.CheckForUpdates;

                Properties.Settings.Default.TaskbarWidgetMonitor = this.TaskbarWidgetMonitor;

                Properties.Settings.Default.MainWindowLocation = this.MainWindowLocation ?? "";

                Properties.Settings.Default.MainWindowMaximized = this.MainWindowMaximized;

                Properties.Settings.Default.Save();
            }
        }

        public List<PersistedIssue> ReadIssues(string data)
        {
            if (string.IsNullOrEmpty(data))
                return new List<PersistedIssue>();

            // Going-forward format: a plain JSON array.
            if (data.TrimStart().StartsWith("["))
                return JsonSerializer.Deserialize<List<PersistedIssue>>(data) ?? new List<PersistedIssue>();

            // Anything else is a pre-upgrade BinaryFormatter blob. Decode it with
            // System.Formats.Nrbf (safe: it walks the record graph without ever
            // instantiating/executing the serialized types) and immediately
            // re-save as JSON, so this path only ever runs once per installation.
            try
            {
                List<PersistedIssue> issues = ReadLegacyIssues(data);

                Properties.Settings.Default.PersistedIssues = WriteIssues(issues);
                Properties.Settings.Default.Save();

                return issues;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Failed to read legacy persisted issues; starting with an empty list.", ex);
                return new List<PersistedIssue>();
            }
        }


        public string WriteIssues(List<PersistedIssue> issues)
        {
            return JsonSerializer.Serialize(issues);
        }


        /// <summary>
        /// One-time mapping from the retired <see cref="MinimizeToTray"/> bool
        /// to the new setting. Internal (not private) so it's directly
        /// testable without going through <see cref="Properties.Settings.Default"/>.
        /// </summary>
        internal static MinimizeBehavior MigrateMinimizeBehavior(bool minimizeToTray)
        {
            return minimizeToTray ? MinimizeBehavior.Tray : MinimizeBehavior.MiniView;
        }
        #endregion


        #region legacy BinaryFormatter migration
        private static List<PersistedIssue> ReadLegacyIssues(string data)
        {
            byte[] bytes = Convert.FromBase64String(data);

            using (MemoryStream ms = new MemoryStream(bytes))
            {
                ClassRecord listRecord = (ClassRecord)NrbfDecoder.Decode(ms);

                int count = (int)listRecord.GetRawValue("_size");
                SZArrayRecord<SerializationRecord> items = (SZArrayRecord<SerializationRecord>)listRecord.GetRawValue("_items");
                SerializationRecord[] array = items.GetArray();

                List<PersistedIssue> issues = new List<PersistedIssue>();
                for (int i = 0; i < count; i++)
                    issues.Add(ReadLegacyIssue((ClassRecord)array[i]));

                return issues;
            }
        }


        // PersistedIssue's auto-properties are read back by their compiler-generated
        // backing field names ("<PropertyName>k__BackingField"), which is how
        // BinaryFormatter recorded them.
        private static PersistedIssue ReadLegacyIssue(ClassRecord record)
        {
            return new PersistedIssue
            {
                Key = (string)record.GetRawValue("<Key>k__BackingField"),
                TimerRunning = (bool)record.GetRawValue("<TimerRunning>k__BackingField"),
                InitialStartTime = ReadLegacyDateTimeOffset(record.GetRawValue("<InitialStartTime>k__BackingField")),
                SessionStartTime = (DateTime)record.GetRawValue("<SessionStartTime>k__BackingField"),
                TotalTime = (TimeSpan)record.GetRawValue("<TotalTime>k__BackingField"),
                Comment = (string)record.GetRawValue("<Comment>k__BackingField"),
                EstimateUpdateMethod = (EstimateUpdateMethods)(int)((ClassRecord)record.GetRawValue("<EstimateUpdateMethod>k__BackingField")).GetRawValue("value__"),
                EstimateUpdateValue = (string)record.GetRawValue("<EstimateUpdateValue>k__BackingField"),
            };
        }


        // DateTimeOffset serializes itself (ISerializable) as an internal UTC-ish
        // "DateTime" field plus an "OffsetMinutes" field - not as the local/clock
        // value its own public members expose. Reconstructing it means treating
        // that field as UTC and then converting to the recorded offset.
        private static DateTimeOffset? ReadLegacyDateTimeOffset(object raw)
        {
            if (raw is null)
                return null;

            ClassRecord dtoRecord = (ClassRecord)raw;
            DateTime utcish = (DateTime)dtoRecord.GetRawValue("DateTime");
            short offsetMinutes = (short)dtoRecord.GetRawValue("OffsetMinutes");

            return new DateTimeOffset(DateTime.SpecifyKind(utcish, DateTimeKind.Utc)).ToOffset(TimeSpan.FromMinutes(offsetMinutes));
        }
        #endregion


        #region private methods
        /// <summary>
        /// Internal rather than private so that a test can hand a service its
        /// own settings instead of mutating the process-wide
        /// <see cref="Instance"/>. The application still uses only Instance.
        /// </summary>
        internal Settings()
        {
            this.PersistedIssues = new List<PersistedIssue>();
        }
        #endregion


        private Object _writeLock = new Object(); 
    }
}
