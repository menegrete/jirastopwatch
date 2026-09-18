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
using System.ComponentModel;

namespace StopWatch
{
    /// <summary>
    /// One row of the mini timer view: a single <see cref="ITimerSource"/>
    /// presented the way the mini view's list needs it. Exists so that the
    /// mini view can list several timers at once instead of the single row
    /// <see cref="ActiveTimerViewModel"/> resolves - that model still decides
    /// which sources are worth showing (see its RunningSources/ActiveSource),
    /// this class only wraps one of them for display.
    /// </summary>
    internal class MiniTimerRowViewModel : INotifyPropertyChanged
    {
        #region public members
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The source this row wraps, exposed so callers can match rows back to sources.</summary>
        public ITimerSource Source
        {
            get { return source; }
        }

        public string IssueKey
        {
            get { return source.IssueKey ?? ""; }
        }

        public string Summary
        {
            get { return source.Summary ?? ""; }
        }

        public bool IsRunning
        {
            get { return source.WatchTimer.Running; }
        }

        /// <summary>Elapsed time as a running clock, with seconds.</summary>
        public string ElapsedText
        {
            get { return JiraTimeHelpers.TimeSpanToClockTime(source.WatchTimer.TimeElapsed); }
        }
        #endregion


        #region public methods
        public MiniTimerRowViewModel(ITimerSource source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            this.source = source;
        }


        /// <summary>Pauses or resumes this row's timer, with the same effect as the main window's button.</summary>
        public void ToggleActive()
        {
            source.StartStop();
        }


        /// <summary>Re-reads the wrapped timer and raises change notifications. Called once a second while the mini view is visible.</summary>
        public void Refresh()
        {
            Raise("IsRunning");
            Raise("ElapsedText");

            // Summary starts empty until Jira resolves it, and this row keeps
            // wrapping the same source across ticks (and across hiding and
            // showing the mini view again) rather than being recreated - so
            // without this, a row built before the summary resolved would
            // never notice it arrive, leaving the open-in-browser button
            // permanently disabled.
            Raise("Summary");
        }
        #endregion


        #region private methods
        private void Raise(string property)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(property));
        }
        #endregion


        #region private members
        private readonly ITimerSource source;
        #endregion
    }
}
