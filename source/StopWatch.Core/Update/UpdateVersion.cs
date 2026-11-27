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
    /// Parses and compares the semantic-release version strings this project
    /// uses - a git tag like "v3.1.0" or "v3.8.0-rc.1" and AppInfo.Version's
    /// "3.1.0" / "3.8.0-rc.1" - as a <see cref="SemanticVersion"/>, so a
    /// prerelease suffix is understood and ordered correctly.
    /// </summary>
    internal static class UpdateVersion
    {
        public static bool TryParse(string text, out SemanticVersion version)
        {
            return SemanticVersion.TryParse(text, out version);
        }


        public static bool IsNewer(SemanticVersion candidate, SemanticVersion current)
        {
            return candidate != null && current != null && candidate.CompareTo(current) > 0;
        }
    }
}
