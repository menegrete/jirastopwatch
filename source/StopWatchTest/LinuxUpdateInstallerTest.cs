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
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.Versioning;
    using System.Threading;
    using NUnit.Framework;
    using StopWatch.Update;


    [TestFixture]
    public class LinuxUpdateInstallerTest
    {
        [Test]
        public void BuildApplyScript_UsesLfLineEndingsAndAShebang()
        {
            string script = LinuxUpdateInstaller.BuildApplyScript(1234, "/staged/extracted", "/opt/jira");

            Assert.That(script, Does.StartWith("#!/bin/sh\n"));
            Assert.That(script, Does.Not.Contain("\r"));
            Assert.That(script, Does.Contain("PID=1234\n"));
        }


        [Test]
        public void BuildApplyScript_QuotesPathsWithSpacesAndApostrophes()
        {
            string script = LinuxUpdateInstaller.BuildApplyScript(1, "/home/ana maria/.local/share/staged dir/extracted", "/opt/Jira's apps/stop watch");

            Assert.That(script, Does.Contain("STAGED='/home/ana maria/.local/share/staged dir/extracted'\n"));
            Assert.That(script, Does.Contain("INSTALL='/opt/Jira'\\''s apps/stop watch'\n"));
        }


        [Test]
        public void BuildApplyScript_TrimsTrailingSeparators()
        {
            string script = LinuxUpdateInstaller.BuildApplyScript(1, "/staged/extracted/", "/opt/jira/");

            Assert.That(script, Does.Contain("STAGED='/staged/extracted'\n"));
            Assert.That(script, Does.Contain("INSTALL='/opt/jira'\n"));
        }


        [Test]
        public void BuildApplyScript_WaitsForThePidSwapsWithMvMakesTheExeExecutableRelaunchesAndDeletesItself()
        {
            string script = LinuxUpdateInstaller.BuildApplyScript(1, "/s", "/i");

            Assert.That(script, Does.Contain("kill -0 \"$PID\""));
            Assert.That(script, Does.Contain("-ge 30"));
            Assert.That(script.IndexOf("mv -f", StringComparison.Ordinal), Is.GreaterThan(script.IndexOf("kill -0", StringComparison.Ordinal)));
            Assert.That(script.IndexOf("chmod +x", StringComparison.Ordinal), Is.GreaterThan(script.IndexOf("mv -f", StringComparison.Ordinal)));
            Assert.That(script.IndexOf("nohup \"$INSTALL/$EXE\"", StringComparison.Ordinal), Is.GreaterThan(script.IndexOf("chmod +x", StringComparison.Ordinal)));
            Assert.That(script, Does.Contain("rm -f -- \"$0\""));
        }


        [Test]
        public void BuildApplyScript_NeverRemovesAnythingFromTheInstallDirectoryExceptItsOwnTempFile()
        {
            string script = LinuxUpdateInstaller.BuildApplyScript(1, "/s", "/i");

            foreach (string line in script.Split('\n'))
            {
                if (line.Contains("rm "))
                    Assert.That(line, Does.Contain("\"$TMP\"").Or.Contain("\"$0\""), line);
            }
            Assert.That(script, Does.Not.Contain("plugins"));
        }


        [TestCase("/plain", "'/plain'")]
        [TestCase("/with space", "'/with space'")]
        [TestCase("/it's", "'/it'\\''s'")]
        [TestCase("/$HOME/`id`", "'/$HOME/`id`'")]
        public void Quote_SingleQuotesAnythingSoTheShellTakesItLiterally(string value, string expected)
        {
            Assert.That(LinuxUpdateInstaller.Quote(value), Is.EqualTo(expected));
        }


        [Test]
        public void BuildLaunchInfo_RunsTheScriptDetachedThroughBinSh()
        {
            ProcessStartInfo info = LinuxUpdateInstaller.BuildLaunchInfo("/tmp/a b/StopWatch-update-1.sh");

            Assert.That(info.FileName, Is.EqualTo("/bin/sh"));
            Assert.That(info.UseShellExecute, Is.False);
            Assert.That(info.ArgumentList[1], Does.Contain("setsid -f").And.Contain("nohup"));
            // The script path is a positional argument ($0), never spliced into the command text.
            Assert.That(info.ArgumentList[2], Is.EqualTo("/tmp/a b/StopWatch-update-1.sh"));
        }


        [Test]
        public void IsWritable_IsTrueForAWritableDirectoryAndFalseForAMissingOne()
        {
            string dir = NewTempDir();
            try
            {
                Assert.That(UpdateApplier.IsWritable(dir), Is.True);
                Assert.That(Directory.GetFileSystemEntries(dir), Is.Empty, "the probe file must not be left behind");
                Assert.That(UpdateApplier.IsWritable(Path.Combine(dir, "missing")), Is.False);
            }
            finally { Directory.Delete(dir, recursive: true); }
        }


        [Test]
        [Platform("Linux")]
        [SupportedOSPlatform("linux")]
        public void ApplyOnExit_LeavesAnUnwritableInstallDirectoryUntouched()
        {
            if (Environment.UserName == "root")
                Assert.Ignore("root can write anywhere, so a directory cannot be made unwritable.");

            string install = NewTempDir();
            string staged = NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(install, "StopWatch"), "old");
                File.WriteAllText(Path.Combine(staged, "StopWatch"), "new");
                File.SetUnixFileMode(install, UnixFileMode.UserRead | UnixFileMode.UserExecute);

                new LinuxUpdateInstaller().ApplyOnExit(
                    new PendingUpdate("4.0.0", staged, isSelfContained: true, "https://example.com"),
                    install);
                Thread.Sleep(1500);

                Assert.That(File.ReadAllText(Path.Combine(install, "StopWatch")), Is.EqualTo("old"));
                Assert.That(Directory.GetFileSystemEntries(install), Has.Length.EqualTo(1));
            }
            finally
            {
                File.SetUnixFileMode(install, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                Directory.Delete(install, recursive: true);
                Directory.Delete(staged, recursive: true);
            }
        }


        // Runs the generated script for real (the PID it waits for is already
        // gone): the installed binary is replaced and made executable, the app
        // is relaunched, plugins and unrelated files stay, and the script and
        // its temp copies are gone.
        [Test]
        [Platform("Linux")]
        [SupportedOSPlatform("linux")]
        public void BuildApplyScript_WhenRun_ReplacesTheExeRelaunchesAndLeavesPluginsAlone()
        {
            string root = NewTempDir();
            try
            {
                string install = Path.Combine(root, "in stall's dir");
                string staged = Path.Combine(root, "staged dir", "extracted");
                string marker = Path.Combine(root, "relaunched.txt");
                Directory.CreateDirectory(Path.Combine(install, "plugins"));
                Directory.CreateDirectory(staged);

                File.WriteAllText(Path.Combine(install, "StopWatch"), "#!/bin/sh\necho old\n");
                File.SetUnixFileMode(Path.Combine(install, "StopWatch"), (UnixFileMode)Convert.ToInt32("755", 8));
                File.WriteAllText(Path.Combine(install, "plugins", "keep.dll"), "plugin");
                File.WriteAllText(Path.Combine(install, "unrelated.txt"), "keep me");

                string newExe = Path.Combine(staged, "StopWatch");
                File.WriteAllText(newExe, $"#!/bin/sh\necho relaunched > '{marker}'\n");
                File.SetUnixFileMode(newExe, UnixFileMode.UserRead | UnixFileMode.UserWrite); // chmod +x must restore it
                File.WriteAllText(Path.Combine(staged, "libextra.so"), "lib");

                int deadPid;
                using (Process p = Process.Start("/bin/sh", new[] { "-c", "true" }))
                {
                    deadPid = p.Id;
                    p.WaitForExit();
                }

                string scriptPath = Path.Combine(root, "update.sh");
                File.WriteAllText(scriptPath, LinuxUpdateInstaller.BuildApplyScript(deadPid, staged + "/", install + "/"));

                using (Process script = Process.Start("/bin/sh", new[] { scriptPath }))
                    Assert.That(script.WaitForExit(20000), Is.True);

                for (int i = 0; i < 50 && !File.Exists(marker); i++)
                    Thread.Sleep(100);

                Assert.That(File.Exists(marker), Is.True, "the new build was relaunched");
                Assert.That(File.ReadAllText(Path.Combine(install, "StopWatch")), Does.Contain("relaunched"));
                Assert.That(File.GetUnixFileMode(Path.Combine(install, "StopWatch")).HasFlag(UnixFileMode.UserExecute), Is.True);
                Assert.That(File.ReadAllText(Path.Combine(install, "libextra.so")), Is.EqualTo("lib"));
                Assert.That(File.ReadAllText(Path.Combine(install, "plugins", "keep.dll")), Is.EqualTo("plugin"));
                Assert.That(File.ReadAllText(Path.Combine(install, "unrelated.txt")), Is.EqualTo("keep me"));
                Assert.That(Directory.GetFiles(install, ".*"), Is.Empty, "no temp copies left");
                Assert.That(File.Exists(scriptPath), Is.False, "the script deleted itself");
            }
            finally { Directory.Delete(root, recursive: true); }
        }


        private static string NewTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"StopWatchTest-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
