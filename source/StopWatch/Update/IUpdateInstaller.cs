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

namespace StopWatch.Update
{
    /// <summary>
    /// Applies a <see cref="PendingUpdate"/> on the app's way out, by
    /// whatever means the operating system needs: the running executable
    /// can't be replaced from inside the process on either platform, so each
    /// implementation hands the swap to a detached helper script.
    /// </summary>
    internal interface IUpdateInstaller
    {
        /// <summary>
        /// Launches the apply helper for <paramref name="update"/>, or does
        /// nothing (logging why) if the install directory isn't writable or
        /// the helper can't be started. Never throws - a failed apply must
        /// leave the current install exactly as it was.
        /// </summary>
        void ApplyOnExit(PendingUpdate update, string installDir);
    }


    /// <summary>The Windows installer: the self-deleting .cmd helper in <see cref="UpdateApplier"/>.</summary>
    internal sealed class WindowsUpdateInstaller : IUpdateInstaller
    {
        public void ApplyOnExit(PendingUpdate update, string installDir)
        {
            UpdateApplier.ApplyOnExit(update, installDir);
        }
    }


    internal static class UpdateInstallers
    {
        public static IUpdateInstaller ForPlatform(UpdatePlatform platform)
        {
            return platform == UpdatePlatform.Linux
                ? new LinuxUpdateInstaller()
                : new WindowsUpdateInstaller();
        }
    }
}
