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
    using System.Linq;
    using NUnit.Framework;
    using StopWatch;


    [TestFixture]
    public class MinimizeBehaviorTest
    {
        [TestCase(MinimizeBehavior.MiniView)]
        [TestCase(MinimizeBehavior.Tray)]
        [TestCase(MinimizeBehavior.TaskbarWidget)]
        public void Effective_OnWindows_KeepsTheStoredValue(MinimizeBehavior stored)
        {
            Assert.That(MinimizeBehaviors.Effective(stored, true), Is.EqualTo(stored));
        }


        [Test]
        public void Effective_OffWindows_TaskbarWidgetBehavesAsMiniView()
        {
            Assert.That(MinimizeBehaviors.Effective(MinimizeBehavior.TaskbarWidget, false), Is.EqualTo(MinimizeBehavior.MiniView));
        }


        [TestCase(MinimizeBehavior.MiniView)]
        [TestCase(MinimizeBehavior.Tray)]
        public void Effective_OffWindows_KeepsTheOtherValues(MinimizeBehavior stored)
        {
            Assert.That(MinimizeBehaviors.Effective(stored, false), Is.EqualTo(stored));
        }


        [Test]
        public void Available_OnWindows_OffersAllThree()
        {
            Assert.That(MinimizeBehaviors.Available(true).ToArray(),
                Is.EqualTo(new[] { MinimizeBehavior.MiniView, MinimizeBehavior.Tray, MinimizeBehavior.TaskbarWidget }));
        }


        [Test]
        public void Available_OffWindows_OffersMiniViewAndTrayOnly()
        {
            Assert.That(MinimizeBehaviors.Available(false).ToArray(),
                Is.EqualTo(new[] { MinimizeBehavior.MiniView, MinimizeBehavior.Tray }));
        }
    }
}
