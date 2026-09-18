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
    public class UpdateApplierTest
    {
        // Regression test: AppContext.BaseDirectory (what MainWindow passes
        // as installDir) always ends in a trailing separator. A trailing
        // backslash right before a closing double-quote in a batch script
        // escapes that quote instead of closing it, which silently corrupts
        // everything after it on the line - manual end-to-end testing hit
        // this for real: robocopy's arguments got swallowed, so the
        // framework-dependent variant never actually applied the update and
        // just kept relaunching the old build.
        [Test]
        public void BuildApplyScript_TrailingSeparatorNeverEscapesAClosingQuote()
        {
            string script = UpdateApplier.BuildApplyScript(
                processId: 1234,
                stagedPath: @"C:\staged\extracted",
                isSelfContained: false,
                installDir: @"C:\install\dir\",
                installExePath: @"C:\install\dir\StopWatch.exe");

            // A `\"` two-character sequence right before what should be a
            // closing quote is what cmd.exe reads as an escaped, still-open
            // quote rather than a closed argument - corrupting everything
            // after it on that line. A well-formed script (paths trimmed of
            // their trailing separator) never contains this sequence.
            Assert.That(script, Does.Not.Contain("\\\""));

            Assert.That(script, Does.Contain(@"robocopy ""C:\staged\extracted"" ""C:\install\dir"""));
        }


        [Test]
        public void BuildApplyScript_SelfContainedSwapsTheExeDirectly()
        {
            string script = UpdateApplier.BuildApplyScript(
                processId: 1234,
                stagedPath: @"C:\staged\StopWatch.exe",
                isSelfContained: true,
                installDir: @"C:\install\dir",
                installExePath: @"C:\install\dir\StopWatch.exe");

            Assert.That(script, Does.Contain(@"move /y ""C:\staged\StopWatch.exe"" ""C:\install\dir\StopWatch.exe"""));
        }


        [TestCase(@"C:\install\dir\", @"C:\install\dir")]
        [TestCase(@"C:\install\dir/", @"C:\install\dir")]
        [TestCase(@"C:\install\dir", @"C:\install\dir")]
        public void TrimTrailingSeparator_RemovesATrailingSlashOfEitherKind(string path, string expected)
        {
            Assert.That(UpdateApplier.TrimTrailingSeparator(path), Is.EqualTo(expected));
        }
    }
}
