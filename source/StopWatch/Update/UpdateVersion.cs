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
    /// Parses and compares the semantic-release version strings this project
    /// uses - a git tag like "v3.1.0" and AppInfo.Version's "3.1.0" - as a
    /// plain <see cref="Version"/>. GitHub's `releases/latest` endpoint
    /// already excludes drafts and prereleases, so no prerelease suffix
    /// handling is needed here.
    /// </summary>
    internal static class UpdateVersion
    {
        public static bool TryParse(string text, out Version version)
        {
            string trimmed = text?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && (trimmed[0] == 'v' || trimmed[0] == 'V'))
                trimmed = trimmed.Substring(1);

            return Version.TryParse(trimmed, out version);
        }


        public static bool IsNewer(Version candidate, Version current)
        {
            return candidate != null && current != null && candidate > current;
        }
    }
}
