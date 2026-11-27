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

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using StopWatch.Logging;

namespace StopWatch.Update
{
    /// <summary>
    /// The Linux counterpart of <see cref="UpdateApplier"/>: writes an
    /// <c>sh</c> helper to the temp directory and launches it detached
    /// (<c>setsid</c>, falling back to <c>nohup</c>). The helper waits for
    /// this process to exit, copies each staged file next to its installed
    /// counterpart and <c>mv</c>s it over (atomic, being on the same
    /// filesystem), re-applies <c>chmod +x</c> to the executable, relaunches
    /// the app and deletes itself. Anything in the install directory that is
    /// not in the new build - installed plugins above all - is left alone.
    /// </summary>
    internal sealed class LinuxUpdateInstaller : IUpdateInstaller
    {
        public void ApplyOnExit(PendingUpdate update, string installDir)
        {
            if (update == null)
                return;

            // AppContext.BaseDirectory ends in a separator, which would leave
            // a double slash in every path derived from it.
            installDir = UpdateApplier.TrimTrailingSeparator(installDir);

            try
            {
                // Checked here, before anything is touched: an install the
                // user can't write to (a root-owned /opt, say) keeps the
                // current build and keeps starting normally.
                if (!UpdateApplier.IsWritable(installDir))
                {
                    Logger.Instance.Log($"Auto-update: install directory '{installDir}' is not writable, skipping apply");
                    return;
                }

                string scriptPath = Path.Combine(Path.GetTempPath(), $"StopWatch-update-{Guid.NewGuid():N}.sh");
                File.WriteAllText(scriptPath, BuildApplyScript(Environment.ProcessId, update.StagedPath, installDir));

                Process.Start(BuildLaunchInfo(scriptPath));
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Auto-update: failed to launch apply helper", ex);
            }
        }


        /// <summary>
        /// Runs the helper in its own session so it outlives this process,
        /// with no stdio tied to it. Internal for tests.
        /// </summary>
        internal static ProcessStartInfo BuildLaunchInfo(string scriptPath)
        {
            ProcessStartInfo info = new ProcessStartInfo("/bin/sh")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add("setsid -f /bin/sh \"$0\" </dev/null >/dev/null 2>&1 || nohup /bin/sh \"$0\" </dev/null >/dev/null 2>&1 &");
            info.ArgumentList.Add(scriptPath);
            return info;
        }


        /// <summary>
        /// Builds the helper script (LF line endings). Internal so it is
        /// testable without touching the filesystem or a process.
        /// </summary>
        internal static string BuildApplyScript(int processId, string stagedDir, string installDir)
        {
            stagedDir = UpdateApplier.TrimTrailingSeparator(stagedDir);
            installDir = UpdateApplier.TrimTrailingSeparator(installDir);

            // Giving up after MaxWaitSeconds leaves the install untouched and
            // does not relaunch (the app is presumably still running), the
            // same as the Windows helper.
            return string.Join("\n",
                "#!/bin/sh",
                $"PID={processId.ToString(CultureInfo.InvariantCulture)}",
                $"STAGED={Quote(stagedDir)}",
                $"INSTALL={Quote(installDir)}",
                $"EXE={Quote(AutoUpdateService.LinuxExecutableName)}",
                "COUNT=0",
                "while kill -0 \"$PID\" 2>/dev/null; do",
                "  COUNT=$((COUNT + 1))",
                $"  if [ \"$COUNT\" -ge {MaxWaitSeconds} ]; then",
                "    rm -f -- \"$0\"",
                "    exit 0",
                "  fi",
                "  sleep 1",
                "done",
                "for SRC in \"$STAGED\"/*; do",
                "  [ -f \"$SRC\" ] || continue",
                "  NAME=${SRC##*/}",
                "  TMP=\"$INSTALL/.$NAME.update-new\"",
                "  if cp -p -- \"$SRC\" \"$TMP\" && mv -f -- \"$TMP\" \"$INSTALL/$NAME\"; then",
                "    :",
                "  else",
                "    rm -f -- \"$TMP\"",
                "  fi",
                "done",
                "chmod +x -- \"$INSTALL/$EXE\"",
                "nohup \"$INSTALL/$EXE\" </dev/null >/dev/null 2>&1 &",
                "rm -f -- \"$0\"",
                "");
        }


        /// <summary>
        /// POSIX single-quoting: safe for spaces, <c>$</c>, quotes and
        /// anything else in a path. Internal for tests.
        /// </summary>
        internal static string Quote(string value)
        {
            return "'" + value.Replace("'", "'\\''") + "'";
        }


        /// <summary>How long the helper waits for the app to exit, as on Windows.</summary>
        private const int MaxWaitSeconds = 30;
    }
}
