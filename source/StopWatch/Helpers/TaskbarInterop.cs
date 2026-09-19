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
 *
 * The taskbar-riding techniques in this file (window ownership via
 * GWLP_HWNDPARENT, ABM_GETSTATE for auto-hide detection, riding the
 * EVENT_OBJECT_LOCATIONCHANGE slide with a SetWindowRgn clip, and the
 * foreground-fullscreen check) are adapted from mechanicwb2-hub's
 * now-playing-taskbar-widget (MIT License, Copyright (c) 2026 MechanicWB),
 * https://github.com/mechanicwb2-hub/now-playing-taskbar-widget
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace StopWatch
{
    /// <summary>
    /// Raw Win32 interop for docking a window to the Windows taskbar and
    /// riding its auto-hide animation. Deliberately free of any WPF/WinForms
    /// type: <see cref="TaskbarWidgetWindow"/> is the only caller, and
    /// <see cref="TaskbarPlacement"/> holds the pure geometry this feeds.
    /// </summary>
    internal static class TaskbarInterop
    {
        #region window handles
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        /// <summary>The primary taskbar's window handle, or <see cref="IntPtr.Zero"/> if the shell is not (yet) running.</summary>
        public static IntPtr GetPrimaryTray()
        {
            return FindWindow("Shell_TrayWnd", null);
        }


        /// <summary>
        /// Secondary-monitor taskbars, ordered by their monitor's position
        /// (left-to-right, then top-to-bottom) rather than raw enumeration
        /// order - enumeration order is z-order, which changes on its own and
        /// would otherwise make "monitor 2" and "monitor 3" swap identity
        /// between sessions.
        /// </summary>
        public static List<IntPtr> GetSecondaryTrays()
        {
            List<(IntPtr Handle, int Left, int Top)> found = new List<(IntPtr, int, int)>();

            IntPtr handle = IntPtr.Zero;
            while ((handle = FindWindowEx(IntPtr.Zero, handle, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                RECT rect;
                if (GetWindowRect(handle, out rect))
                    found.Add((handle, rect.Left, rect.Top));
            }

            return found
                .OrderBy(t => t.Left)
                .ThenBy(t => t.Top)
                .Select(t => t.Handle)
                .ToList();
        }


        /// <summary>Left edge (physical px) of the system notification area (clock, network, etc.), or null if it cannot be found.</summary>
        public static int? GetTrayNotifyLeft(IntPtr tray)
        {
            IntPtr notify = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
            if (notify == IntPtr.Zero || !GetWindowRect(notify, out RECT rect))
                return null;

            return rect.Left;
        }
        #endregion


        #region rects and monitors
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X, Y;
        }

        /// <summary>
        /// The taskbar's own monitor: rcMonitor (the full monitor bounds) and
        /// rcWork (the work area, i.e. minus any reserved taskbar space).
        /// Queried from a point just above the taskbar's rect rather than the
        /// taskbar window itself, because with auto-hide the taskbar's rect
        /// can sit off-screen, where MonitorFromWindow would resolve to
        /// whichever monitor happens to be below instead of the taskbar's
        /// real home.
        /// </summary>
        public static bool TryGetTrayMonitorRects(RECT trayRect, out RECT monitorRect, out RECT workAreaRect)
        {
            POINT probe = new POINT { X = (trayRect.Left + trayRect.Right) / 2, Y = trayRect.Top - 10 };
            IntPtr monitor = MonitorFromPoint(probe, MONITOR_DEFAULTTONEAREST);

            MONITORINFO info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            {
                monitorRect = trayRect;
                workAreaRect = trayRect;
                return false;
            }

            monitorRect = info.rcMonitor;
            workAreaRect = info.rcWork;
            return true;
        }


        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);
        #endregion


        #region auto-hide state
        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        [DllImport("shell32.dll")]
        private static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        private const uint ABM_GETSTATE = 4;
        private const ulong ABS_AUTOHIDE = 1;

        /// <summary>True if the Windows taskbar currently has auto-hide enabled.</summary>
        public static bool IsAutoHideEnabled()
        {
            APPBARDATA data = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
            return ((ulong)SHAppBarMessage(ABM_GETSTATE, ref data) & ABS_AUTOHIDE) != 0;
        }
        #endregion


        #region window ownership and style
        private const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        private const int GWLP_HWNDPARENT = -8;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        /// <summary>Applies the tool-window/no-activate extended styles: no taskbar entry of its own, no Alt+Tab entry, never steals focus on click.</summary>
        public static void ApplyToolWindowStyle(IntPtr hwnd)
        {
            int current = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, current | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }


        /// <summary>
        /// Makes the window owned by the given taskbar handle: the window
        /// manager then keeps it above its owner by construction (no per-tick
        /// z-order re-assertion needed), and the window dies for free if the
        /// taskbar's process (explorer.exe) restarts.
        /// </summary>
        public static void SetOwner(IntPtr hwnd, IntPtr ownerHwnd)
        {
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, ownerHwnd);
        }


        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOZORDER = 0x0004;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        public static void EnsureTopmost(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }


        /// <summary>Moves the window to physical-pixel coordinates - positioning in raw pixels avoids DIP/DPI mismatches when the window spans monitors with different scales.</summary>
        public static void MoveWindowTo(IntPtr hwnd, int xPx, int yPx)
        {
            SetWindowPos(hwnd, IntPtr.Zero, xPx, yPx, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
        #endregion


        #region slide-animation clipping
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        // Per-handle, not a single shared flag: one widget window clearing its
        // clip must not be able to consume the "clear" for a widget on a
        // different monitor, which would leave that other one clipped/invisible.
        private static readonly HashSet<IntPtr> ClippedWindows = new HashSet<IntPtr>();

        /// <summary>
        /// Clips the window to whatever currently fits above the monitor's
        /// bottom edge, so the part that has already "slid off" during
        /// auto-hide does not keep drawing on a monitor placed below. The
        /// system takes ownership of the region after SetWindowRgn; it is not
        /// released here.
        /// </summary>
        public static void ClipWindowBottom(IntPtr hwnd, int widthPx, int heightPx, int visibleHeightPx)
        {
            if (visibleHeightPx >= heightPx)
            {
                if (ClippedWindows.Remove(hwnd))
                    SetWindowRgn(hwnd, IntPtr.Zero, true);
                return;
            }

            SetWindowRgn(hwnd, CreateRectRgn(0, 0, widthPx, Math.Max(0, visibleHeightPx)), true);
            ClippedWindows.Add(hwnd);
        }
        #endregion


        #region event hooks
        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        #endregion


        #region foreground / fullscreen
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private static readonly HashSet<string> ShellWindowClasses = new HashSet<string>
        {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "XamlExplorerHostIslandWindow"
        };

        /// <summary>
        /// True when the current foreground window covers the whole of the
        /// given monitor - a game or a video player in exclusive/borderless
        /// fullscreen, as opposed to shell surfaces (Start menu, search,
        /// screen-clipping overlays) which also happen to cover the screen
        /// but should not hide the widget.
        /// </summary>
        public static bool IsForegroundFullscreenOnMonitor(IntPtr self, RECT monitorRect)
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == self)
                return false;

            StringBuilder className = new StringBuilder(256);
            GetClassName(foreground, className, className.Capacity);
            string cls = className.ToString();
            if (ShellWindowClasses.Contains(cls))
                return false;

            if (!GetWindowRect(foreground, out RECT windowRect))
                return false;

            bool coversMonitor = windowRect.Left <= monitorRect.Left && windowRect.Top <= monitorRect.Top
                && windowRect.Right >= monitorRect.Right && windowRect.Bottom >= monitorRect.Bottom;

            return coversMonitor;
        }
        #endregion
    }
}
