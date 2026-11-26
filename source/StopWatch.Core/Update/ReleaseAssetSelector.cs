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
using System.Linq;

namespace StopWatch.Update
{
    /// <summary>
    /// Names and picks the release asset matching the variant that's
    /// currently running, following the naming convention
    /// scripts/publish-release-artifacts.js uses:
    /// JiraStopWatch-v{version}-{self-contained|framework-dependent}.{exe|zip}.
    /// </summary>
    internal static class ReleaseAssetSelector
    {
        public static string AssetFileName(string version, bool selfContained)
        {
            return $"JiraStopWatch-v{version}-{Variant(selfContained)}.{Extension(selfContained)}";
        }


        public static string ChecksumFileName(string version, bool selfContained)
        {
            return $"{AssetFileName(version, selfContained)}.sha256";
        }


        public static ReleaseAsset FindAsset(ReleaseInfo release, string fileName)
        {
            return release?.Assets?.FirstOrDefault(
                asset => string.Equals(asset.Name, fileName, StringComparison.OrdinalIgnoreCase));
        }


        private static string Variant(bool selfContained) => selfContained ? "self-contained" : "framework-dependent";
        private static string Extension(bool selfContained) => selfContained ? "exe" : "zip";
    }
}
