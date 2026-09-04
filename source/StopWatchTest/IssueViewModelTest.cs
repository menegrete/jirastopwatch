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
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using StopWatch;


    [TestFixture]
    public class IssueViewModelTest
    {
        private IssueViewModel model;
        private List<string> changed;

        [SetUp]
        public void Setup()
        {
            model = new IssueViewModel();
            changed = new List<string>();
            model.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
        }


        #region change notification
        [Test]
        public void IssueKey_NotifiesWhenItChanges()
        {
            model.IssueKey = "TST-1";

            Assert.That(model.IssueKey, Is.EqualTo("TST-1"));
            Assert.That(changed, Contains.Item("IssueKey"));
        }


        [Test]
        public void IssueKey_DoesNotNotifyWhenSetToTheSameValue()
        {
            model.IssueKey = "TST-1";
            changed.Clear();

            model.IssueKey = "TST-1";

            Assert.That(changed, Is.Empty);
        }


        [Test]
        public void IssueKey_TreatsNullAsEmpty()
        {
            model.IssueKey = null;

            Assert.That(model.IssueKey, Is.EqualTo(""));
        }


        [Test]
        public void Summary_NotifiesWhenItChanges()
        {
            model.Summary = "Do the thing";

            Assert.That(model.Summary, Is.EqualTo("Do the thing"));
            Assert.That(changed, Contains.Item("Summary"));
        }


        [Test]
        public void Comment_NotifiesBothItselfAndHasComment()
        {
            model.Comment = "worked on it";

            Assert.That(model.HasComment, Is.True);
            Assert.That(changed, Contains.Item("Comment"));
            Assert.That(changed, Contains.Item("HasComment"));
        }


        [Test]
        public void Comment_EmptyDoesNotCountAsHavingOne()
        {
            model.Comment = "";

            Assert.That(model.HasComment, Is.False);
        }


        [Test]
        public void EstimateUpdate_NotifiesWhenMethodAndValueChange()
        {
            model.EstimateUpdateMethod = EstimateUpdateMethods.Leave;
            model.EstimateUpdateValue = "2h";

            Assert.That(changed, Contains.Item("EstimateUpdateMethod"));
            Assert.That(changed, Contains.Item("EstimateUpdateValue"));
        }


        [Test]
        public void IsCurrent_NotifiesWhenItChanges()
        {
            model.IsCurrent = true;

            Assert.That(changed, Contains.Item("IsCurrent"));
        }


        [Test]
        public void CanOpen_IsFalseWhileThereIsNoKey()
        {
            Assert.That(model.CanOpen, Is.False);

            model.IssueKey = "   ";
            Assert.That(model.CanOpen, Is.False);

            model.IssueKey = "TST-1";
            Assert.That(model.CanOpen, Is.True);
        }
        #endregion


        #region the shown time follows the timer
        [Test]
        public void TimeElapsed_FollowsTheTimerOnRefresh()
        {
            model.WatchTimer.TimeElapsed = TimeSpan.FromMinutes(90);

            model.Refresh();

            Assert.That(model.TimeElapsed, Is.EqualTo(TimeSpan.FromMinutes(90)));
            Assert.That(model.TimeElapsedText, Is.EqualTo("1h 30m"));
        }


        [Test]
        public void TimeElapsed_NotifiesItselfAndItsTextWhenTheClockMoved()
        {
            model.WatchTimer.TimeElapsed = TimeSpan.FromMinutes(5);

            model.Refresh();

            Assert.That(changed, Contains.Item("TimeElapsed"));
            Assert.That(changed, Contains.Item("TimeElapsedText"));
        }


        [Test]
        public void Refresh_DoesNotNotifyWhenNothingMoved()
        {
            model.Refresh();

            Assert.That(changed, Is.Empty);
        }


        [Test]
        public void Start_MakesTheModelRunAndAnnouncesIt()
        {
            model.Start();

            Assert.That(model.IsRunning, Is.True);
            Assert.That(model.WatchTimer.Running, Is.True);
            Assert.That(changed, Contains.Item("IsRunning"));
        }


        [Test]
        public void Start_RaisesTimerStarted()
        {
            int started = 0;
            model.TimerStarted += (s, e) => started++;

            model.Start();

            Assert.That(started, Is.EqualTo(1));
        }


        [Test]
        public void Start_OnAnAlreadyRunningTimerDoesNothing()
        {
            model.Start();
            int started = 0;
            model.TimerStarted += (s, e) => started++;

            model.Start();

            Assert.That(started, Is.EqualTo(0));
        }


        [Test]
        public void Pause_StopsRunningAndAnnouncesIt()
        {
            model.Start();
            changed.Clear();

            model.Pause();

            Assert.That(model.IsRunning, Is.False);
            Assert.That(changed, Contains.Item("IsRunning"));
        }


        [Test]
        public void StartStop_TogglesRunning()
        {
            model.StartStop();
            Assert.That(model.IsRunning, Is.True);

            model.StartStop();
            Assert.That(model.IsRunning, Is.False);
        }


        [Test]
        public void CanReset_IsFalseOnlyWhileStoppedAtZero()
        {
            Assert.That(model.CanReset, Is.False);

            model.SetTimeElapsed(TimeSpan.FromMinutes(1));
            Assert.That(model.CanReset, Is.True);

            model.Reset();
            Assert.That(model.CanReset, Is.False);

            model.Start();
            Assert.That(model.CanReset, Is.True);
        }


        [Test]
        public void CanReset_IsAnnouncedWhenItFlips()
        {
            model.SetTimeElapsed(TimeSpan.FromMinutes(1));

            Assert.That(changed, Contains.Item("CanReset"));
        }


        [Test]
        public void CanPost_NeedsAtLeastAMinute()
        {
            Assert.That(model.CanPost, Is.False);

            model.SetTimeElapsed(TimeSpan.FromSeconds(30));
            Assert.That(model.CanPost, Is.True, "half a minute rounds up to one");

            model.Reset();
            Assert.That(model.CanPost, Is.False);
        }


        [Test]
        public void Reset_ClearsTheTimeAndAnnouncesIt()
        {
            model.SetTimeElapsed(TimeSpan.FromHours(2));
            changed.Clear();

            model.Reset();

            Assert.That(model.TimeElapsed, Is.EqualTo(TimeSpan.Zero));
            Assert.That(changed, Contains.Item("TimeElapsed"));
        }
        #endregion


        #region persistence
        [Test]
        public void Hydrate_RestoresKeyTimeAndWorklogState()
        {
            var persisted = new PersistedIssue
            {
                Key = "TST-7",
                TimerRunning = false,
                TotalTime = TimeSpan.FromMinutes(45),
                SessionStartTime = DateTime.Now,
                InitialStartTime = DateTimeOffset.UtcNow.AddHours(-1),
                Comment = "notes",
                EstimateUpdateMethod = EstimateUpdateMethods.SetTo,
                EstimateUpdateValue = "3h"
            };

            model.Hydrate(persisted, SaveTimerSetting.SaveRunActive);

            Assert.That(model.IssueKey, Is.EqualTo("TST-7"));
            Assert.That(model.TimeElapsed, Is.EqualTo(TimeSpan.FromMinutes(45)));
            Assert.That(model.Comment, Is.EqualTo("notes"));
            Assert.That(model.EstimateUpdateMethod, Is.EqualTo(EstimateUpdateMethods.SetTo));
            Assert.That(model.EstimateUpdateValue, Is.EqualTo("3h"));
        }


        [Test]
        public void Hydrate_WithNoSaveKeepsOnlyTheKey()
        {
            var persisted = new PersistedIssue
            {
                Key = "TST-7",
                TotalTime = TimeSpan.FromMinutes(45),
                Comment = "notes"
            };

            model.Hydrate(persisted, SaveTimerSetting.NoSave);

            Assert.That(model.IssueKey, Is.EqualTo("TST-7"));
            Assert.That(model.TimeElapsed, Is.EqualTo(TimeSpan.Zero));
            Assert.That(model.Comment, Is.Null);
        }


        [Test]
        public void Hydrate_WithSavePauseRestoresTimeButNotRunning()
        {
            var persisted = new PersistedIssue
            {
                Key = "TST-7",
                TimerRunning = true,
                TotalTime = TimeSpan.FromMinutes(45),
                SessionStartTime = DateTime.Now
            };

            model.Hydrate(persisted, SaveTimerSetting.SavePause);

            Assert.That(model.IsRunning, Is.False);
            Assert.That(model.TimeElapsed, Is.EqualTo(TimeSpan.FromMinutes(45)));
        }


        [Test]
        public void Persist_RoundTripsThroughHydrate()
        {
            model.IssueKey = "TST-9";
            model.Comment = "a comment";
            model.EstimateUpdateMethod = EstimateUpdateMethods.ManualDecrease;
            model.EstimateUpdateValue = "30m";
            model.SetTimeElapsed(TimeSpan.FromMinutes(12));

            var restored = new IssueViewModel();
            restored.Hydrate(model.Persist(), SaveTimerSetting.SaveRunActive);

            Assert.That(restored.IssueKey, Is.EqualTo("TST-9"));
            Assert.That(restored.Comment, Is.EqualTo("a comment"));
            Assert.That(restored.EstimateUpdateMethod, Is.EqualTo(EstimateUpdateMethods.ManualDecrease));
            Assert.That(restored.EstimateUpdateValue, Is.EqualTo("30m"));
            Assert.That(restored.TimeElapsed, Is.EqualTo(TimeSpan.FromMinutes(12)));
        }


        [Test]
        public void Hydrate_IgnoresANullPersistedIssue()
        {
            model.IssueKey = "TST-1";

            model.Hydrate(null, SaveTimerSetting.SaveRunActive);

            Assert.That(model.IssueKey, Is.EqualTo("TST-1"));
        }
        #endregion
    }
}
