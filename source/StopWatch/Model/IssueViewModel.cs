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
using System.ComponentModel;

namespace StopWatch
{
    /// <summary>
    /// The state of one issue row, owned by the model rather than by the
    /// controls that display it.
    ///
    /// Until this existed, a row's key lived in a ComboBox's Text, its summary
    /// in a Label's Text and its running state in a TextBox's BackColor, which
    /// is why the persistence code had to walk controls and the Jira calls had
    /// to marshal back to the UI thread just to read their own input.
    ///
    /// This class does not tick on its own: whoever displays it drives
    /// <see cref="Refresh"/> at whatever rate that display needs, the same
    /// contract <see cref="ActiveTimerViewModel"/> uses.
    /// </summary>
    internal class IssueViewModel : ITimerSource, INotifyPropertyChanged
    {
        #region public members
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raised when this row's timer is started, so that the single-timer
        /// rule can pause the others.
        /// </summary>
        public event EventHandler TimerStarted;

        public string IssueKey
        {
            get { return issueKey; }
            set
            {
                string newValue = value ?? "";
                if (issueKey == newValue)
                    return;

                issueKey = newValue;
                Raise("IssueKey");

                // The old summary belonged to the old key - drop it so CanOpen
                // reflects the new key's unresolved state immediately, rather
                // than staying enabled against a summary that no longer
                // matches what's in the box.
                Summary = "";
            }
        }


        /// <summary>
        /// The summary as resolved from Jira, already carrying whatever the
        /// query composed - project prefix and parent summary included. Empty
        /// while it has not been resolved yet.
        /// </summary>
        public string Summary
        {
            get { return summary; }
            set
            {
                string newValue = value ?? "";
                if (summary == newValue)
                    return;

                summary = newValue;
                Raise("Summary");
                Raise("CanOpen");
            }
        }


        /// <summary>
        /// The worklog comment carried between the worklog dialog and the post,
        /// and kept across runs. Null when there is none.
        /// </summary>
        public string Comment
        {
            get { return comment; }
            set
            {
                if (comment == value)
                    return;

                comment = value;
                Raise("Comment");
                Raise("HasComment");
            }
        }


        /// <summary>Whether a comment is waiting to be posted with the worklog.</summary>
        public bool HasComment
        {
            get { return !string.IsNullOrEmpty(comment); }
        }


        public EstimateUpdateMethods EstimateUpdateMethod
        {
            get { return estimateUpdateMethod; }
            set { Set(ref estimateUpdateMethod, value, "EstimateUpdateMethod"); }
        }


        public string EstimateUpdateValue
        {
            get { return estimateUpdateValue; }
            set { Set(ref estimateUpdateValue, value, "EstimateUpdateValue"); }
        }


        /// <summary>Whether this is the row selected in the main window.</summary>
        public bool IsCurrent
        {
            get { return isCurrent; }
            set { Set(ref isCurrent, value, "IsCurrent"); }
        }


        public WatchTimer WatchTimer { get; private set; }

        /// <summary>
        /// Elapsed time as of the last <see cref="Refresh"/>. Read from here
        /// rather than from the timer so that a display bound to it is told when
        /// it changes.
        /// </summary>
        public TimeSpan TimeElapsed
        {
            get { return timeElapsed; }
        }


        /// <summary>Elapsed time in the Jira notation the time field shows.</summary>
        public string TimeElapsedText
        {
            get { return JiraTimeHelpers.TimeSpanToJiraTime(timeElapsed); }
        }


        public bool IsRunning
        {
            get { return isRunning; }
        }


        /// <summary>
        /// Whether resetting is available: there is something to reset only
        /// while the timer runs or has accumulated time.
        /// </summary>
        public bool CanReset
        {
            get { return isRunning || timeElapsed.Ticks > 0; }
        }


        /// <summary>
        /// Whether posting is available. Jira rejects a worklog below a minute,
        /// so a row under that threshold cannot post yet.
        ///
        /// Reads the cached <see cref="timeElapsed"/> rather than
        /// WatchTimer.TimeElapsedNearestMinute directly - the same reason
        /// CanReset does. Refresh() sets WatchTimer's value before it runs
        /// (via SetTimeElapsed), so a getter that reads WatchTimer live would
        /// already reflect the new value when Refresh() captures its "before"
        /// snapshot, and the before/after comparison below would never see a
        /// difference to announce.
        /// </summary>
        public bool CanPost
        {
            get { return Math.Ceiling(timeElapsed.TotalMinutes) >= 1; }
        }


        /// <summary>
        /// Whether the issue can be opened in a browser. Requires a resolved
        /// summary, not just a non-empty key - that's Jira's confirmation
        /// that the key is valid and the issue exists.
        /// </summary>
        public bool CanOpen
        {
            get { return !string.IsNullOrEmpty(summary); }
        }
        #endregion


        #region public methods
        public IssueViewModel()
        {
            WatchTimer = new WatchTimer();
            Refresh();
        }


        /// <summary>
        /// Re-reads the timer and raises change notifications for whatever
        /// actually changed. Everything derived from elapsed time is announced
        /// here, because none of it changes without the clock moving.
        /// </summary>
        public void Refresh()
        {
            bool newRunning = WatchTimer.Running;
            TimeSpan newElapsed = WatchTimer.TimeElapsed;

            bool wasReset = CanReset;
            bool wasPost = CanPost;

            if (newElapsed != timeElapsed)
            {
                timeElapsed = newElapsed;
                Raise("TimeElapsed");
                Raise("TimeElapsedText");
            }

            Set(ref isRunning, newRunning, "IsRunning");

            if (CanReset != wasReset)
                Raise("CanReset");
            if (CanPost != wasPost)
                Raise("CanPost");
        }


        public void Start()
        {
            if (WatchTimer.Running)
                return;

            WatchTimer.Start();
            Refresh();

            EventHandler handler = TimerStarted;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }


        public void Pause()
        {
            WatchTimer.Pause();
            Refresh();
        }


        /// <summary>
        /// Toggles the timer. Starting raises <see cref="TimerStarted"/> so that
        /// the single-timer rule still applies when resuming.
        /// </summary>
        public void StartStop()
        {
            if (WatchTimer.Running)
                Pause();
            else
                Start();
        }


        public void Reset()
        {
            WatchTimer.Reset();
            Refresh();
        }


        /// <summary>Sets elapsed time to what the edit-time dialog returned.</summary>
        public void SetTimeElapsed(TimeSpan value)
        {
            WatchTimer.TimeElapsed = value;
            Refresh();
        }


        /// <summary>Loads this row's state from what was saved on the last run.</summary>
        public void Hydrate(PersistedIssue persisted, SaveTimerSetting saveTimerState)
        {
            if (persisted == null)
                return;

            IssueKey = persisted.Key;

            if (saveTimerState == SaveTimerSetting.NoSave)
                return;

            WatchTimer.SetState(new TimerState
            {
                Running = saveTimerState == SaveTimerSetting.SavePause ? false : persisted.TimerRunning,
                SessionStartTime = persisted.SessionStartTime,
                InitialStartTime = persisted.InitialStartTime,
                TotalTime = persisted.TotalTime
            });

            Comment = persisted.Comment;
            EstimateUpdateMethod = persisted.EstimateUpdateMethod;
            EstimateUpdateValue = persisted.EstimateUpdateValue;

            Refresh();
        }


        /// <summary>Captures this row's state for the next run.</summary>
        public PersistedIssue Persist()
        {
            TimerState state = WatchTimer.GetState();

            return new PersistedIssue
            {
                Key = IssueKey,
                TimerRunning = state.Running,
                SessionStartTime = state.SessionStartTime,
                InitialStartTime = state.InitialStartTime,
                TotalTime = state.TotalTime,
                Comment = Comment,
                EstimateUpdateMethod = EstimateUpdateMethod,
                EstimateUpdateValue = EstimateUpdateValue
            };
        }
        #endregion


        #region private methods
        private void Set<T>(ref T field, T value, string property)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            Raise(property);
        }


        private void Raise(string property)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(property));
        }
        #endregion


        #region private members
        private string issueKey = "";
        private string summary = "";
        private string comment;
        private EstimateUpdateMethods estimateUpdateMethod = EstimateUpdateMethods.Auto;
        private string estimateUpdateValue;
        private bool isCurrent;

        private TimeSpan timeElapsed;
        private bool isRunning;
        #endregion
    }
}
