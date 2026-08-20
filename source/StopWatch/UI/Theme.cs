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

using System.Drawing;

namespace StopWatch
{
    public enum ThemeMode
    {
        Dark = 0,
        Light = 1
    }


    /// <summary>
    /// Semantic colour palette. Nothing in the UI should reference a literal
    /// colour or a SystemColors member - everything goes through the active theme,
    /// so that a runtime state change cannot produce an off-theme colour.
    /// </summary>
    public class Theme
    {
        #region public properties
        public ThemeMode Mode { get; private set; }

        // Surfaces
        public Color Background { get; private set; }
        public Color Surface { get; private set; }
        public Color SurfaceActive { get; private set; }
        public Color SurfaceDisabled { get; private set; }
        public Color Border { get; private set; }

        // Text
        public Color Text { get; private set; }
        public Color TextMuted { get; private set; }

        // Accent band (top strip)
        public Color Accent { get; private set; }
        public Color AccentText { get; private set; }

        public Color Link { get; private set; }

        // State
        public Color Success { get; private set; }
        public Color Danger { get; private set; }
        public Color DangerSurface { get; private set; }
        public Color TimerRunning { get; private set; }

        // Button interaction states, derived from the surface colours
        public Color ButtonHover { get; private set; }
        public Color ButtonPressed { get; private set; }
        #endregion


        #region public static members
        public static readonly Theme Dark = new Theme
        {
            Mode = ThemeMode.Dark,

            Background = ColorFrom("#1E1E1E"),
            Surface = ColorFrom("#252526"),
            SurfaceActive = ColorFrom("#094771"),
            SurfaceDisabled = ColorFrom("#2D2D30"),
            Border = ColorFrom("#3F3F46"),

            Text = ColorFrom("#E8E8E8"),
            TextMuted = ColorFrom("#9A9A9A"),

            Accent = ColorFrom("#427AA9"),
            AccentText = ColorFrom("#FFFFFF"),

            Link = ColorFrom("#4FC1FF"),

            Success = ColorFrom("#4EC9B0"),
            Danger = ColorFrom("#F48771"),
            DangerSurface = ColorFrom("#5A2A24"),
            TimerRunning = ColorFrom("#2D4A2D"),

            ButtonHover = ColorFrom("#3E3E42"),
            ButtonPressed = ColorFrom("#4A4A4F")
        };

        public static readonly Theme Light = new Theme
        {
            Mode = ThemeMode.Light,

            Background = ColorFrom("#FFFFFF"),
            Surface = ColorFrom("#F5F5F5"),
            SurfaceActive = ColorFrom("#CDE6F7"),
            SurfaceDisabled = ColorFrom("#F0F0F0"),
            Border = ColorFrom("#C8C8C8"),

            Text = ColorFrom("#1E1E1E"),
            TextMuted = ColorFrom("#5F5F5F"),

            Accent = ColorFrom("#427AA9"),
            AccentText = ColorFrom("#FFFFFF"),

            Link = ColorFrom("#0B5FA5"),

            Success = ColorFrom("#12703A"),
            Danger = ColorFrom("#B3261E"),
            DangerSurface = ColorFrom("#F9C9C4"),
            TimerRunning = ColorFrom("#CDEBCD"),

            ButtonHover = ColorFrom("#E5E5E5"),
            ButtonPressed = ColorFrom("#D0D0D0")
        };


        /// <summary>
        /// The theme every form and control paints itself with. Set once at
        /// startup from settings, and again when the user changes the setting.
        /// </summary>
        public static Theme Current { get; set; } = Dark;


        public static Theme ForMode(ThemeMode mode)
        {
            return mode == ThemeMode.Light ? Light : Dark;
        }
        #endregion


        #region private methods
        private static Color ColorFrom(string hex)
        {
            return ColorTranslator.FromHtml(hex);
        }
        #endregion
    }
}
