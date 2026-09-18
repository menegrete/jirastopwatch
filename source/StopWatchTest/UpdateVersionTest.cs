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
    using NUnit.Framework;
    using StopWatch.Update;


    [TestFixture]
    public class UpdateVersionTest
    {
        [TestCase("v3.1.0", "3.1.0")]
        [TestCase("V3.1.0", "3.1.0")]
        [TestCase("3.1.0", "3.1.0")]
        [TestCase(" v3.1.0 ", "3.1.0")]
        public void TryParse_StripsALeadingVAndWhitespace(string text, string expected)
        {
            Assert.That(UpdateVersion.TryParse(text, out Version version), Is.True);
            Assert.That(version, Is.EqualTo(Version.Parse(expected)));
        }


        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-version")]
        [TestCase("vNext")]
        public void TryParse_RejectsMalformedInput(string text)
        {
            Assert.That(UpdateVersion.TryParse(text, out _), Is.False);
        }


        [Test]
        public void IsNewer_TrueWhenCandidateIsGreater()
        {
            Assert.That(UpdateVersion.IsNewer(new Version(3, 1, 0), new Version(3, 0, 0)), Is.True);
        }


        [Test]
        public void IsNewer_FalseWhenEqual()
        {
            Assert.That(UpdateVersion.IsNewer(new Version(3, 0, 0), new Version(3, 0, 0)), Is.False);
        }


        [Test]
        public void IsNewer_FalseWhenCandidateIsOlder()
        {
            Assert.That(UpdateVersion.IsNewer(new Version(2, 9, 0), new Version(3, 0, 0)), Is.False);
        }
    }
}
