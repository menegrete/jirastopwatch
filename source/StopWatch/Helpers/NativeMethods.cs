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
    class NativeMethods
    {
        public const int HWND_BROADCAST = 0xffff;

        public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME");

        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32")]
        public static extern int RegisterWindowMessage(string message);


        #region dark mode interop
        // Documented as of Windows 10 20H1. On 1809..19H2 the attribute was 19.
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // NOT a documented API. "DarkMode_Explorer" is the theme name uxtheme
        // uses for dark scrollbars. If it stops working, scrollbars go back to
        // light and nothing else changes - see UseDarkScrollBars.
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        // SetPreferredAppMode is exported from uxtheme by ordinal only - it has
        // no name - so it has to be resolved by hand. Without this call the
        // DarkMode_* theme names above are silently ignored, which is why the
        // combo box button and the scrollbars stay light without it.
        private const int UXTHEME_ORDINAL_SetPreferredAppMode = 135;

        private enum PreferredAppMode
        {
            Default = 0,
            AllowDark = 1,
            ForceDark = 2,
            ForceLight = 3
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetPreferredAppModeFn(PreferredAppMode mode);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, IntPtr ordinal);


        /// <summary>
        /// Paints a window's title bar and non-client area dark. Silently does
        /// nothing on Windows versions that do not support it.
        /// </summary>
        public static void UseImmersiveDarkMode(IntPtr hwnd, bool enabled)
        {
            if (hwnd == IntPtr.Zero)
                return;

            int value = enabled ? 1 : 0;

            try
            {
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
            }
            catch (DllNotFoundException)
            {
                // Pre-Vista, or dwmapi unavailable. Light title bar, no harm.
            }
            catch (EntryPointNotFoundException)
            {
            }
        }


        /// <summary>
        /// Asks uxtheme to draw a control's native scrollbars dark. Degrades to
        /// light scrollbars if the undocumented theme name is unavailable.
        /// </summary>
        public static void UseDarkScrollBars(IntPtr hwnd, bool enabled)
        {
            ApplyWindowTheme(hwnd, enabled ? "DarkMode_Explorer" : "Explorer");
        }


        /// <summary>
        /// Asks uxtheme to draw a combo box's drop-down button dark. The body of
        /// the control already follows BackColor; the button does not, and this is
        /// the only way to reach it short of subclassing WM_PAINT.
        /// </summary>
        public static void UseDarkComboBox(IntPtr hwnd, bool enabled)
        {
            ApplyWindowTheme(hwnd, enabled ? "DarkMode_CFD" : "CFD");
        }


        /// <summary>
        /// Opts the process into dark mode so that the DarkMode_* window themes
        /// take effect. Must run before any window is created. Silently does
        /// nothing on Windows versions that do not export the entry point
        /// (pre-1903), in which case those elements simply stay light.
        /// </summary>
        public static void AllowDarkModeForApp(bool enabled)
        {
            try
            {
                IntPtr uxtheme = LoadLibrary("uxtheme.dll");
                if (uxtheme == IntPtr.Zero)
                    return;

                IntPtr proc = GetProcAddress(uxtheme, new IntPtr(UXTHEME_ORDINAL_SetPreferredAppMode));
                if (proc == IntPtr.Zero)
                    return;

                SetPreferredAppModeFn setPreferredAppMode =
                    (SetPreferredAppModeFn)Marshal.GetDelegateForFunctionPointer(proc, typeof(SetPreferredAppModeFn));

                setPreferredAppMode(enabled ? PreferredAppMode.AllowDark : PreferredAppMode.Default);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }


        /// <summary>
        /// Whether Windows is currently showing access-key underlines. Native
        /// controls keep them hidden until the user presses Alt; a hand-drawn
        /// control has to ask, because Control.ShowKeyboardCues is protected.
        /// </summary>
        public static bool KeyboardCuesVisible()
        {
            const uint SPI_GETKEYBOARDCUES = 0x100A;

            try
            {
                bool visible = false;
                if (SystemParametersInfo(SPI_GETKEYBOARDCUES, 0, ref visible, 0))
                    return visible;
            }
            catch (EntryPointNotFoundException)
            {
            }

            return false;
        }


        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, ref bool value, uint update);


        private static void ApplyWindowTheme(IntPtr hwnd, string subAppName)
        {
            if (hwnd == IntPtr.Zero)
                return;

            try
            {
                SetWindowTheme(hwnd, subAppName, null);
            }
            catch (DllNotFoundException)
            {
                // uxtheme unavailable - the element stays light, nothing breaks.
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
        #endregion
    }

    public enum EstimateUpdateMethods
    {
        Auto,
        Leave,
        SetTo,
        ManualDecrease
    }
}
