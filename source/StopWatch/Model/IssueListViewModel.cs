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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace StopWatch
{
    /// <summary>
    /// The list of issue rows the main window shows.
    ///
    /// Replaces MainForm's InitializeIssueControls, which instantiated N
    /// controls and positioned them by hand at a fixed row height. The list is
    /// now a collection and the window is a view of it, which is what lets rows
    /// have the height their own content needs.
    /// </summary>
    internal class IssueListViewModel : INotifyPropertyChanged
    {
        #region public members
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Raised when any row's timer is started.</summary>
        public event EventHandler<IssueViewModel> TimerStarted;

        /// <summary>Raised when any row's timer is reset.</summary>
        public event EventHandler<IssueViewModel> TimerReset;

        /// <summary>Raised when the user asks to remove a row.</summary>
        public event EventHandler<IssueViewModel> RemoveRequested;

        public ObservableCollection<IssueViewModel> Issues { get; private set; }


        /// <summary>
        /// The selected row, or null while the list is empty. Exactly one row
        /// carries IsCurrent, which is what the row template highlights.
        /// </summary>
        public IssueViewModel Current
        {
            get { return currentIndex >= 0 && currentIndex < Issues.Count ? Issues[currentIndex] : null; }
        }


        public int CurrentIndex
        {
            get { return currentIndex; }
        }


        /// <summary>Sum of every row's elapsed time, as the bottom bar shows it.</summary>
        public TimeSpan TotalTime
        {
            get { return totalTime; }
        }


        public string TotalTimeText
        {
            get { return JiraTimeHelpers.TimeSpanToJiraTime(totalTime); }
        }


        /// <summary>
        /// Whether another row can be added. False once the list has reached
        /// the configured maximum.
        /// </summary>
        public bool CanAdd
        {
            get { return Issues.Count < settings.MaxIssues; }
        }


        /// <summary>
        /// Whether rows can be removed. A single remaining row cannot be
        /// removed, which is what disabled the button before.
        /// </summary>
        public bool CanRemove
        {
            get { return Issues.Count > 1; }
        }


        /// <summary>How much room each row takes. Persisted; see the issue-list spec.</summary>
        public ListDensity Density
        {
            get { return settings.ListDensity; }
            set
            {
                if (settings.ListDensity == value)
                    return;

                settings.ListDensity = value;
                Raise("Density");
            }
        }
        #endregion


        #region public methods
        public IssueListViewModel(Settings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.settings = settings;

            Issues = new ObservableCollection<IssueViewModel>();
            Issues.CollectionChanged += Issues_CollectionChanged;
        }


        /// <summary>
        /// Fills the list from what the last run saved, one row per persisted
        /// issue and at least one row always.
        /// </summary>
        public void Hydrate()
        {
            Issues.Clear();

            int count = Math.Max(1, Math.Min(settings.IssueCount, settings.MaxIssues));

            for (int i = 0; i < count; i++)
            {
                IssueViewModel issue = NewIssue();

                if (i < settings.PersistedIssues.Count)
                    issue.Hydrate(settings.PersistedIssues[i], settings.SaveTimerState);

                Issues.Add(issue);
            }

            SetCurrent(0);
            Refresh();
        }


        /// <summary>Captures the list for the next run.</summary>
        public void Persist()
        {
            settings.IssueCount = Issues.Count;

            settings.PersistedIssues.Clear();
            foreach (IssueViewModel issue in Issues)
                settings.PersistedIssues.Add(issue.Persist());
        }


        /// <summary>
        /// Adds a row, unless the list is already at the configured maximum.
        /// Returns the row that was added, or null when the limit stopped it.
        /// </summary>
        public IssueViewModel Add()
        {
            if (!CanAdd)
                return null;

            IssueViewModel issue = NewIssue();
            Issues.Add(issue);
            settings.IssueCount = Issues.Count;

            SetCurrent(Issues.Count - 1);
            return issue;
        }


        /// <summary>
        /// Removes a row, unless it is the last one left. Selection stays as
        /// close as it can to where it was.
        /// </summary>
        public void Remove(IssueViewModel issue)
        {
            if (issue == null || !CanRemove || !Issues.Contains(issue))
                return;

            int index = Issues.IndexOf(issue);

            issue.TimerStarted -= issue_TimerStarted;
            Issues.Remove(issue);
            settings.IssueCount = Issues.Count;

            SetCurrent(Math.Min(index, Issues.Count - 1));
            Refresh();
        }


        /// <summary>Moves a row to a different position, keeping it selected.</summary>
        public void Move(IssueViewModel issue, int newIndex)
        {
            if (issue == null || !Issues.Contains(issue))
                return;

            if (newIndex < 0 || newIndex >= Issues.Count)
                return;

            Issues.Move(Issues.IndexOf(issue), newIndex);
            SetCurrent(newIndex);
        }


        /// <summary>Selects the row above, or stays put at the top of the list.</summary>
        public void SelectPrevious()
        {
            if (currentIndex <= 0)
                return;

            SetCurrent(currentIndex - 1);
        }


        /// <summary>Selects the row below, or stays put at the bottom of the list.</summary>
        public void SelectNext()
        {
            if (currentIndex < 0 || currentIndex >= Issues.Count - 1)
                return;

            SetCurrent(currentIndex + 1);
        }


        public void SetCurrent(IssueViewModel issue)
        {
            if (issue == null || !Issues.Contains(issue))
                return;

            SetCurrent(Issues.IndexOf(issue));
        }


        public void SetCurrent(int index)
        {
            if (Issues.Count == 0)
            {
                currentIndex = -1;
                return;
            }

            currentIndex = Math.Max(0, Math.Min(index, Issues.Count - 1));

            for (int i = 0; i < Issues.Count; i++)
                Issues[i].IsCurrent = i == currentIndex;

            Raise("Current");
            Raise("CurrentIndex");
        }


        /// <summary>
        /// Re-reads every row's timer and the total. The window drives this on
        /// its tick, the same contract the other view models use.
        /// </summary>
        public void Refresh()
        {
            foreach (IssueViewModel issue in Issues)
                issue.Refresh();

            TimeSpan sum = TimeSpan.Zero;
            foreach (IssueViewModel issue in Issues)
                sum += issue.TimeElapsed;

            if (sum == totalTime)
                return;

            totalTime = sum;
            Raise("TotalTime");
            Raise("TotalTimeText");
        }


        /// <summary>
        /// Pauses every row but the one given. This is the single-timer rule,
        /// which used to live in MainForm's TimerStarted handler.
        /// </summary>
        public void PauseAllBut(IssueViewModel issue)
        {
            foreach (IssueViewModel other in Issues)
                if (other != issue)
                    other.Pause();
        }


        /// <summary>Every row whose timer is running, in the order shown.</summary>
        public IEnumerable<IssueViewModel> Running
        {
            get { return Issues.Where(i => i.WatchTimer.Running); }
        }
        #endregion


        #region private methods
        private IssueViewModel NewIssue()
        {
            IssueViewModel issue = new IssueViewModel();
            issue.TimerStarted += issue_TimerStarted;
            return issue;
        }


        private void issue_TimerStarted(object sender, EventArgs e)
        {
            EventHandler<IssueViewModel> handler = TimerStarted;
            if (handler != null)
                handler(this, (IssueViewModel)sender);
        }


        private void Issues_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Raise("CanAdd");
            Raise("CanRemove");
        }


        /// <summary>
        /// Reports a reset to whoever is listening. Rows do not raise this
        /// themselves, because a reset is driven from the window.
        /// </summary>
        public void NotifyReset(IssueViewModel issue)
        {
            Refresh();

            EventHandler<IssueViewModel> handler = TimerReset;
            if (handler != null)
                handler(this, issue);
        }


        /// <summary>Passes a row's remove request on to the window.</summary>
        public void RequestRemove(IssueViewModel issue)
        {
            EventHandler<IssueViewModel> handler = RemoveRequested;
            if (handler != null)
                handler(this, issue);
        }


        private void Raise(string property)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(property));
        }
        #endregion


        #region private members
        private readonly Settings settings;

        private int currentIndex = -1;
        private TimeSpan totalTime;
        #endregion
    }
}
