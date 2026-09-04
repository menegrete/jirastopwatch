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

    internal sealed class Settings
    {
        public static readonly Settings Instance = new Settings();

        #region public members
        public string JiraBaseUrl { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool MinimizeToTray { get; set; }
        public int IssueCount { get; set; }
        public bool AllowMultipleTimers { get; set; }
        public bool IncludeProjectName { get; set; }

        public SaveTimerSetting SaveTimerState { get; set; }
        public ThemeMode Theme { get; set; }
        public PauseAndResumeSetting PauseOnSessionLock { get; set; }
        public WorklogCommentSetting PostWorklogComment { get; set; }

        public string Username { get; set; }
        public string ApiToken { get; set; }
        public bool FirstRun { get; set; }

        public int CurrentFilter { get; set; }

        public List<PersistedIssue> PersistedIssues { get; private set; }

        public string StartTransitions { get; set; }

        public bool LoggingEnabled { get; set; }

        public bool CheckForUpdate { get; set; }

        public int MaxIssues { get; set; }

        /// <summary>
        /// Where the mini timer view was left, as "x,y" in virtual screen
        /// coordinates. Empty until the user has moved it at least once.
        /// Run it through <see cref="ScreenPlacement"/> before using it: the
        /// screen it refers to may not exist any more.
        /// </summary>
        public string MiniViewLocation { get; set; }
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

            this.CurrentFilter = Properties.Settings.Default.CurrentFilter;

            this.PersistedIssues = ReadIssues(Properties.Settings.Default.PersistedIssues);

            this.AllowMultipleTimers = Properties.Settings.Default.AllowMultipleTimers;

            this.StartTransitions = Properties.Settings.Default.StartTransitions;

            this.LoggingEnabled = Properties.Settings.Default.LoggingEnabled;

            CheckForUpdate = Properties.Settings.Default.CheckForUpdate;

            this.MaxIssues = Properties.Settings.Default.MaxIssues;

            this.MiniViewLocation = Properties.Settings.Default.MiniViewLocation ?? "";
        }


        public void Save()
        {
            lock (_writeLock)
            {
                Properties.Settings.Default.JiraBaseUrl = this.JiraBaseUrl;

                Properties.Settings.Default.AlwaysOnTop = this.AlwaysOnTop;
                Properties.Settings.Default.MinimizeToTray = this.MinimizeToTray;
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

                Properties.Settings.Default.CurrentFilter = this.CurrentFilter;

                Properties.Settings.Default.PersistedIssues = WriteIssues(this.PersistedIssues);

                Properties.Settings.Default.AllowMultipleTimers = this.AllowMultipleTimers;

                Properties.Settings.Default.StartTransitions = this.StartTransitions;

                Properties.Settings.Default.LoggingEnabled = this.LoggingEnabled;

                Properties.Settings.Default.CheckForUpdate = CheckForUpdate;

                Properties.Settings.Default.MaxIssues = this.MaxIssues;

                Properties.Settings.Default.MiniViewLocation = this.MiniViewLocation ?? "";

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
