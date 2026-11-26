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
    using System;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using NUnit.Framework;
    using StopWatch;


    [TestFixture]
    public class ThemeStatusColorsTest
    {
        private static readonly string[] StatusKeys = { "StatusToDo", "StatusInProgress", "StatusDone" };


        [Test, Apartment(ApartmentState.STA)]
        public void Apply_ReplacesStatusBrushesWhenTheThemeChanges()
        {
            var resources = new ResourceDictionary();

            ThemeBrushes.Apply(resources, Theme.Dark);
            Color[] dark = Array.ConvertAll(StatusKeys, k => ((SolidColorBrush)resources[k]).Color);

            ThemeBrushes.Apply(resources, Theme.Light);
            Color[] light = Array.ConvertAll(StatusKeys, k => ((SolidColorBrush)resources[k]).Color);

            for (int i = 0; i < StatusKeys.Length; i++)
                Assert.That(light[i], Is.Not.EqualTo(dark[i]), StatusKeys[i]);
        }


        // The icon sits on whatever background its row has: normal, selected
        // or with its timer running. 3:1 is the minimum for a graphic that
        // carries meaning.
        [TestCase("Dark")]
        [TestCase("Light")]
        public void StatusColors_StayVisibleOnEveryRowBackground(string mode)
        {
            Theme theme = mode == "Dark" ? Theme.Dark : Theme.Light;
            System.Drawing.Color[] icons = { theme.StatusToDo, theme.StatusInProgress, theme.StatusDone };
            System.Drawing.Color[] backgrounds = { theme.Surface, theme.SurfaceActive, theme.TimerRunning };

            foreach (System.Drawing.Color icon in icons)
                foreach (System.Drawing.Color background in backgrounds)
                    Assert.That(ContrastRatio(icon, background), Is.GreaterThanOrEqualTo(3.0),
                        $"{mode}: {ColorHex(icon)} on {ColorHex(background)}");
        }


        private static double ContrastRatio(System.Drawing.Color a, System.Drawing.Color b)
        {
            double la = RelativeLuminance(a);
            double lb = RelativeLuminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }


        private static double RelativeLuminance(System.Drawing.Color c)
        {
            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        }


        private static double Channel(byte value)
        {
            double v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }


        private static string ColorHex(System.Drawing.Color c)
        {
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }
}
