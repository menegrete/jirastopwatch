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
    /// <summary>
    /// The three icons that need a lighter variant to stay legible on a dark
    /// surface. Contrast of the glyph against the dark background was measured:
    /// openbrowser 1.52:1, reset 2.90:1 and settings 2.90:1, all under the 3:1
    /// floor for non-text content. The "light" variants reach roughly 5:1.
    ///
    /// Every other icon in the set already clears 3:1 on dark and is used as-is.
    /// </summary>
    internal static class ThemeIcons
    {
        public static Image OpenBrowser
        {
            get { return Pick(Properties.Resources.openbrowser16, Properties.Resources.openbrowser16light); }
        }

        public static Image Reset
        {
            get { return Pick(Properties.Resources.reset16, Properties.Resources.reset16light); }
        }

        public static Image Settings
        {
            get { return Pick(Properties.Resources.settings16, Properties.Resources.settings16light); }
        }


        private static Image Pick(Image forLightTheme, Image forDarkTheme)
        {
            return Theme.Current.Mode == ThemeMode.Dark ? forDarkTheme : forLightTheme;
        }
    }
}
