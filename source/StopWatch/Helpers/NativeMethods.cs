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
using System.Windows;
using System.Windows.Interop;

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
    /// through uxtheme by ordinal because it has no exported name. That block
    /// existed to make WinForms CONTROLS respect a dark palette, and with the
    /// UI in WPF none of it is needed for that. DwmSetWindowAttribute came
    /// back on its own, though: it is what the OS actually reads to decide
    /// whether a window's own title bar (which WPF does not draw or theme at
    /// all - it is non-client area, owned by DWM) is light or dark, and
    /// nothing about that changed by moving the client area to WPF.
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
        /// light. A no-op before the window's handle exists (i.e. before
        /// SourceInitialized) - callers made before then are expected to rely
        /// on a later call, not on this one retrying.
        /// </summary>
        public static void SetTitleBarDarkMode(Window window, bool dark)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            int useDarkMode = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        }


        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }

    public enum EstimateUpdateMethods
    {
        Auto,
        Leave,
        SetTo,
        ManualDecrease
    }
}
