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

using System.Windows;
using System.Windows.Media;

namespace StopWatch
{
    /// <summary>
    /// Exposes the active <see cref="Theme"/> to WPF as brush resources.
    ///
    /// Deliberately built from Theme rather than from WPF's own Fluent
    /// ThemeMode: the mini view has to look like it belongs to the same
    /// application as the WinForms windows, and Fluent's palette is not this
    /// palette. See the openspec design, "D6".
    ///
    /// Resource keys are the Theme property names, so XAML says
    /// {DynamicResource Surface} for Theme.Current.Surface.
    /// </summary>
    internal static class ThemeBrushes
    {
        /// <summary>
        /// Replaces every theme brush in <paramref name="target"/> with the
        /// active theme's. Safe to call again when the user changes theme;
        /// DynamicResource references pick the new brushes up.
        /// </summary>
        public static void Apply(ResourceDictionary target)
        {
            Apply(target, Theme.Current);
        }


        /// <summary>
        /// Repaints the whole application with the active theme.
        ///
        /// The brushes live in the application's own dictionary rather than in
        /// each window's, so one call reaches every window there is - including
        /// dialogs that are not open yet. This is what replaces walking each
        /// form's control tree.
        /// </summary>
        public static void ApplyToApplication()
        {
            if (Application.Current != null)
                Apply(Application.Current.Resources);
        }


        public static void Apply(ResourceDictionary target, Theme theme)
        {
            if (target == null || theme == null)
                return;

            Set(target, "Background", theme.Background);
            Set(target, "Surface", theme.Surface);
            Set(target, "SurfaceActive", theme.SurfaceActive);
            Set(target, "SurfaceDisabled", theme.SurfaceDisabled);
            Set(target, "Border", theme.Border);

            Set(target, "Text", theme.Text);
            Set(target, "TextMuted", theme.TextMuted);

            Set(target, "Accent", theme.Accent);
            Set(target, "AccentText", theme.AccentText);

            Set(target, "Link", theme.Link);
            Set(target, "Success", theme.Success);
            Set(target, "Danger", theme.Danger);
            Set(target, "DangerSurface", theme.DangerSurface);
            Set(target, "TimerRunning", theme.TimerRunning);

            Set(target, "ButtonHover", theme.ButtonHover);
            Set(target, "ButtonPressed", theme.ButtonPressed);
        }


        /// <summary>System.Drawing colour to WPF colour.</summary>
        public static Color ToMediaColor(System.Drawing.Color color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }


        public static SolidColorBrush ToBrush(System.Drawing.Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(ToMediaColor(color));
            brush.Freeze();
            return brush;
        }


        private static void Set(ResourceDictionary target, string key, System.Drawing.Color color)
        {
            target[key] = ToBrush(color);
        }
    }
}
