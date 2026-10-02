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
    using NUnit.Framework;
    using StopWatch.Update;


    [TestFixture]
    public class UpdateVersionTest
    {
        [TestCase("v3.1.0", "3.1.0")]
        [TestCase("V3.1.0", "3.1.0")]
        [TestCase("3.1.0", "3.1.0")]
        [TestCase(" v3.1.0 ", "3.1.0")]
        [TestCase("v3.8.0-rc.1", "3.8.0-rc.1")]
        [TestCase("3.8.0-rc.1", "3.8.0-rc.1")]
        public void TryParse_StripsALeadingVAndWhitespace(string text, string expected)
        {
            Assert.That(UpdateVersion.TryParse(text, out SemanticVersion version), Is.True);
            Assert.That(version.ToString(), Is.EqualTo(expected));
        }


        [TestCase("3.8.0+abc123", "3.8.0")]
        [TestCase("3.8.0-rc.1+abc123", "3.8.0-rc.1")]
        public void TryParse_IgnoresBuildMetadata(string text, string expected)
        {
            Assert.That(UpdateVersion.TryParse(text, out SemanticVersion version), Is.True);
            Assert.That(version.ToString(), Is.EqualTo(expected));
        }


        [Test]
        public void TryParse_ExposesThePrereleaseFlag()
        {
            UpdateVersion.TryParse("3.8.0-rc.1", out SemanticVersion beta);
            UpdateVersion.TryParse("3.8.0", out SemanticVersion stable);

            Assert.That(beta.IsPrerelease, Is.True);
            Assert.That(stable.IsPrerelease, Is.False);
        }


        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-version")]
        [TestCase("vNext")]
        [TestCase("3.8")]
        [TestCase("3.8.0.1")]
        [TestCase("3.x.0")]
        [TestCase("3.8.0-")]
        [TestCase("3.8.0-rc..1")]
        [TestCase("3.8.0-rc_1")]
        [TestCase("-3.8.0")]
        public void TryParse_RejectsMalformedInput(string text)
        {
            Assert.That(UpdateVersion.TryParse(text, out _), Is.False);
        }


        [TestCase("3.1.0", "3.0.0")]
        [TestCase("3.0.1", "3.0.0")]
        [TestCase("4.0.0", "3.9.9")]
        public void IsNewer_TrueWhenCandidateIsGreater(string candidate, string current)
        {
            Assert.That(IsNewer(candidate, current), Is.True);
        }


        [Test]
        public void IsNewer_FalseWhenEqual()
        {
            Assert.That(IsNewer("3.0.0", "3.0.0"), Is.False);
        }


        [Test]
        public void IsNewer_FalseWhenCandidateIsOlder()
        {
            Assert.That(IsNewer("2.9.0", "3.0.0"), Is.False);
        }


        [Test]
        public void IsNewer_FalseWhenEqualIgnoringBuildMetadata()
        {
            Assert.That(IsNewer("3.8.0-rc.1+abc", "3.8.0-rc.1"), Is.False);
        }


        [Test]
        public void IsNewer_StableBeatsItsOwnPrerelease()
        {
            Assert.That(IsNewer("3.8.0", "3.8.0-rc.3"), Is.True);
            Assert.That(IsNewer("3.8.0-rc.3", "3.8.0"), Is.False);
        }


        [Test]
        public void IsNewer_PrereleaseOfAHigherVersionBeatsALowerStable()
        {
            Assert.That(IsNewer("3.8.0-rc.1", "3.7.0"), Is.True);
        }


        [Test]
        public void IsNewer_NumericPrereleaseIdentifiersCompareAsNumbers()
        {
            Assert.That(IsNewer("3.8.0-rc.10", "3.8.0-rc.2"), Is.True);
            Assert.That(IsNewer("3.8.0-rc.2", "3.8.0-rc.10"), Is.False);
        }


        [Test]
        public void IsNewer_AlphanumericPrereleaseIdentifiersCompareAsText()
        {
            Assert.That(IsNewer("3.8.0-rc.1", "3.8.0-beta.1"), Is.True);
            Assert.That(IsNewer("3.8.0-beta.1", "3.8.0-rc.1"), Is.False);
        }


        [Test]
        public void IsNewer_NumericIdentifierIsLowerThanAlphanumeric()
        {
            Assert.That(IsNewer("3.8.0-rc", "3.8.0-1"), Is.True);
        }


        [Test]
        public void IsNewer_LongerPrereleaseWinsWhenSharedIdentifiersMatch()
        {
            Assert.That(IsNewer("3.8.0-rc.1.1", "3.8.0-rc.1"), Is.True);
        }


        [Test]
        public void IsNewer_HugeNumericIdentifierDoesNotOverflow()
        {
            Assert.That(IsNewer("3.8.0-rc.99999999999999999999", "3.8.0-rc.9"), Is.True);
        }


        [Test]
        public void IsNewer_FalseWhenEitherSideIsNull()
        {
            UpdateVersion.TryParse("3.0.0", out SemanticVersion version);

            Assert.That(UpdateVersion.IsNewer(null, version), Is.False);
            Assert.That(UpdateVersion.IsNewer(version, null), Is.False);
        }


        private static bool IsNewer(string candidate, string current)
        {
            Assert.That(UpdateVersion.TryParse(candidate, out SemanticVersion c), Is.True, candidate);
            Assert.That(UpdateVersion.TryParse(current, out SemanticVersion k), Is.True, current);
            return UpdateVersion.IsNewer(c, k);
        }
    }
}
