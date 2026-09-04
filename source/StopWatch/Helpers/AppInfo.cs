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
    /// The two things the windows used to get from
    /// System.Windows.Forms.Application: the product version, and opening a URL
    /// in the browser.
    /// </summary>
    internal static class AppInfo
    {
        public const string Name = "Jira StopWatch";


        public static string Version
        {
            get
            {
                if (version != null)
                    return version;

                version = FileVersionInfo
                    .GetVersionInfo(Assembly.GetExecutingAssembly().Location)
                    .ProductVersion ?? "";

                return version;
            }
        }


        /// <summary>
        /// Opens a URL in the user's browser. UseShellExecute has to be set
        /// explicitly: unlike on .NET Framework, it defaults to false, and
        /// Process.Start would then look for an executable by that name.
        /// </summary>
        public static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }


        private static string version;
    }
}
