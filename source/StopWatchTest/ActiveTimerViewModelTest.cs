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

namespace StopWatchTest
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using StopWatch;


    [TestFixture]
    public class ActiveTimerViewModelTest
    {
        /// <summary>
        /// Stand-in for an IssueControl row. Only the four members the model
        /// reads, plus a real WatchTimer so that running state and elapsed time
        /// behave exactly as they do in the application.
        /// </summary>
        private class FakeSource : ITimerSource
        {
            public FakeSource(string key, string summary)
            {
                IssueKey = key;
                Summary = summary;
                WatchTimer = new WatchTimer();
            }

            public string IssueKey { get; private set; }

            public string Summary { get; private set; }

            public bool IsCurrent { get; set; }

            public WatchTimer WatchTimer { get; private set; }

            public int StartStopCalls { get; private set; }

            public void StartStop()
            {
                StartStopCalls++;

                if (WatchTimer.Running)
                    WatchTimer.Pause();
                else
                    WatchTimer.Start();
            }
        }


        private List<FakeSource> sources;
        private ActiveTimerViewModel viewModel;


        [SetUp]
        public void Setup()
        {
            sources = new List<FakeSource>();
            viewModel = new ActiveTimerViewModel(() => sources.Cast<ITimerSource>());
        }


        #region resolving the active issue
        [Test]
        public void ActiveSource_is_the_running_issue()
        {
            FakeSource idle = Add("ABC-1", "idle one");
            FakeSource running = Add("ABC-2", "running one");
            idle.IsCurrent = true;

            running.WatchTimer.Start();
            viewModel.Refresh();

            Assert.That(viewModel.IssueKey, Is.EqualTo("ABC-2"));
            Assert.That(viewModel.Summary, Is.EqualTo("running one"));
            Assert.That(viewModel.IsRunning, Is.True);
        }


        [Test]
        public void ActiveSource_is_the_last_started_when_several_are_running()
        {
            FakeSource first = Add("ABC-1", "first");
            FakeSource second = Add("ABC-2", "second");

            first.WatchTimer.Start();
            viewModel.NotifyTimerStarted(first);

            second.WatchTimer.Start();
            viewModel.NotifyTimerStarted(second);

            Assert.That(viewModel.IssueKey, Is.EqualTo("ABC-2"));
        }


        [Test]
        public void ActiveSource_is_the_last_one_that_ran_when_none_are_running()
        {
            FakeSource current = Add("ABC-1", "selected");
            FakeSource ran = Add("ABC-2", "ran earlier");
            current.IsCurrent = true;

            ran.WatchTimer.Start();
            viewModel.NotifyTimerStarted(ran);
            ran.WatchTimer.Pause();
            viewModel.Refresh();

            Assert.That(viewModel.IssueKey, Is.EqualTo("ABC-2"));
            Assert.That(viewModel.IsRunning, Is.False);
        }


        [Test]
        public void ActiveSource_is_the_selected_issue_when_none_ever_ran()
        {
            Add("ABC-1", "first");
            FakeSource current = Add("ABC-2", "selected");
            current.IsCurrent = true;

            viewModel.Refresh();

            Assert.That(viewModel.IssueKey, Is.EqualTo("ABC-2"));
            Assert.That(viewModel.IsRunning, Is.False);
            Assert.That(viewModel.Elapsed, Is.EqualTo(System.TimeSpan.Zero));
        }


        [Test]
        public void ActiveSource_falls_back_to_any_running_issue_when_no_start_was_reported()
        {
            // A timer restored from saved state at startup never raises
            // TimerStarted, so the model has no last-started to prefer.
            FakeSource restored = Add("ABC-1", "restored running");
            restored.WatchTimer.Start();

            viewModel.Refresh();

            Assert.That(viewModel.IssueKey, Is.EqualTo("ABC-1"));
            Assert.That(viewModel.IsRunning, Is.True);
        }


        [Test]
        public void ActiveSource_is_null_when_there_are_no_issues()
        {
            viewModel.Refresh();

            Assert.That(viewModel.ActiveSource, Is.Null);
            Assert.That(viewModel.IssueKey, Is.EqualTo(""));
            Assert.That(viewModel.Summary, Is.EqualTo(""));
            Assert.That(viewModel.IsRunning, Is.False);
        }


        [Test]
        public void Last_started_is_dropped_once_its_row_is_gone()
        {
            FakeSource removed = Add("ABC-1", "removed later");
            FakeSource remaining = Add("ABC-2", "still here");
            remaining.IsCurrent = true;

            removed.WatchTimer.Start();
            viewModel.NotifyTimerStarted(removed);
            removed.WatchTimer.Pause();

            sources.Remove(removed);
            viewModel.Refresh();

            Assert.That(viewModel.IssueKey, Is.EqualTo("ABC-2"));
        }
        #endregion


        #region other running timers
        [Test]
        public void HasOtherRunningTimers_is_false_with_a_single_running_timer()
        {
            FakeSource only = Add("ABC-1", "one");
            Add("ABC-2", "two");

            only.WatchTimer.Start();
            viewModel.Refresh();

            Assert.That(viewModel.HasOtherRunningTimers, Is.False);
            Assert.That(viewModel.RunningSources.Count(), Is.EqualTo(1));
        }


        [Test]
        public void HasOtherRunningTimers_is_true_with_two_running_timers()
        {
            FakeSource first = Add("ABC-1", "one");
            FakeSource second = Add("ABC-2", "two");

            first.WatchTimer.Start();
            second.WatchTimer.Start();
            viewModel.Refresh();

            Assert.That(viewModel.HasOtherRunningTimers, Is.True);
            Assert.That(viewModel.RunningSources.Count(), Is.EqualTo(2));
        }


        [Test]
        public void HasOtherRunningTimers_goes_back_to_false_when_the_second_is_paused()
        {
            FakeSource first = Add("ABC-1", "one");
            FakeSource second = Add("ABC-2", "two");

            first.WatchTimer.Start();
            second.WatchTimer.Start();
            viewModel.Refresh();

            second.WatchTimer.Pause();
            viewModel.Refresh();

            Assert.That(viewModel.HasOtherRunningTimers, Is.False);
        }


        [Test]
        public void RunningSources_is_empty_when_nothing_runs()
        {
            Add("ABC-1", "one");
            viewModel.Refresh();

            Assert.That(viewModel.RunningSources, Is.Empty);
        }
        #endregion


        #region toggling
        [Test]
        public void ToggleActive_pauses_the_running_issue()
        {
            FakeSource running = Add("ABC-1", "one");
            running.WatchTimer.Start();
            viewModel.NotifyTimerStarted(running);

            viewModel.ToggleActive();

            Assert.That(running.StartStopCalls, Is.EqualTo(1));
            Assert.That(running.WatchTimer.Running, Is.False);
            Assert.That(viewModel.IsRunning, Is.False);
        }


        [Test]
        public void ToggleActive_resumes_the_paused_issue()
        {
            FakeSource idle = Add("ABC-1", "one");
            idle.IsCurrent = true;
            viewModel.Refresh();

            viewModel.ToggleActive();

            Assert.That(idle.WatchTimer.Running, Is.True);
            Assert.That(viewModel.IsRunning, Is.True);
        }


        [Test]
        public void ToggleActive_does_nothing_without_an_active_issue()
        {
            viewModel.Refresh();

            Assert.DoesNotThrow(() => viewModel.ToggleActive());
        }
        #endregion


        #region change notification
        [Test]
        public void Refresh_raises_change_notifications_for_what_changed()
        {
            FakeSource source = Add("ABC-1", "one");
            source.IsCurrent = true;
            viewModel.Refresh();

            List<string> changed = new List<string>();
            viewModel.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            source.WatchTimer.Start();
            viewModel.Refresh();

            Assert.That(changed, Contains.Item("IsRunning"));
        }


        [Test]
        public void Refresh_is_silent_when_nothing_changed()
        {
            FakeSource source = Add("ABC-1", "one");
            source.IsCurrent = true;
            viewModel.Refresh();

            List<string> changed = new List<string>();
            viewModel.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            viewModel.Refresh();

            Assert.That(changed, Is.Empty);
        }
        #endregion


        private FakeSource Add(string key, string summary)
        {
            FakeSource source = new FakeSource(key, summary);
            sources.Add(source);
            return source;
        }
    }
}
