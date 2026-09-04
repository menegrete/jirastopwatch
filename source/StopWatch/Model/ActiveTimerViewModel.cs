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
using System.Linq;

namespace StopWatch
{
    /// <summary>
    /// The slice of an issue row that the active-timer model needs. Exists so
    /// that the model can be tested without constructing an IssueControl, which
    /// drags in a Jira client and a window handle.
    /// </summary>
    internal interface ITimerSource
    {
        string IssueKey { get; }

        string Summary { get; }

        /// <summary>Whether this is the row selected in the main window.</summary>
        bool IsCurrent { get; }

        WatchTimer WatchTimer { get; }

        /// <summary>
        /// Toggles the timer exactly as the main window's button does,
        /// including raising TimerStarted so that the single-timer rule still
        /// applies when resuming.
        /// </summary>
        void StartStop();
    }


    /// <summary>
    /// Observable view of "the timer the user cares about right now".
    ///
    /// Owns the rule for picking the active issue, which used to be an ad-hoc
    /// walk over MainForm's issue controls repeated at every call site.
    ///
    /// This class does not tick on its own: whoever displays it drives Refresh()
    /// at whatever rate that display needs. The main window refreshes on issue
    /// events, the mini view refreshes once a second while it is visible.
    /// </summary>
    internal class ActiveTimerViewModel : INotifyPropertyChanged
    {
        #region public members
        public event PropertyChangedEventHandler PropertyChanged;

        public string IssueKey
        {
            get { return issueKey; }
        }

        public string Summary
        {
            get { return summary; }
        }

        public TimeSpan Elapsed
        {
            get { return elapsed; }
        }

        /// <summary>Elapsed time as a running clock, with seconds.</summary>
        public string ElapsedText
        {
            get { return JiraTimeHelpers.TimeSpanToClockTime(elapsed); }
        }

        public bool IsRunning
        {
            get { return isRunning; }
        }

        /// <summary>
        /// Whether timers other than the one shown are also running, so that a
        /// display can avoid presenting this time as everything being recorded.
        /// </summary>
        public bool HasOtherRunningTimers
        {
            get { return hasOtherRunningTimers; }
        }

        /// <summary>The row the model currently resolves to, or null if there are none.</summary>
        public ITimerSource ActiveSource
        {
            get { return active; }
        }

        /// <summary>
        /// Every row whose timer is running, in the order the main window shows
        /// them. The one place that answers "who is running", so that callers
        /// stop walking the issue rows themselves.
        /// </summary>
        public IEnumerable<ITimerSource> RunningSources
        {
            get { return running; }
        }
        #endregion


        #region public methods
        public ActiveTimerViewModel(Func<IEnumerable<ITimerSource>> sourcesProvider)
        {
            if (sourcesProvider == null)
                throw new ArgumentNullException("sourcesProvider");

            this.sourcesProvider = sourcesProvider;
            Refresh();
        }


        /// <summary>
        /// Records which row was started last. The main window calls this from
        /// its TimerStarted handler; it is what lets the model answer "the last
        /// one to start" without WatchTimer exposing its session start time.
        /// </summary>
        public void NotifyTimerStarted(ITimerSource source)
        {
            lastStarted = source;
            Refresh();
        }


        /// <summary>
        /// Re-resolves the active issue and re-reads its timer, raising change
        /// notifications for whatever actually changed.
        /// </summary>
        public void Refresh()
        {
            List<ITimerSource> sources = (sourcesProvider() ?? Enumerable.Empty<ITimerSource>()).ToList();
            running = sources.Where(s => s.WatchTimer.Running).ToList();

            ITimerSource resolved = Resolve(sources, running);

            SetActive(resolved);
            Set(ref issueKey, resolved == null ? "" : resolved.IssueKey ?? "", "IssueKey");
            Set(ref summary, resolved == null ? "" : resolved.Summary ?? "", "Summary");
            Set(ref isRunning, resolved != null && resolved.WatchTimer.Running, "IsRunning");
            Set(ref hasOtherRunningTimers, running.Count > 1, "HasOtherRunningTimers");

            TimeSpan newElapsed = resolved == null ? TimeSpan.Zero : resolved.WatchTimer.TimeElapsed;
            if (newElapsed != elapsed)
            {
                elapsed = newElapsed;
                Raise("Elapsed");
                Raise("ElapsedText");
            }
        }


        /// <summary>
        /// Pauses or resumes the active issue, with the same effect as using the
        /// main window's button.
        /// </summary>
        public void ToggleActive()
        {
            if (active == null)
                return;

            active.StartStop();

            if (active.WatchTimer.Running)
                lastStarted = active;

            Refresh();
        }
        #endregion


        #region private methods
        /// <summary>
        /// The active issue is the one whose timer is running; the last one to
        /// have started when several are; the last one that ran when none are;
        /// and the row selected in the main window when none ever ran.
        /// </summary>
        private ITimerSource Resolve(List<ITimerSource> sources, List<ITimerSource> running)
        {
            // A running timer always wins. Preferring the one we were told
            // started last covers the multiple-timers case; falling back to any
            // running one covers a timer restored from saved state at startup,
            // which never raised TimerStarted.
            if (running.Count > 0)
                return running.Contains(lastStarted) ? lastStarted : running[0];

            // Nothing running: stay on the last one that ran, as long as it is
            // still one of the rows on screen.
            if (lastStarted != null && sources.Contains(lastStarted))
                return lastStarted;

            return sources.FirstOrDefault(s => s.IsCurrent) ?? sources.FirstOrDefault();
        }


        private void SetActive(ITimerSource resolved)
        {
            if (ReferenceEquals(active, resolved))
                return;

            active = resolved;
            Raise("ActiveSource");
        }


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
        private readonly Func<IEnumerable<ITimerSource>> sourcesProvider;

        private ITimerSource lastStarted;
        private ITimerSource active;
        private List<ITimerSource> running = new List<ITimerSource>();

        private string issueKey = "";
        private string summary = "";
        private TimeSpan elapsed;
        private bool isRunning;
        private bool hasOtherRunningTimers;
        #endregion
    }
}
