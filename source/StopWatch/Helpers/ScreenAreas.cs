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
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace StopWatch
{
    /// <summary>
    /// The screens attached right now, as the plain rectangles
    /// <see cref="ScreenPlacement"/> works on. This is the only place that
    /// turns Avalonia's <see cref="Screens"/> into them; everything else about
    /// placement stays pure so it can be tested without a monitor.
    ///
    /// Units are device pixels, as <c>Screen.WorkingArea</c> was. Avalonia's
    /// <c>Window.Position</c> is in device pixels too, but <c>Width</c> and
    /// <c>Height</c> are device-independent units: multiply by the window's
    /// <c>DesktopScaling</c> before passing a size in.
    /// </summary>
    internal sealed class ScreenAreas
    {
        /// <summary>Used when there is no screen to ask, so callers still get a usable answer.</summary>
        private static readonly Rectangle DefaultArea = new Rectangle(0, 0, 1280, 720);


        public ScreenAreas(IEnumerable<Rectangle> workingAreas, Rectangle primaryWorkingArea)
        {
            WorkingAreas = (workingAreas ?? Enumerable.Empty<Rectangle>()).ToList();
            PrimaryWorkingArea = primaryWorkingArea;
        }


        public IReadOnlyList<Rectangle> WorkingAreas { get; private set; }

        public Rectangle PrimaryWorkingArea { get; private set; }


        /// <summary>The screens of <paramref name="screens"/>; a default area when it is null or empty.</summary>
        public static ScreenAreas From(Screens screens)
        {
            if (screens == null || screens.All.Count == 0)
                return new ScreenAreas(new[] { DefaultArea }, DefaultArea);

            List<Rectangle> areas = screens.All.Select(s => ToRectangle(s.WorkingArea)).ToList();
            Rectangle primary = screens.Primary != null ? ToRectangle(screens.Primary.WorkingArea) : areas[0];

            return new ScreenAreas(areas, primary);
        }


        /// <summary>
        /// The screens, asked of the main window - or of any open window - for
        /// code that has no window of its own. Falls back to a default area
        /// when the application has no window yet.
        /// </summary>
        public static ScreenAreas Current()
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            Window window = lifetime?.MainWindow ?? lifetime?.Windows.FirstOrDefault();

            return From(window?.Screens);
        }


        public static Rectangle ToRectangle(PixelRect rect)
        {
            return new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
        }


        /// <summary>Same as <see cref="ScreenPlacement.EnsureOnScreen(Point, Size, IEnumerable{Rectangle}, Rectangle)"/>, against these screens.</summary>
        public Point EnsureOnScreen(Point desired, Size size)
        {
            return ScreenPlacement.EnsureOnScreen(desired, size, WorkingAreas, PrimaryWorkingArea);
        }


        /// <summary>Same as <see cref="ScreenPlacement.NearestArea"/>, against these screens.</summary>
        public Rectangle NearestWorkingArea(Point point)
        {
            return ScreenPlacement.NearestArea(point, WorkingAreas, PrimaryWorkingArea);
        }


        public Point FallbackLocation(Size size)
        {
            return ScreenPlacement.FallbackLocation(size, PrimaryWorkingArea);
        }


        public int ClampWidth(int desired, int minimum)
        {
            return ScreenPlacement.ClampWidth(desired, minimum, PrimaryWorkingArea);
        }
    }
}
