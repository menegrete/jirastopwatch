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
    public class ScreenPlacementTest
    {
        // A primary 1920x1080 with a 40px taskbar at the bottom, plus a second
        // monitor to its left. Stated explicitly so the disconnected-monitor
        // case is testable without a second monitor.
        private static readonly Rectangle Primary = new Rectangle(0, 0, 1920, 1040);
        private static readonly Rectangle Secondary = new Rectangle(-1280, 0, 1280, 1024);

        private static readonly Size MiniSize = new Size(240, 36);


        #region parsing
        [Test]
        public void Parses_a_saved_location()
        {
            Point location;
            Assert.That(ScreenPlacement.TryParseLocation("100,250", out location), Is.True);
            Assert.That(location, Is.EqualTo(new Point(100, 250)));
        }


        [Test]
        public void Parses_negative_coordinates_from_a_monitor_left_of_primary()
        {
            Point location;
            Assert.That(ScreenPlacement.TryParseLocation("-1200,40", out location), Is.True);
            Assert.That(location, Is.EqualTo(new Point(-1200, 40)));
        }


        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("100")]
        [TestCase("100,200,300")]
        [TestCase("left,top")]
        [TestCase("100;200")]
        public void Rejects_unusable_text(string text)
        {
            Point location;
            Assert.That(ScreenPlacement.TryParseLocation(text, out location), Is.False);
        }


        [Test]
        public void Round_trips_through_the_saved_form()
        {
            Point original = new Point(-42, 900);

            Point parsed;
            Assert.That(ScreenPlacement.TryParseLocation(ScreenPlacement.FormatLocation(original), out parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(original));
        }
        #endregion


        #region keeping the window reachable
        [Test]
        public void A_position_on_a_connected_screen_is_kept()
        {
            Point desired = new Point(600, 300);

            Point result = ScreenPlacement.EnsureOnScreen(
                desired, MiniSize, new[] { Primary, Secondary }, Primary);

            Assert.That(result, Is.EqualTo(desired));
        }


        [Test]
        public void A_position_on_a_secondary_screen_is_kept()
        {
            Point desired = new Point(-1000, 100);

            Point result = ScreenPlacement.EnsureOnScreen(
                desired, MiniSize, new[] { Primary, Secondary }, Primary);

            Assert.That(result, Is.EqualTo(desired));
        }


        [Test]
        public void A_position_on_a_disconnected_screen_moves_to_the_primary()
        {
            // Saved while the second monitor was attached; now it is gone.
            Point desired = new Point(-1000, 100);

            Point result = ScreenPlacement.EnsureOnScreen(
                desired, MiniSize, new[] { Primary }, Primary);

            Assert.That(Primary.Contains(new Rectangle(result, MiniSize)), Is.True);
        }


        [Test]
        public void A_position_past_the_edge_of_a_shrunken_screen_is_pulled_back_in()
        {
            // The monitor is still there but the resolution dropped, so the
            // remembered position now hangs off the right edge.
            Point desired = new Point(1900, 20);

            Point result = ScreenPlacement.EnsureOnScreen(
                desired, MiniSize, new[] { Primary }, Primary);

            Assert.That(result.X, Is.EqualTo(Primary.Right - MiniSize.Width));
            Assert.That(Primary.Contains(new Rectangle(result, MiniSize)), Is.True);
        }


        [Test]
        public void With_no_screens_at_all_the_window_lands_on_the_primary_area()
        {
            Point result = ScreenPlacement.EnsureOnScreen(
                new Point(500, 500), MiniSize, new Rectangle[0], Primary);

            Assert.That(Primary.Contains(new Rectangle(result, MiniSize)), Is.True);
        }


        [Test]
        public void The_fallback_position_is_inside_the_primary_working_area()
        {
            Point result = ScreenPlacement.FallbackLocation(MiniSize, Primary);

            Assert.That(Primary.Contains(new Rectangle(result, MiniSize)), Is.True);
            Assert.That(result.Y, Is.GreaterThan(Primary.Top));
        }
        #endregion


        #region snapping
        [Test]
        public void Dropped_near_the_top_right_it_snaps_to_both_edges()
        {
            Point dropped = new Point(Primary.Right - MiniSize.Width - 8, Primary.Top + 6);

            Point result = ScreenPlacement.SnapToEdges(dropped, MiniSize, Primary, 20);

            Assert.That(result.X, Is.EqualTo(Primary.Right - MiniSize.Width));
            Assert.That(result.Y, Is.EqualTo(Primary.Top));
        }


        [Test]
        public void Dropped_near_the_bottom_left_it_snaps_to_both_edges()
        {
            Point dropped = new Point(Primary.Left + 5, Primary.Bottom - MiniSize.Height - 5);

            Point result = ScreenPlacement.SnapToEdges(dropped, MiniSize, Primary, 20);

            Assert.That(result.X, Is.EqualTo(Primary.Left));
            Assert.That(result.Y, Is.EqualTo(Primary.Bottom - MiniSize.Height));
        }


        [Test]
        public void Snapping_to_the_bottom_respects_the_taskbar()
        {
            Point dropped = new Point(500, Primary.Bottom - MiniSize.Height - 3);

            Point result = ScreenPlacement.SnapToEdges(dropped, MiniSize, Primary, 20);

            // Primary here is the working area, so its bottom already excludes
            // the taskbar; the window must not extend past it.
            Assert.That(result.Y + MiniSize.Height, Is.EqualTo(Primary.Bottom));
            Assert.That(result.Y + MiniSize.Height, Is.LessThanOrEqualTo(Primary.Bottom));
        }


        [Test]
        public void Dropped_in_the_middle_it_stays_put()
        {
            Point dropped = new Point(800, 500);

            Point result = ScreenPlacement.SnapToEdges(dropped, MiniSize, Primary, 20);

            Assert.That(result, Is.EqualTo(dropped));
        }


        [Test]
        public void Only_the_near_axis_snaps()
        {
            // Close to the left edge, nowhere near top or bottom.
            Point dropped = new Point(Primary.Left + 4, 500);

            Point result = ScreenPlacement.SnapToEdges(dropped, MiniSize, Primary, 20);

            Assert.That(result.X, Is.EqualTo(Primary.Left));
            Assert.That(result.Y, Is.EqualTo(500));
        }


        [Test]
        public void Snapping_on_a_secondary_screen_uses_that_screens_edges()
        {
            Point dropped = new Point(Secondary.Left + 6, Secondary.Top + 6);

            Point result = ScreenPlacement.SnapToEdges(dropped, MiniSize, Secondary, 20);

            Assert.That(result.X, Is.EqualTo(Secondary.Left));
            Assert.That(result.Y, Is.EqualTo(Secondary.Top));
        }
        #endregion


        #region a remembered width
        [Test]
        public void A_width_that_fits_is_left_alone()
        {
            Assert.That(ScreenPlacement.ClampWidth(1081, 620, Primary), Is.EqualTo(1081));
        }


        [Test]
        public void A_width_wider_than_the_screen_shrinks_to_it()
        {
            Assert.That(ScreenPlacement.ClampWidth(Primary.Width + 400, 620, Primary), Is.EqualTo(Primary.Width));
        }


        [Test]
        public void A_width_below_the_minimum_grows_to_it()
        {
            Assert.That(ScreenPlacement.ClampWidth(120, 620, Primary), Is.EqualTo(620));
        }


        [Test]
        public void The_minimum_wins_over_a_screen_narrower_than_it()
        {
            // Nothing can be done for a screen this narrow, and a window with
            // no room for the key and the time would be worse than one that
            // overflows.
            var tiny = new Rectangle(0, 0, 400, 800);

            Assert.That(ScreenPlacement.ClampWidth(1081, 620, tiny), Is.EqualTo(620));
        }
        #endregion
    }
}
