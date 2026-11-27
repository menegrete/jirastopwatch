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
using Avalonia.Controls;

namespace StopWatch
{
    /// <summary>
    /// The one Win32 message the application still needs: the broadcast a
    /// second instance sends so that the one already running comes to the
    /// front. App posts it; the main window picks it off its own handle with
    /// a hook (Avalonia.Win32.Win32Properties.AddWndProcHookCallback).
    ///
    /// DwmSetWindowAttribute is what the OS reads to decide whether a
    /// window's own title bar (non-client area, owned by DWM) is light or
    /// dark; Avalonia does not follow the app's theme variant for it on its
    /// own. Every member here is Windows-only: callers guard with
    /// OperatingSystem.IsWindows().
    /// </summary>
    class NativeMethods
    {
        public const int HWND_BROADCAST = 0xffff;

        public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME");

        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32")]
        public static extern int RegisterWindowMessage(string message);


        /// <summary>
        /// Tells DWM whether this window's own title bar should be dark or
        /// light. A no-op before the window's handle exists (i.e. before the
        /// window is opened) - callers are expected to call it again from the
        /// window's Opened event, not to rely on this retrying.
        /// </summary>
        public static void SetTitleBarDarkMode(Window window, bool dark)
        {
            IntPtr hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero)
                return;

            int useDarkMode = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        }


        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
