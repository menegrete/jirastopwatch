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

namespace StopWatch.Update
{
    /// <summary>
    /// A verified, staged build ready to apply on the next normal exit.
    /// </summary>
    internal sealed class PendingUpdate
    {
        public PendingUpdate(string version, string stagedPath, bool isSelfContained)
        {
            Version = version;
            StagedPath = stagedPath;
            IsSelfContained = isSelfContained;
        }

        /// <summary>The version this update will bring the app to.</summary>
        public string Version { get; }

        /// <summary>
        /// The self-contained build's staged .exe file, or the
        /// framework-dependent build's extracted directory.
        /// </summary>
        public string StagedPath { get; }

        public bool IsSelfContained { get; }
    }
}
