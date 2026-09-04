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
    using System.Linq;
    using System.Threading.Tasks;
    using Moq;
    using NUnit.Framework;
    using StopWatch;


    [TestFixture]
    public class FilterProviderTest
    {
        private Mock<IJiraOperations> jira;
        private Settings settings;
        private FilterProvider provider;

        [SetUp]
        public void Setup()
        {
            jira = new Mock<IJiraOperations>();
            jira.SetupGet(j => j.SessionValid).Returns(true);

            settings = new Settings();
            provider = new FilterProvider(jira.Object, settings);
        }


        private void JiraHasFilters(params Filter[] filters)
        {
            // A fresh list every call: the provider inserts into what it is
            // handed, and Jira would hand it a fresh list too.
            jira.Setup(j => j.GetFavoriteFilters()).Returns(() => filters.ToList());
        }


        #region loading
        [Test]
        public async Task Load_PutsMyOpenIssuesFirst()
        {
            JiraHasFilters(new Filter { Id = 7, Name = "Sprint", Jql = "project = TST" });

            await provider.LoadAsync();

            List<FilterItem> items = provider.Filters.ToList();
            Assert.That(items[0].Id, Is.EqualTo(FilterItem.MyOpenIssuesId));
            Assert.That(items[0].Name, Is.EqualTo("My open issues"));
            Assert.That(items[0].Jql, Does.Contain("currentUser()"));
        }


        [Test]
        public async Task Load_AddsASeparatorOnlyWhenThereIsSomethingBelowIt()
        {
            JiraHasFilters(new Filter { Id = 7, Name = "Sprint", Jql = "project = TST" });

            await provider.LoadAsync();

            List<FilterItem> items = provider.Filters.ToList();
            Assert.That(items.Count, Is.EqualTo(3));
            Assert.That(items[1].IsSeparator, Is.True);
            Assert.That(items[2].Id, Is.EqualTo(7));
        }


        [Test]
        public async Task Load_LeavesTheSeparatorOutWhenJiraHasNoFilters()
        {
            JiraHasFilters();

            await provider.LoadAsync();

            List<FilterItem> items = provider.Filters.ToList();
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Id, Is.EqualTo(FilterItem.MyOpenIssuesId));
        }


        [Test]
        public async Task Load_KeepsTheListItHadWhenTheLookupFails()
        {
            JiraHasFilters(new Filter { Id = 7, Name = "Sprint", Jql = "project = TST" });
            await provider.LoadAsync();

            jira.Setup(j => j.GetFavoriteFilters()).Returns((List<Filter>)null);
            await provider.LoadAsync();

            Assert.That(provider.Filters.Count(), Is.EqualTo(3));
        }


        [Test]
        public async Task Load_AnnouncesThatTheListChanged()
        {
            JiraHasFilters(new Filter { Id = 7, Name = "Sprint", Jql = "project = TST" });
            int loaded = 0;
            provider.FiltersLoaded += (s, e) => loaded++;

            await provider.LoadAsync();

            Assert.That(loaded, Is.EqualTo(1));
        }


        [Test]
        public async Task Load_DoesNotAnnounceAFailedLookup()
        {
            jira.Setup(j => j.GetFavoriteFilters()).Returns((List<Filter>)null);
            int loaded = 0;
            provider.FiltersLoaded += (s, e) => loaded++;

            await provider.LoadAsync();

            Assert.That(loaded, Is.EqualTo(0));
        }
        #endregion


        #region the active filter
        [Test]
        public async Task Load_RestoresTheFilterSavedFromTheLastRun()
        {
            settings.CurrentFilter = 7;
            JiraHasFilters(new Filter { Id = 7, Name = "Sprint", Jql = "project = TST" });

            await provider.LoadAsync();

            Assert.That(provider.Current, Is.Not.Null);
            Assert.That(provider.Current.Id, Is.EqualTo(7));
            Assert.That(provider.CurrentJql, Is.EqualTo("project = TST"));
        }


        [Test]
        public async Task Load_LeavesNothingSelectedWhenTheSavedFilterIsGone()
        {
            settings.CurrentFilter = 999;
            JiraHasFilters(new Filter { Id = 7, Name = "Sprint", Jql = "project = TST" });

            await provider.LoadAsync();

            Assert.That(provider.Current, Is.Null);
        }


        [Test]
        public void CurrentJql_IsEmptyWhileNoFilterIsSelected()
        {
            Assert.That(provider.Current, Is.Null);
            Assert.That(provider.CurrentJql, Is.EqualTo(""));
        }


        [Test]
        public void Current_SavesTheChoiceForTheNextRun()
        {
            provider.Current = new FilterItem(7, "Sprint", "project = TST");

            Assert.That(settings.CurrentFilter, Is.EqualTo(7));
            Assert.That(provider.CurrentJql, Is.EqualTo("project = TST"));
        }


        [Test]
        public void Current_IgnoresTheSeparator()
        {
            provider.Current = new FilterItem(7, "Sprint", "project = TST");

            provider.Current = new FilterItem(FilterItem.SeparatorId, "--------------", "");

            Assert.That(provider.Current.Id, Is.EqualTo(7), "picking the separator is not picking a filter");
            Assert.That(settings.CurrentFilter, Is.EqualTo(7));
        }


        [Test]
        public async Task Load_NeverRestoresTheSeparatorAsTheActiveFilter()
        {
            settings.CurrentFilter = FilterItem.SeparatorId;
            JiraHasFilters(new Filter { Id = 7, Name = "Sprint", Jql = "project = TST" });

            await provider.LoadAsync();

            Assert.That(provider.Current, Is.Null);
            Assert.That(provider.CurrentJql, Is.EqualTo(""));
        }


        [Test]
        public void CurrentJql_IsEmptyForAFilterWithoutOne()
        {
            provider.Current = new FilterItem(7, "Sprint", null);

            Assert.That(provider.CurrentJql, Is.EqualTo(""));
        }
        #endregion


        #region construction
        [Test]
        public void ItRefusesToBeBuiltWithoutItsCollaborators()
        {
            Assert.Throws<ArgumentNullException>(() => new FilterProvider(null, settings));
            Assert.Throws<ArgumentNullException>(() => new FilterProvider(jira.Object, null));
        }
        #endregion
    }
}
