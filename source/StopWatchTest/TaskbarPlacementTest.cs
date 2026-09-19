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

namespace StopWatchTest
{
    using System.Drawing;
    using NUnit.Framework;
    using StopWatch;


    [TestFixture]
    public class TaskbarPlacementTest
    {
        // A 1920x1080 primary monitor with a 40px taskbar settled at the bottom.
        private static readonly Rectangle Monitor = new Rectangle(0, 0, 1920, 1080);
        private static readonly Rectangle TraySettled = new Rectangle(0, 1040, 1920, 40);
        private static readonly Size WidgetSize = new Size(160, 28);


        #region ComputePosition - centered taskbar (Win11 default)
        [Test]
        public void Centered_taskbar_lands_left_of_the_widgets_button()
        {
            var anchors = new TaskbarPlacement.AnchorPoints
            {
                WidgetsRightPx = 860,
                StartLeftPx = 1000
            };

            var result = TaskbarPlacement.ComputePosition(TraySettled, 40, anchors, leftAlignedTaskbar: false, WidgetSize, minWidthPx: 60);

            Assert.That(result.Fits, Is.True);
            Assert.That(result.PositionPx.X, Is.EqualTo(868)); // widgetsRight + Gap(8)
        }


        [Test]
        public void Centered_taskbar_without_a_widgets_button_starts_at_the_tray_edge()
        {
            var anchors = new TaskbarPlacement.AnchorPoints { StartLeftPx = 900 };

            var result = TaskbarPlacement.ComputePosition(TraySettled, 40, anchors, leftAlignedTaskbar: false, WidgetSize, minWidthPx: 60);

            Assert.That(result.Fits, Is.True);
            Assert.That(result.PositionPx.X, Is.EqualTo(TraySettled.Left + 12)); // EdgeMargin
        }


        [Test]
        public void Centered_taskbar_with_no_start_anchor_yet_does_not_fit()
        {
            // Start always exists on a centered taskbar; a missing reading
            // means the query hasn't produced a usable result yet, so the
            // caller must not guess a position from it.
            var result = TaskbarPlacement.ComputePosition(TraySettled, 40, new TaskbarPlacement.AnchorPoints(), leftAlignedTaskbar: false, WidgetSize, minWidthPx: 60);

            Assert.That(result.Fits, Is.False);
        }
        #endregion


        #region ComputePosition - left-aligned taskbar
        [Test]
        public void Left_aligned_taskbar_lands_before_the_notification_area()
        {
            var anchors = new TaskbarPlacement.AnchorPoints
            {
                TaskButtonsRightPx = 400,
                TrayNotifyLeftPx = 1700
            };

            var result = TaskbarPlacement.ComputePosition(TraySettled, 40, anchors, leftAlignedTaskbar: true, WidgetSize, minWidthPx: 60);

            Assert.That(result.Fits, Is.True);
            Assert.That(result.PositionPx.X, Is.EqualTo(1700 - 8 - WidgetSize.Width)); // rightLimit(notify - Gap) - width
        }


        [Test]
        public void Left_aligned_taskbar_never_overlaps_the_last_app_button()
        {
            var anchors = new TaskbarPlacement.AnchorPoints
            {
                TaskButtonsRightPx = 1650,
                TrayNotifyLeftPx = 1750
            };

            var result = TaskbarPlacement.ComputePosition(TraySettled, 40, anchors, leftAlignedTaskbar: true, WidgetSize, minWidthPx: 60);

            Assert.That(result.Fits, Is.True);
            Assert.That(result.PositionPx.X, Is.EqualTo(1650 + 8)); // clamped to taskButtonsRight + Gap
        }
        #endregion


        [Test]
        public void Narrow_available_space_does_not_fit()
        {
            var anchors = new TaskbarPlacement.AnchorPoints
            {
                WidgetsRightPx = 890,
                StartLeftPx = 900 // only 10px free, less than min content width
            };

            var result = TaskbarPlacement.ComputePosition(TraySettled, 40, anchors, leftAlignedTaskbar: false, WidgetSize, minWidthPx: 60);

            Assert.That(result.Fits, Is.False);
        }


        #region band height
        [Test]
        public void Band_height_prefers_the_work_area_reservation()
        {
            int band = TaskbarPlacement.GetBandHeightPx(trayHeightPx: 48, reservedPx: 40, dpiScale: 1.0);

            Assert.That(band, Is.EqualTo(40));
        }


        [Test]
        public void Band_height_falls_back_to_the_default_when_there_is_no_reservation()
        {
            // Auto-hide taskbars reserve no work area.
            int band = TaskbarPlacement.GetBandHeightPx(trayHeightPx: 40, reservedPx: 0, dpiScale: 1.0);

            Assert.That(band, Is.EqualTo(40)); // capped by the actual tray height
        }


        [Test]
        public void Band_height_scales_the_default_with_dpi()
        {
            int band = TaskbarPlacement.GetBandHeightPx(trayHeightPx: 200, reservedPx: 0, dpiScale: 1.5);

            Assert.That(band, Is.EqualTo(72)); // 48dip * 1.5
        }
        #endregion


        #region visibility / auto-hide
        [Test]
        public void Fully_settled_taskbar_is_fully_visible()
        {
            int visible = TaskbarPlacement.GetVisiblePixels(TraySettled, Monitor);

            Assert.That(visible, Is.EqualTo(40));
        }


        [Test]
        public void Fully_hidden_taskbar_has_zero_visible_pixels()
        {
            Rectangle hidden = new Rectangle(0, 1080, 1920, 40); // slid entirely below the monitor

            int visible = TaskbarPlacement.GetVisiblePixels(hidden, Monitor);

            Assert.That(visible, Is.EqualTo(0));
        }


        [Test]
        public void Taskbar_mid_slide_is_partially_visible()
        {
            Rectangle midSlide = new Rectangle(0, 1060, 1920, 40); // 20 of 40px still on screen

            int visible = TaskbarPlacement.GetVisiblePixels(midSlide, Monitor);

            Assert.That(visible, Is.EqualTo(20));
        }


        [Test]
        public void Hidden_phase_is_zero_when_fully_visible()
        {
            Assert.That(TaskbarPlacement.GetHiddenPhase(visiblePx: 40, trayHeightPx: 40), Is.EqualTo(0.0));
        }


        [Test]
        public void Hidden_phase_is_one_when_fully_hidden()
        {
            Assert.That(TaskbarPlacement.GetHiddenPhase(visiblePx: 0, trayHeightPx: 40), Is.EqualTo(1.0));
        }


        [Test]
        public void Hidden_phase_is_partial_mid_slide()
        {
            Assert.That(TaskbarPlacement.GetHiddenPhase(visiblePx: 10, trayHeightPx: 40), Is.EqualTo(0.75).Within(0.0001));
        }
        #endregion


        #region easing
        [Test]
        public void Hiding_eases_in_from_zero()
        {
            Assert.That(TaskbarPlacement.Ease(0.0, hiding: true), Is.EqualTo(0.0));
            Assert.That(TaskbarPlacement.Ease(1.0, hiding: true), Is.EqualTo(1.0));
            Assert.That(TaskbarPlacement.Ease(0.5, hiding: true), Is.EqualTo(0.125).Within(0.0001));
        }


        [Test]
        public void Showing_eases_out_to_one()
        {
            Assert.That(TaskbarPlacement.Ease(0.0, hiding: false), Is.EqualTo(0.0));
            Assert.That(TaskbarPlacement.Ease(1.0, hiding: false), Is.EqualTo(1.0));
            Assert.That(TaskbarPlacement.Ease(0.5, hiding: false), Is.EqualTo(0.875).Within(0.0001));
        }


        [Test]
        public void Interpolates_between_two_positions()
        {
            Assert.That(TaskbarPlacement.Interpolate(0, 100, 0.0), Is.EqualTo(0));
            Assert.That(TaskbarPlacement.Interpolate(0, 100, 1.0), Is.EqualTo(100));
            Assert.That(TaskbarPlacement.Interpolate(0, 100, 0.5), Is.EqualTo(50));
        }
        #endregion
    }
}
