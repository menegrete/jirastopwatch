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
    /// Applies a <see cref="PendingUpdate"/> on the app's way out. The
    /// running process can't overwrite or delete its own .exe on Windows, so
    /// this writes a small self-deleting .cmd helper to %TEMP% and launches
    /// it detached: it waits for this process to actually exit, swaps the
    /// staged build into place, relaunches the app, then deletes itself. See
    /// the auto-update design, "Download staging: outside the install
    /// directory, applied by a detached helper".
    /// </summary>
    internal static class UpdateApplier
    {
        /// <summary>
        /// Launches the apply helper for <paramref name="update"/>, or does
        /// nothing (logging why) if the install directory isn't writable or
        /// the helper can't be started. Never throws - a failed apply must
        /// leave the current install exactly as it was.
        /// </summary>
        public static void ApplyOnExit(PendingUpdate update, string installDir)
        {
            if (update == null)
                return;

            try
            {
                if (!IsWritable(installDir))
                {
                    Logger.Instance.Log($"Auto-update: install directory '{installDir}' is not writable, skipping apply");
                    return;
                }

                string installExePath = Path.Combine(installDir, "StopWatch.exe");
                string scriptPath = Path.Combine(Path.GetTempPath(), $"StopWatch-update-{Guid.NewGuid():N}.cmd");

                string script = BuildApplyScript(
                    processId: Environment.ProcessId,
                    stagedPath: update.StagedPath,
                    isSelfContained: update.IsSelfContained,
                    installDir: installDir,
                    installExePath: installExePath);

                File.WriteAllText(scriptPath, script);

                Process.Start(new ProcessStartInfo(scriptPath)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Auto-update: failed to launch apply helper", ex);
            }
        }


        /// <summary>
        /// Builds the helper script's contents. Internal (not private) so
        /// it's testable without touching the filesystem or a process.
        /// </summary>
        internal static string BuildApplyScript(int processId, string stagedPath, bool isSelfContained, string installDir, string installExePath)
        {
            // Windows (commonly Defender's on-access scan of the just-written
            // .exe) can hold a brief lock on the staged or installed file
            // right after the process that wrote/ran it is gone - a single
            // move/robocopy attempt can lose that race. Both retry rather
            // than fail outright, so a transient lock doesn't silently leave
            // the old build in place and relaunch it unchanged.
            string swap = isSelfContained
                ? string.Join(Environment.NewLine,
                    "set \"SWAPTRIES=0\"",
                    ":swaploop",
                    $"move /y \"{stagedPath}\" \"{installExePath}\" >nul 2>&1",
                    "if not errorlevel 1 goto :swapdone",
                    "set /a SWAPTRIES+=1",
                    $"if %SWAPTRIES% GEQ {MaxSwapRetries} goto :swapdone",
                    "timeout /t 1 /nobreak >nul",
                    "goto :swaploop",
                    ":swapdone")
                // /MIR mirrors the extracted build into the install
                // directory: added/changed files are copied, anything the
                // new build no longer has is removed - matching it exactly.
                // /R and /W bound robocopy's own retrying (its defaults
                // amount to retrying for hours), covering the same
                // transient-lock case as the self-contained move above.
                : $"robocopy \"{stagedPath}\" \"{installDir}\" /MIR /NFL /NDL /NJH /NJS /R:{MaxSwapRetries} /W:1";

            return string.Join(Environment.NewLine,
                "@echo off",
                "setlocal",
                $"set \"PID={processId.ToString(CultureInfo.InvariantCulture)}\"",
                $"set \"COUNT=0\"",
                ":waitloop",
                "tasklist /FI \"PID eq %PID%\" | find \"%PID%\" >nul",
                "if errorlevel 1 goto :apply",
                "set /a COUNT+=1",
                $"if %COUNT% GEQ {MaxWaitSeconds} goto :giveup",
                "timeout /t 1 /nobreak >nul",
                "goto :waitloop",
                "",
                ":apply",
                swap,
                $"start \"\" \"{installExePath}\"",
                "",
                ":giveup",
                "(goto) 2>nul & del \"%~f0\"");
        }


        private static bool IsWritable(string dir)
        {
            try
            {
                string probePath = Path.Combine(dir, $".StopWatch-write-test-{Guid.NewGuid():N}");
                File.WriteAllBytes(probePath, Array.Empty<byte>());
                File.Delete(probePath);
                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// How long the helper waits for the app to exit before giving up
        /// and leaving the current install untouched, per the auto-update
        /// spec, "La app se cierra sin que la actualización se aplique".
        /// </summary>
        private const int MaxWaitSeconds = 30;

        /// <summary>
        /// How many times the swap step retries a locked file (e.g. Defender
        /// scanning the just-downloaded .exe) before giving up, one second
        /// apart.
        /// </summary>
        private const int MaxSwapRetries = 10;
    }
}
