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
using System.Globalization;
using Avalonia.Data.Converters;

namespace StopWatch
{
    /// <summary>
    /// Value converters shared by the windows. Visibility converters are gone:
    /// Avalonia has IsVisible (a bool), so WPF's WhenTrue/WhenFalse become
    /// <c>IsVisible="{Binding X}"</c> and
    /// <c>IsVisible="{Binding X, Converter={x:Static BoolConverters.Not}}"</c>.
    /// The two below are published as the StaticResources NotEmpty and
    /// StartStopTooltip by Styles.axaml.
    /// </summary>
    internal static class BoolConverterHelpers
    {
        public static bool IsTrue(object value)
        {
            return value is bool && (bool)value;
        }
    }


    /// <summary>
    /// True when the bound string is non-empty - used to enable an action
    /// only once a value (e.g. a summary resolved from Jira) is available,
    /// without needing a dedicated bool property on the view model.
    /// </summary>
    internal class NotEmptyToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrEmpty(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }


    /// <summary>
    /// The start/stop button's tooltip: the normal shortcut hint, unless the
    /// row is paused and starting it would exceed the configured
    /// concurrent-timer limit, in which case it explains why the click will
    /// not do anything - same "stay enabled, tooltip is the explanation"
    /// approach as the add-issue button's tooltip.
    ///
    /// Bind with a MultiBinding: values are (IsRunning, AtTimerLimit).
    /// </summary>
    internal class StartStopTooltipConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isRunning = values.Count > 0 && BoolConverterHelpers.IsTrue(values[0]);
            bool atLimit = values.Count > 1 && BoolConverterHelpers.IsTrue(values[1]);

            if (!isRunning && atLimit)
                return "Reached the max number of simultaneous timers - pause another one first";

            return "Start/stop timer (CTRL-P)";
        }
    }
}
