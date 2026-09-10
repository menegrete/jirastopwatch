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


        #region construction
        [Test]
        public void ItRefusesToBeBuiltWithoutSettings()
        {
            Assert.Throws<System.ArgumentNullException>(() => new IssueListViewModel(null));
        }
        #endregion
    }
}
