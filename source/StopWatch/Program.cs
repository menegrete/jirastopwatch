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
using Avalonia;

namespace StopWatch
{
    internal static class Program
    {
        /// <summary>
        /// The entry point. The single-instance check and the AppDomain
        /// exception handler come first, before Avalonia exists, so that a
        /// second instance leaves without ever creating a window; everything
        /// else that used to be startup work is in App.
        ///
        /// Wayland is deliberately not enabled (UsePlatformDetect picks X11 or
        /// the native Windows backend); see the openspec design, decision 4.
        /// </summary>
        [STAThread]
        public static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                App.ReportException("Unhandled UI Exception", e.ExceptionObject as Exception);

            // Platform secret store for Settings (task 7.1 replaces this with ISecretStore).
            Settings.ProtectSecret = DPAPI.Encrypt;
            Settings.UnprotectSecret = DPAPI.Decrypt;

            if (!App.ClaimSingleInstance())
            {
                // Somebody is already running. They have been asked to come to
                // the front; this process leaves without ever showing a window.
                return 0;
            }

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }


        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect();
        }
    }
}
