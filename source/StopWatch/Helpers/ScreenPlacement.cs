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
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace StopWatch
{
    /// <summary>
    /// Keeps a remembered window position reachable. A position saved on a
    /// monitor that is no longer connected, or at a resolution no longer in
    /// use, would otherwise put the window somewhere the user cannot click.
    ///
    /// The geometry functions take the screen rectangles as arguments so that
    /// the disconnected-monitor case can be tested without a second monitor.
    /// </summary>
    internal static class ScreenPlacement
    {
        /// <summary>Margin used when falling back to a default position.</summary>
        private const int FallbackMargin = 24;


        /// <summary>
        /// Parses the "x,y" form written to settings. Returns false for empty,
        /// malformed or out-of-range text, in which case the caller should use
        /// the fallback position.
        /// </summary>
        public static bool TryParseLocation(string text, out Point location)
        {
            location = Point.Empty;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string[] parts = text.Split(',');
            if (parts.Length != 2)
                return false;

            int x;
            int y;
            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out x))
                return false;
            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                return false;

            location = new Point(x, y);
            return true;
        }


        public static string FormatLocation(Point location)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0},{1}", location.X, location.Y);
        }


        /// <summary>
        /// Returns a position for a window of <paramref name="size"/> that is
        /// guaranteed to be on a connected screen.
        ///
        /// If the desired top-left lies on one of the working areas, the window
        /// is nudged so that it fits entirely within that area - which also
        /// covers "the monitor is still there but smaller than it was". If it
        /// lies on none of them, the window goes to the fallback corner of the
        /// primary working area.
        /// </summary>
        public static Point EnsureOnScreen(Point desired, Size size, IEnumerable<Rectangle> workingAreas, Rectangle primaryWorkingArea)
        {
            List<Rectangle> areas = (workingAreas ?? Enumerable.Empty<Rectangle>()).ToList();

            Rectangle host = areas.FirstOrDefault(a => a.Contains(desired));

            if (host.IsEmpty)
                return FallbackLocation(size, primaryWorkingArea);

            return ClampInto(desired, size, host);
        }


        /// <summary>Same as EnsureOnScreen, against the screens attached right now.</summary>
        public static Point EnsureOnScreen(Point desired, Size size)
        {
            return EnsureOnScreen(
                desired,
                size,
                Screen.AllScreens.Select(s => s.WorkingArea),
                Screen.PrimaryScreen.WorkingArea);
        }


        /// <summary>Where a window of this size goes when there is nothing remembered.</summary>
        public static Point FallbackLocation(Size size, Rectangle primaryWorkingArea)
        {
            return ClampInto(
                new Point(
                    primaryWorkingArea.Right - size.Width - FallbackMargin,
                    primaryWorkingArea.Top + FallbackMargin),
                size,
                primaryWorkingArea);
        }


        public static Point FallbackLocation(Size size)
        {
            return FallbackLocation(size, Screen.PrimaryScreen.WorkingArea);
        }


        /// <summary>
        /// Returns a width that fits the screen the window will appear on.
        ///
        /// The main window's width is remembered between runs, so it has the
        /// same problem as a remembered position: the screen may be narrower
        /// now than it was when the width was saved. Clamped up to
        /// <paramref name="minimum"/> as well, so that a settings file carrying
        /// a nonsense value cannot produce a window with no room for the issue
        /// key and the elapsed time.
        /// </summary>
        public static int ClampWidth(int desired, int minimum, Rectangle workingArea)
        {
            int widest = Math.Max(minimum, workingArea.Width);

            if (desired > widest)
                return widest;

            return desired < minimum ? minimum : desired;
        }


        /// <summary>Same as ClampWidth, against the primary screen.</summary>
        public static int ClampWidth(int desired, int minimum)
        {
            return ClampWidth(desired, minimum, Screen.PrimaryScreen.WorkingArea);
        }


        /// <summary>
        /// Snaps to whichever edges of <paramref name="area"/> the window was
        /// dropped near, leaving the other axis untouched.
        /// </summary>
        public static Point SnapToEdges(Point location, Size size, Rectangle area, int threshold)
        {
            int x = location.X;
            int y = location.Y;

            if (Math.Abs(x - area.Left) <= threshold)
                x = area.Left;
            else if (Math.Abs(area.Right - (x + size.Width)) <= threshold)
                x = area.Right - size.Width;

            if (Math.Abs(y - area.Top) <= threshold)
                y = area.Top;
            else if (Math.Abs(area.Bottom - (y + size.Height)) <= threshold)
                y = area.Bottom - size.Height;

            return ClampInto(new Point(x, y), size, area);
        }


        private static Point ClampInto(Point location, Size size, Rectangle area)
        {
            int x = location.X;
            int y = location.Y;

            // Right and bottom first, then left and top, so that a window
            // larger than the area ends up flush with its top-left rather than
            // pushed off the other side.
            if (x + size.Width > area.Right)
                x = area.Right - size.Width;
            if (y + size.Height > area.Bottom)
                y = area.Bottom - size.Height;

            if (x < area.Left)
                x = area.Left;
            if (y < area.Top)
                y = area.Top;

            return new Point(x, y);
        }
    }
}
