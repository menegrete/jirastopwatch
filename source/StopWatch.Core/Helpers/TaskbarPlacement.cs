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
using System.Drawing;

namespace StopWatch
{
    /// <summary>
    /// Pure, DPI-agnostic geometry for the taskbar widget: where in the free
    /// space of the taskbar it lands, how tall the "drawn" band of the
    /// taskbar actually is, and how far into the auto-hide slide animation
    /// the taskbar currently is. Kept free of Win32/WPF types - same idea as
    /// <see cref="ScreenPlacement"/> - so it is testable without a real
    /// screen or a real taskbar. <see cref="Helpers.TaskbarInterop"/> and
    /// <see cref="Helpers.TaskbarAnchors"/> hold the interop that feeds these
    /// functions their inputs.
    /// </summary>
    internal static class TaskbarPlacement
    {
        /// <summary>Gap kept between the widget and whatever it is anchored next to.</summary>
        private const int Gap = 8;

        /// <summary>Kept clear of the taskbar's own edge on the anchor side.</summary>
        private const int EdgeMargin = 12;

        /// <summary>
        /// Above this reserved height, the OS-reported work-area reservation is
        /// trusted as the taskbar's real drawn height. At or below it (no
        /// reservation, e.g. auto-hide), <see cref="DefaultBandDip"/> is used
        /// instead.
        /// </summary>
        private const int ReservedHeightThresholdPx = 8;

        /// <summary>Win11 taskbar band height, in device-independent pixels, used when there is no work-area reservation to measure against.</summary>
        private const double DefaultBandDip = 48;


        /// <summary>The taskbar anchor points this needs, all in physical pixels. Any of them may be unknown (null).</summary>
        public struct AnchorPoints
        {
            /// <summary>Right edge of the widgets/date-time button (Win11 centered taskbars).</summary>
            public int? WidgetsRightPx;

            /// <summary>Left edge of the Start button (Win11 centered taskbars). Always present when the taskbar is centered.</summary>
            public int? StartLeftPx;

            /// <summary>Right edge of the rightmost open-app button (left-aligned taskbars).</summary>
            public int? TaskButtonsRightPx;

            /// <summary>Left edge of the system notification area (left-aligned taskbars).</summary>
            public int? TrayNotifyLeftPx;
        }


        /// <summary>Result of trying to place the widget in the taskbar's free space.</summary>
        public struct PlacementResult
        {
            /// <summary>False when even the minimum content would not fit - the caller should hide the widget instead of overlapping something else.</summary>
            public bool Fits;

            /// <summary>Top-left of the widget, in physical pixels. Only meaningful when <see cref="Fits"/> is true.</summary>
            public Point PositionPx;
        }


        /// <summary>
        /// Where the widget goes in the taskbar's free space, and whether it
        /// fits at all. Centered taskbars (Win11 default) have their free
        /// space to the left of the widgets/clock button, bounded by Start;
        /// left-aligned taskbars (Win10, or Win11 set to align left) have
        /// theirs to the left of the notification area, bounded by the last
        /// open-app button.
        /// </summary>
        public static PlacementResult ComputePosition(
            Rectangle trayRectPx,
            int bandHeightPx,
            AnchorPoints anchors,
            bool leftAlignedTaskbar,
            Size windowSizePx,
            int minWidthPx)
        {
            int leftPx;
            int rightLimitPx;

            if (!leftAlignedTaskbar)
            {
                // A centered taskbar always has a Start button; a missing
                // reading means the anchor query has not produced a usable
                // result yet (see TaskbarAnchors), not that Start disappeared.
                if (!anchors.StartLeftPx.HasValue)
                    return new PlacementResult { Fits = false };

                leftPx = anchors.WidgetsRightPx.HasValue
                    ? anchors.WidgetsRightPx.Value + Gap
                    : trayRectPx.Left + EdgeMargin;
                rightLimitPx = anchors.StartLeftPx.Value - Gap;
            }
            else
            {
                int leftLimitPx = anchors.TaskButtonsRightPx.HasValue
                    ? anchors.TaskButtonsRightPx.Value + Gap
                    : trayRectPx.Left + EdgeMargin;

                rightLimitPx = (anchors.TrayNotifyLeftPx ?? (trayRectPx.Right - EdgeMargin)) - Gap;
                leftPx = Math.Max(leftLimitPx, rightLimitPx - windowSizePx.Width);
            }

            int availablePx = rightLimitPx - leftPx;
            if (availablePx < minWidthPx)
                return new PlacementResult { Fits = false };

            int topPx = trayRectPx.Bottom - bandHeightPx + (bandHeightPx - windowSizePx.Height) / 2;

            return new PlacementResult
            {
                Fits = true,
                PositionPx = new Point(leftPx, topPx)
            };
        }


        /// <summary>
        /// How tall the taskbar's actually-drawn band is. Preferring the
        /// work-area reservation over the raw rect height matters on builds
        /// where the taskbar's window rect is taller than what is actually
        /// painted (multi-row taskbars, some Win11 builds) - centering
        /// against the full rect there floats the widget away from the
        /// visible bar. With no reservation (auto-hide has none), falls back
        /// to the Win11 default band height for the window's own DPI.
        /// </summary>
        public static int GetBandHeightPx(int trayHeightPx, int reservedPx, double dpiScale)
        {
            if (reservedPx > ReservedHeightThresholdPx)
                return Math.Min(trayHeightPx, reservedPx);

            return Math.Min(trayHeightPx, (int)Math.Round(DefaultBandDip * dpiScale));
        }


        /// <summary>
        /// How many pixels of the taskbar's rect currently fall inside its
        /// monitor - the taskbar's own rect, intersected with the monitor
        /// bounds vertically. Full height when settled on screen, shrinking
        /// as auto-hide slides it off, zero once it has slid away entirely.
        /// </summary>
        public static int GetVisiblePixels(Rectangle trayRectPx, Rectangle monitorRectPx)
        {
            int top = Math.Max(trayRectPx.Top, monitorRectPx.Top);
            int bottom = Math.Min(trayRectPx.Bottom, monitorRectPx.Bottom);

            return Math.Max(0, bottom - top);
        }


        /// <summary>
        /// How far into hiding the taskbar currently is, 0 (fully on screen)
        /// to 1 (fully hidden) - used to start the widget's own slide
        /// animation already in sync with wherever the taskbar's real
        /// animation currently is, instead of always starting from the top.
        /// </summary>
        public static double GetHiddenPhase(int visiblePx, int trayHeightPx)
        {
            if (trayHeightPx <= 0)
                return 1.0;

            return 1.0 - Clamp01((double)visiblePx / trayHeightPx);
        }


        /// <summary>
        /// Eased progress for the slide animation - matches the acceleration
        /// feel of the real taskbar's own show/hide animation: hiding speeds
        /// up (cubic ease-in), showing slows down (cubic ease-out).
        /// </summary>
        public static double Ease(double t, bool hiding)
        {
            double clamped = Clamp01(t);
            return hiding ? clamped * clamped * clamped : 1 - Math.Pow(1 - clamped, 3);
        }


        /// <summary>Linear interpolation between two Y positions, given an already-eased progress.</summary>
        public static int Interpolate(int fromPx, int toPx, double easedT)
        {
            return (int)Math.Round(fromPx + (toPx - fromPx) * easedT);
        }


        private static double Clamp01(double value)
        {
            if (value < 0)
                return 0;
            if (value > 1)
                return 1;
            return value;
        }
    }
}
