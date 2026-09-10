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
    using StopWatch;


    [TestFixture]
    public class StringHelpersTest
    {
        [Test]
        public void Truncate_CutsToTheGivenLength()
        {
            Assert.That(StringHelpers.Truncate("abcdefgh", 3), Is.EqualTo("abc"));
        }


        [Test]
        public void Truncate_LeavesAShorterStringAlone()
        {
            Assert.That(StringHelpers.Truncate("ab", 3), Is.EqualTo("ab"));
        }


        [Test]
        public void Truncate_TreatsNullAsEmpty()
        {
            // Regression test: a response body with no Content (an empty
            // response, or a connection error RestSharp still turns into a
            // response object) used to crash JiraApiRequester's logging
            // instead of just logging it as empty.
            Assert.That(StringHelpers.Truncate(null, 100), Is.EqualTo(""));
        }
    }
}
