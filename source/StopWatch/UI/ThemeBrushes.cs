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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace StopWatch
{
    /// <summary>
    /// Exposes the <see cref="Theme"/> palettes to Avalonia as resources.
    ///
    /// Both palettes are installed once, as the Dark and Light
    /// <c>ThemeDictionaries</c> of the application's resources. Switching theme
    /// is then only <see cref="ApplyToApplication"/>, which sets
    /// <see cref="Application.RequestedThemeVariant"/>: every open window picks
    /// the new brushes up through DynamicResource, with no restart and no walk
    /// over the control tree. Built from <see cref="Theme"/> rather than written
    /// in XAML so that the palette stays declared in one place.
    ///
    /// Two kinds of entries go into each dictionary:
    ///
    /// - The palette itself. Resource keys are the Theme property names, so XAML
    ///   says {DynamicResource Surface} for Theme.Current.Surface.
    /// - Overrides of Fluent's own theme resources (control-state keys such as
    ///   ButtonBackgroundPointerOver, and the SystemBase*/SystemChrome* colours),
    ///   so that Fluent's control templates - scroll bars, check marks, combo
    ///   boxes, menus, tooltips, hover/pressed/disabled states - draw with the
    ///   palette instead of Fluent's own greys, and the ui-theming contrast
    ///   guarantees hold without re-templating each control.
    /// </summary>
    internal static class ThemeBrushes
    {
        /// <summary>
        /// Adds the Dark and Light palettes to the application's resources.
        /// Called once, from App.Initialize, before any window exists.
        /// </summary>
        public static void Install(Application application)
        {
            Install(application.Resources, Theme.Dark, ThemeVariant.Dark);
            Install(application.Resources, Theme.Light, ThemeVariant.Light);
        }


        /// <summary>
        /// Makes the whole application use the active theme. Windows that are
        /// already open repaint at once.
        /// </summary>
        public static void ApplyToApplication()
        {
            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = VariantFor(Theme.Current.Mode);
        }


        public static ThemeVariant VariantFor(ThemeMode mode)
        {
            return mode == ThemeMode.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        }


        /// <summary>System.Drawing colour to Avalonia colour.</summary>
        public static Avalonia.Media.Color ToColor(System.Drawing.Color color)
        {
            return Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }


        public static SolidColorBrush ToBrush(System.Drawing.Color color)
        {
            return new SolidColorBrush(ToColor(color));
        }


        /// <summary>
        /// A palette brush of the active theme, for code-behind that cannot
        /// bind a DynamicResource (drawing, tray icon). Looked up by Theme
        /// property name, e.g. <c>ThemeBrushes.Current("TimerRunning")</c>.
        /// </summary>
        public static IBrush Current(string themeColorName)
        {
            return ToBrush(Theme.Current.Get(themeColorName));
        }


        /// <summary>Puts one palette under <paramref name="variant"/> in <paramref name="target"/> (also what the unit test drives).</summary>
        internal static void Install(IResourceDictionary target, Theme theme, ThemeVariant variant)
        {
            var palette = new ResourceDictionary();

            foreach (string name in Theme.ColorNames)
                palette[name] = ToBrush(theme.Get(name));

            foreach (var entry in FluentOverrides)
            {
                System.Drawing.Color color = entry.Value == null
                    ? System.Drawing.Color.Transparent
                    : theme.Get(entry.Value);

                if (entry.Key.EndsWith("Color", StringComparison.Ordinal))
                    palette[entry.Key] = ToColor(color);
                else
                    palette[entry.Key] = ToBrush(color);
            }

            target.ThemeDictionaries[variant] = palette;
        }


        #region Fluent overrides
        /// <summary>
        /// Fluent resource key to the palette colour it takes (null is
        /// transparent). Keys that end in "Color" are colours, the rest brushes.
        /// </summary>
        private static readonly Dictionary<string, string> FluentOverrides = BuildFluentOverrides();


        private static Dictionary<string, string> BuildFluentOverrides()
        {
            var map = new Dictionary<string, string>();

            // The system colours Fluent derives most brushes from.
            map["SystemRegionColor"] = "Background";
            map["SystemAltHighColor"] = "Background";
            map["SystemAltMediumHighColor"] = "Background";
            map["SystemBaseHighColor"] = "Text";
            map["SystemBaseMediumHighColor"] = "Text";
            map["SystemBaseMediumColor"] = "TextMuted";
            map["SystemBaseMediumLowColor"] = "Border";
            map["SystemBaseLowColor"] = "ButtonHover";
            map["SystemChromeLowColor"] = "Background";
            map["SystemChromeMediumColor"] = "Surface";
            map["SystemChromeMediumLowColor"] = "Surface";
            map["SystemChromeAltLowColor"] = "Surface";
            map["SystemChromeHighColor"] = "Border";
            map["SystemChromeDisabledHighColor"] = "SurfaceDisabled";
            map["SystemChromeDisabledLowColor"] = "TextMuted";
            map["SystemListLowColor"] = "ButtonHover";
            map["SystemListMediumColor"] = "ButtonPressed";
            map["SystemAccentColor"] = "Accent";
            map["SystemAccentColorLight1"] = "Accent";
            map["SystemAccentColorLight2"] = "Accent";
            map["SystemAccentColorLight3"] = "Accent";
            map["SystemAccentColorDark1"] = "Accent";
            map["SystemAccentColorDark2"] = "Accent";
            map["SystemAccentColorDark3"] = "Accent";

            // The brushes among those that Fluent defines once, not per control.
            map["SystemRegionBrush"] = "Background";
            map["SystemControlForegroundBaseHighBrush"] = "Text";
            map["SystemControlForegroundBaseMediumHighBrush"] = "Text";
            map["SystemControlForegroundBaseMediumBrush"] = "TextMuted";
            map["SystemControlForegroundBaseLowBrush"] = "TextMuted";
            map["SystemControlPageTextBaseHighBrush"] = "Text";
            map["SystemControlPageBackgroundAltHighBrush"] = "Background";
            map["SystemControlBackgroundAltHighBrush"] = "Background";
            map["SystemControlBackgroundChromeMediumBrush"] = "Surface";
            map["SystemControlBackgroundChromeMediumLowBrush"] = "Surface";
            map["SystemControlTransientBackgroundBrush"] = "Surface";
            map["SystemControlTransientBorderBrush"] = "Border";
            map["SystemControlHighlightListAccentLowBrush"] = "SurfaceActive";
            map["SystemControlHighlightListAccentMediumBrush"] = "SurfaceActive";
            map["SystemControlHighlightListAccentHighBrush"] = "SurfaceActive";
            map["SystemControlHighlightListLowBrush"] = "ButtonHover";
            map["SystemControlHighlightListMediumBrush"] = "ButtonPressed";
            map["SystemControlHyperlinkTextBrush"] = "Link";
            map["SystemControlErrorTextForegroundBrush"] = "Danger";

            // Buttons. Plain, toggle and repeat buttons share the states.
            foreach (string button in new[] { "Button", "ToggleButton", "RepeatButton" })
            {
                SetStates(map, button + "Background", "Surface", "ButtonHover", "ButtonPressed", "SurfaceDisabled");
                SetStates(map, button + "BorderBrush", "Border", "Border", "Border", "Border");
                SetStates(map, button + "Foreground", "Text", "Text", "Text", "TextMuted");
            }
            SetStates(map, "ToggleButtonBackgroundChecked", "SurfaceActive", "ButtonHover", "ButtonPressed", "SurfaceDisabled");
            SetStates(map, "ToggleButtonForegroundChecked", "Text", "Text", "Text", "TextMuted");
            SetStates(map, "HyperlinkButtonForeground", "Link", "Text", "Text", "TextMuted");
            SetStates(map, "HyperlinkButtonBackground", null, null, null, null);
            SetStates(map, "HyperlinkButtonBorderBrush", null, null, null, null);
            SetStates(map, "AccentButtonBackground", "Accent", "Accent", "Accent", "SurfaceDisabled");
            SetStates(map, "AccentButtonForeground", "AccentText", "AccentText", "AccentText", "TextMuted");
            SetStates(map, "AccentButtonBorderBrush", "Accent", "Accent", "Accent", "Border");

            // Check boxes: the unchecked box is the background colour with a
            // border that takes Text on hover. The three state families
            // (Unchecked, Checked, Indeterminate) are spelled alike.
            foreach (string state in new[] { "Unchecked", "Checked", "Indeterminate" })
            {
                SetStates(map, "CheckBoxBackground" + state, null, null, null, null);
                SetStates(map, "CheckBoxBorderBrush" + state, null, null, null, null);
                SetStates(map, "CheckBoxForeground" + state, "Text", "Text", "Text", "TextMuted");
                SetStates(map, "CheckBoxCheckBackgroundFill" + state, "Background", "Background", "ButtonHover", "SurfaceDisabled");
                SetStates(map, "CheckBoxCheckBackgroundStroke" + state, "Border", "Text", "Text", "Border");
                SetStates(map, "CheckBoxCheckGlyphForeground" + state, "Text", "Text", "Text", "TextMuted");
            }

            // A checked box is filled with the accent and ticked in the accent's
            // text colour, so that checked reads clearly against unchecked.
            foreach (string state in new[] { "Checked", "Indeterminate" })
            {
                SetStates(map, "CheckBoxCheckBackgroundFill" + state, "Accent", "Accent", "Accent", "SurfaceDisabled");
                SetStates(map, "CheckBoxCheckBackgroundStroke" + state, "Accent", "Accent", "Accent", "Border");
                SetStates(map, "CheckBoxCheckGlyphForeground" + state, "AccentText", "AccentText", "AccentText", "TextMuted");
            }

            // Radio buttons.
            SetStates(map, "RadioButtonBackground", null, null, null, null);
            SetStates(map, "RadioButtonBorderBrush", null, null, null, null);
            SetStates(map, "RadioButtonForeground", "Text", "Text", "Text", "TextMuted");
            foreach (string kind in new[] { "RadioButtonOuterEllipse", "RadioButtonOuterEllipseChecked" })
            {
                SetStates(map, kind + "Fill", "Background", "Background", "ButtonHover", "SurfaceDisabled");
                SetStates(map, kind + "Stroke", "Border", "Text", "Text", "Border");
            }
            SetStates(map, "RadioButtonCheckGlyphFill", "Text", "Text", "Text", "TextMuted");
            SetStates(map, "RadioButtonCheckGlyphStroke", "Text", "Text", "Text", "TextMuted");

            // Text input. Focus shows the accent, hover does not change it, as
            // in the WPF styles. (Pressed does not exist for these keys.)
            map["TextControlBackground"] = "Background";
            map["TextControlBackgroundPointerOver"] = "Background";
            map["TextControlBackgroundFocused"] = "Background";
            map["TextControlBackgroundDisabled"] = "SurfaceDisabled";
            map["TextControlBorderBrush"] = "Border";
            map["TextControlBorderBrushPointerOver"] = "Border";
            map["TextControlBorderBrushFocused"] = "Accent";
            map["TextControlBorderBrushDisabled"] = "Border";
            map["TextControlForeground"] = "Text";
            map["TextControlForegroundPointerOver"] = "Text";
            map["TextControlForegroundFocused"] = "Text";
            map["TextControlForegroundDisabled"] = "TextMuted";
            map["TextControlPlaceholderForeground"] = "TextMuted";
            map["TextControlPlaceholderForegroundPointerOver"] = "TextMuted";
            map["TextControlPlaceholderForegroundFocused"] = "TextMuted";
            map["TextControlPlaceholderForegroundDisabled"] = "TextMuted";
            SetStates(map, "TextControlButtonBackground", null, "ButtonHover", "ButtonPressed", null);
            SetStates(map, "TextControlButtonForeground", "TextMuted", "Text", "Text", "TextMuted");
            SetStates(map, "TextControlButtonBorderBrush", null, null, null, null);

            // Combo box and its items.
            SetStates(map, "ComboBoxBackground", "Background", "Background", "Background", "SurfaceDisabled");
            map["ComboBoxBackgroundUnfocused"] = "Background";
            map["ComboBoxBackgroundBorderBrushFocused"] = "Accent";
            map["ComboBoxBackgroundBorderBrushUnfocused"] = "Border";
            SetStates(map, "ComboBoxBorderBrush", "Border", "Border", "Border", "Border");
            SetStates(map, "ComboBoxForeground", "Text", "Text", "Text", "TextMuted");
            map["ComboBoxForegroundFocused"] = "Text";
            map["ComboBoxForegroundFocusedPressed"] = "Text";
            map["ComboBoxPlaceHolderForeground"] = "TextMuted";
            map["ComboBoxDropDownBackground"] = "Background";
            map["ComboBoxDropDownBorderBrush"] = "Border";
            map["ComboBoxDropDownGlyphForeground"] = "Text";
            map["ComboBoxDropDownGlyphForegroundDisabled"] = "TextMuted";
            map["ComboBoxDropDownGlyphForegroundFocused"] = "Text";
            map["ComboBoxDropDownGlyphForegroundFocusedPressed"] = "Text";
            SetStates(map, "ComboBoxItemBackground", null, "SurfaceActive", "SurfaceActive", null);
            SetStates(map, "ComboBoxItemBackgroundSelected", "SurfaceActive", "SurfaceActive", "SurfaceActive", "SurfaceDisabled");
            SetStates(map, "ComboBoxItemForeground", "Text", "Text", "Text", "TextMuted");
            SetStates(map, "ComboBoxItemForegroundSelected", "Text", "Text", "Text", "TextMuted");
            SetStates(map, "ComboBoxItemBorderBrush", null, null, null, null);
            SetStates(map, "ComboBoxItemBorderBrushSelected", null, null, null, null);

            // Scroll bars: a thumb in the border colour, no light track.
            map["ScrollBarBackground"] = "Background";
            map["ScrollBarBackgroundPointerOver"] = "Background";
            map["ScrollBarBorderBrush"] = null;
            map["ScrollBarForeground"] = "Border";
            map["ScrollBarThumbBackgroundColor"] = "Border";
            map["ScrollBarThumbFill"] = "Border";
            map["ScrollBarThumbFillPointerOver"] = "TextMuted";
            map["ScrollBarThumbFillPressed"] = "TextMuted";
            map["ScrollBarThumbFillDisabled"] = "Border";
            map["ScrollBarPanningThumbBackground"] = "Border";
            map["ScrollBarTrackFill"] = null;
            map["ScrollBarTrackFillPointerOver"] = "Surface";
            map["ScrollBarTrackStroke"] = null;
            map["ScrollBarTrackStrokePointerOver"] = null;
            SetStates(map, "ScrollBarButtonBackground", null, "ButtonHover", "ButtonPressed", null);
            SetStates(map, "ScrollBarButtonBorderBrush", null, null, null, null);
            SetStates(map, "ScrollBarButtonArrowForeground", "TextMuted", "Text", "Text", "TextMuted");

            // Tooltips and menus.
            map["ToolTipBackground"] = "Surface";
            map["ToolTipBorderBrush"] = "Border";
            map["ToolTipForeground"] = "Text";
            map["FlyoutPresenterBackground"] = "Surface";
            map["MenuFlyoutPresenterBackground"] = "Surface";
            map["MenuFlyoutPresenterBorderBrush"] = "Border";
            SetStates(map, "MenuFlyoutItemBackground", null, "SurfaceActive", "SurfaceActive", null);
            SetStates(map, "MenuFlyoutItemForeground", "Text", "Text", "Text", "TextMuted");
            SetStates(map, "MenuFlyoutItemKeyboardAcceleratorTextForeground", "TextMuted", "TextMuted", "TextMuted", "TextMuted");
            map["MenuFlyoutSubItemChevron"] = "TextMuted";
            map["MenuFlyoutSubItemChevronPointerOver"] = "Text";
            map["MenuFlyoutSubItemChevronPressed"] = "Text";
            map["MenuFlyoutSubItemChevronSubMenuOpened"] = "Text";
            map["MenuFlyoutSubItemChevronDisabled"] = "TextMuted";

            // Date picker and calendar.
            SetStates(map, "CalendarDatePickerBackground", "Background", "Background", "Background", "SurfaceDisabled");
            map["CalendarDatePickerBackgroundFocused"] = "Background";
            SetStates(map, "CalendarDatePickerBorderBrush", "Border", "Border", "Border", "Border");
            SetStates(map, "CalendarDatePickerForeground", "Text", "Text", "Text", "TextMuted");
            map["CalendarDatePickerTextForeground"] = "Text";
            map["CalendarDatePickerTextForegroundSelected"] = "Text";
            map["CalendarDatePickerTextForegroundDisabled"] = "TextMuted";
            map["CalendarDatePickerCalendarGlyphForeground"] = "Text";
            map["CalendarDatePickerCalendarGlyphForegroundDisabled"] = "TextMuted";
            map["CalendarViewBackground"] = "Background";
            map["CalendarViewBorderBrush"] = "Border";
            map["CalendarViewForeground"] = "Text";
            map["CalendarViewCalendarItemForeground"] = "Text";
            map["CalendarViewOutOfScopeBackground"] = "Surface";
            map["CalendarViewOutOfScopeForeground"] = "TextMuted";
            map["CalendarViewTodayForeground"] = "Link";
            map["CalendarViewHoverBorderBrush"] = "Text";
            map["CalendarViewPressedBorderBrush"] = "Text";
            map["CalendarViewSelectedBorderBrush"] = "Accent";
            map["CalendarViewSelectedHoverBorderBrush"] = "Accent";
            map["CalendarViewSelectedPressedBorderBrush"] = "Accent";
            map["CalendarViewNavigationButtonBackground"] = "Surface";
            map["CalendarViewNavigationButtonBorderBrush"] = "Border";
            map["CalendarViewNavigationButtonBorderBrushPointerOver"] = "Text";
            map["CalendarViewNavigationButtonForegroundPointerOver"] = "Text";
            map["CalendarViewNavigationButtonForegroundPressed"] = "Text";
            map["DatePickerFlyoutPresenterBackground"] = "Background";
            map["DatePickerFlyoutPresenterBorderBrush"] = "Border";

            return map;
        }


        /// <summary>
        /// Sets the four states of a Fluent control key, which are spelled
        /// Key, KeyPointerOver, KeyPressed and KeyDisabled. A null colour
        /// leaves the state transparent.
        /// </summary>
        private static void SetStates(Dictionary<string, string> map, string key, string normal, string pointerOver, string pressed, string disabled)
        {
            map[key] = normal;
            map[key + "PointerOver"] = pointerOver;
            map[key + "Pressed"] = pressed;
            map[key + "Disabled"] = disabled;
        }
        #endregion
    }
}
