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

using System.Diagnostics;
using System.Reflection;

namespace StopWatch
{
    /// <summary>
    /// What windows used to get from System.Windows.Forms.Application: opening
    /// a URL in the browser.
    /// </summary>
    internal static class AppInfo
    {
        /// <summary>
        /// The running assembly's version, as set by the release process in
        /// AssemblyInfo.cs (AssemblyInformationalVersion).
        /// </summary>
        public static string Version { get; } =
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "0.0.0";

        /// <summary>
        /// Whether this build is the self-contained release asset (bundles its
        /// own .NET runtime) rather than the framework-dependent one. Set by
        /// the STOPWATCH_SELF_CONTAINED compile-time constant, which
        /// scripts/publish-release-artifacts.js only defines for the
        /// self-contained publish - the two variants are otherwise
        /// indistinguishable at runtime. Auto-update uses this to pick the
        /// matching release asset.
        /// </summary>
        public static bool IsSelfContained =
#if STOPWATCH_SELF_CONTAINED
            true;
#else
            false;
#endif

        /// <summary>
        /// Opens a URL in the user's browser. UseShellExecute has to be set
        /// explicitly: unlike on .NET Framework, it defaults to false, and
        /// Process.Start would then look for an executable by that name.
        /// </summary>
        public static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
