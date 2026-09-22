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
    public class IssueListViewModelTest
    {
        private Settings settings;
        private IssueListViewModel list;
        private List<string> changed;

        [SetUp]
        public void Setup()
        {
            JiraTimeHelpers.TimeDisplayFormat = TimeDisplayFormat.Jira;
            settings = new Settings();
            list = new IssueListViewModel(settings);
            changed = new List<string>();
            list.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
        }


        #region density
        [Test]
        public void Density_SettingItNotifiesAndPersists()
        {
            list.Density = ListDensity.Spacious;

            Assert.That(list.Density, Is.EqualTo(ListDensity.Spacious));
            Assert.That(settings.ListDensity, Is.EqualTo(ListDensity.Spacious));
            Assert.That(changed, Contains.Item("Density"));
        }


        [Test]
        public void Density_SettingTheSameValueDoesNotNotify()
        {
            list.Density = ListDensity.Compact;
            changed.Clear();

            list.Density = ListDensity.Compact;

            Assert.That(changed, Is.Empty);
        }


        [Test]
        public void NotifyDensityChanged_AnnouncesADensityWrittenDirectlyToSettings()
        {
            // The settings dialog writes Settings.ListDensity directly rather
            // than going through the Density property above, which is exactly
            // the case this method exists for: nothing else would have raised
            // the notification the row templates rely on to switch.
            settings.ListDensity = ListDensity.Spacious;

            list.NotifyDensityChanged();

            Assert.That(changed, Contains.Item("Density"));
            Assert.That(list.Density, Is.EqualTo(ListDensity.Spacious));
        }
        #endregion


        #region time display format
        [Test]
        public void TotalTimeText_FollowsTheConfiguredDisplayFormat()
        {
            settings.MaxIssues = 10;
            IssueViewModel issue = list.Add();
            issue.WatchTimer.TimeElapsed = TimeSpan.FromMinutes(90);
            list.Refresh();

            JiraTimeHelpers.TimeDisplayFormat = TimeDisplayFormat.Clock;

            Assert.That(list.TotalTimeText, Is.EqualTo("1:30"));
        }


        [Test]
        public void NotifyTimeDisplayFormatChanged_AnnouncesTheTotalAndEveryRow()
        {
            JiraTimeHelpers.TimeDisplayFormat = TimeDisplayFormat.Clock;

            list.NotifyTimeDisplayFormatChanged();

            Assert.That(changed, Contains.Item("TotalTimeText"));
        }
        #endregion


        #region construction
        [Test]
        public void ItRefusesToBeBuiltWithoutSettings()
        {
            Assert.Throws<System.ArgumentNullException>(() => new IssueListViewModel(null));
        }
        #endregion


        #region move
        [Test]
        public void Move_ReordersIssuesAndKeepsTheMovedRowSelected()
        {
            IssueViewModel a = AddIssue("A");
            IssueViewModel b = AddIssue("B");
            IssueViewModel c = AddIssue("C");

            list.Move(a, 2);

            Assert.That(list.Issues, Is.EqualTo(new[] { b, c, a }));
            Assert.That(list.Current, Is.EqualTo(a));
        }


        [Test]
        public void Move_IsANoOpPastEitherEndOfTheList()
        {
            IssueViewModel a = AddIssue("A");
            IssueViewModel b = AddIssue("B");
            IssueViewModel c = AddIssue("C");

            list.Move(a, -1);
            list.Move(c, 3);

            Assert.That(list.Issues, Is.EqualTo(new[] { a, b, c }));
        }


        [Test]
        public void MoveUpMoveDown_MoveTheGivenRowRegardlessOfSelection()
        {
            IssueViewModel a = AddIssue("A");
            IssueViewModel b = AddIssue("B");
            IssueViewModel c = AddIssue("C");
            list.SetCurrent(a);

            list.MoveDown(b);

            Assert.That(list.Issues, Is.EqualTo(new[] { a, c, b }));
        }


        [Test]
        public void MoveUp_OnTheFirstRowIsANoOp()
        {
            IssueViewModel a = AddIssue("A");
            IssueViewModel b = AddIssue("B");

            list.MoveUp(a);

            Assert.That(list.Issues, Is.EqualTo(new[] { a, b }));
        }


        [Test]
        public void MoveDown_OnTheLastRowIsANoOp()
        {
            IssueViewModel a = AddIssue("A");
            IssueViewModel b = AddIssue("B");

            list.MoveDown(b);

            Assert.That(list.Issues, Is.EqualTo(new[] { a, b }));
        }
        #endregion


        #region IsFirst / IsLast
        [Test]
        public void IsFirstIsLast_AreCorrectAfterHydrate()
        {
            settings.IssueCount = 3;
            settings.MaxIssues = 10;

            list.Hydrate();

            AssertFirstAndLast(0, 2);
        }


        [Test]
        public void IsFirstIsLast_AreCorrectAfterAdd()
        {
            settings.MaxIssues = 10;
            AddIssue("A");
            AddIssue("B");

            IssueViewModel c = list.Add();

            Assert.That(c.IsLast, Is.True);
            AssertFirstAndLast(0, 2);
        }


        [Test]
        public void IsFirstIsLast_AreCorrectAfterRemove()
        {
            settings.MaxIssues = 10;
            IssueViewModel a = AddIssue("A");
            AddIssue("B");
            IssueViewModel c = AddIssue("C");

            list.Remove(c);

            AssertFirstAndLast(0, 1);
            Assert.That(a.IsFirst, Is.True);
        }


        [Test]
        public void IsFirstIsLast_AreCorrectAfterMove()
        {
            settings.MaxIssues = 10;
            IssueViewModel a = AddIssue("A");
            IssueViewModel b = AddIssue("B");
            IssueViewModel c = AddIssue("C");

            list.Move(a, 2);

            Assert.That(a.IsFirst, Is.False);
            Assert.That(a.IsLast, Is.True);
            Assert.That(b.IsFirst, Is.True);
            Assert.That(c.IsLast, Is.False);
        }


        private void AssertFirstAndLast(int firstIndex, int lastIndex)
        {
            for (int i = 0; i < list.Issues.Count; i++)
            {
                Assert.That(list.Issues[i].IsFirst, Is.EqualTo(i == firstIndex), "IsFirst at index " + i);
                Assert.That(list.Issues[i].IsLast, Is.EqualTo(i == lastIndex), "IsLast at index " + i);
            }
        }
        #endregion


        private IssueViewModel AddIssue(string key)
        {
            settings.MaxIssues = System.Math.Max(settings.MaxIssues, list.Issues.Count + 1);
            IssueViewModel issue = list.Add();
            issue.IssueKey = key;
            return issue;
        }
    }
}
