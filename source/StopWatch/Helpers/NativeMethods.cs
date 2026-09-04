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
using System.Runtime.InteropServices;

namespace StopWatch
{
    /// <summary>
    /// The one Win32 message the application still needs: the broadcast a
    /// second instance sends so that the one already running comes to the
    /// front. App.OnStartup posts it; the main window picks it off its own
    /// handle with a hook.
    ///
    /// What used to be here besides this was a block of dark-mode interop -
    /// DwmSetWindowAttribute, SetWindowTheme, and SetPreferredAppMode reached
    /// through uxtheme by ordinal because it has no exported name. All of it
    /// existed to make WinForms controls respect a dark palette. With the UI in
    /// WPF the palette is the application's own, so none of it is needed.
    /// </summary>
    class NativeMethods
    {
        public const int HWND_BROADCAST = 0xffff;

        public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME");

        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32")]
        public static extern int RegisterWindowMessage(string message);
    }

    public enum EstimateUpdateMethods
    {
        Auto,
        Leave,
        SetTo,
        ManualDecrease
    }
}
