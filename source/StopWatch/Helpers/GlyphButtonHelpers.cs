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
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StopWatch
{
    /// <summary>Shared behaviour for the small glyph-only icon buttons (<c>GlyphButton</c> style), used by both the main window's row templates and the mini view's.</summary>
    static public class GlyphButtonHelpers
    {
        /// <summary>
        /// Swaps a copy icon's glyph for a check mark for a moment, then
        /// restores it. Purely visual - nothing here is persisted, so a row
        /// refresh mid-flash simply leaves the timer to restore the glyph.
        /// </summary>
        public static void FlashCopyConfirmation(Button button)
        {
            Path glyph = button.Content as Path;
            if (glyph == null)
                return;

            Geometry original = glyph.Data;
            glyph.Data = (Geometry)button.FindResource("GlyphCheck");

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                glyph.Data = original;
            };
            timer.Start();
        }
    }
}
