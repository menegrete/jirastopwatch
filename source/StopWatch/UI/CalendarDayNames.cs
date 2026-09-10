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
using System.Globalization;

namespace StopWatch
{
    /// <summary>
    /// The abbreviated weekday names for the themed DatePicker's calendar
    /// popup, in the same order Calendar lays out its own day columns
    /// (starting from DateTimeFormatInfo.FirstDayOfWeek).
    ///
    /// Calendar reads its day-of-week header from a specific
    /// ComponentResourceKey (CalendarItem.DayTitleTemplateResourceKey)
    /// rather than through ordinary implicit-style lookup, and registering a
    /// DataTemplate under that key did not make it appear - so this header
    /// is drawn as ordinary static XAML instead, next to Calendar's own
    /// managed grid rather than inside it.
    /// </summary>
    internal static class CalendarDayNames
    {
        public static string[] OrderedAbbreviations
        {
            get
            {
                DateTimeFormatInfo format = CultureInfo.CurrentCulture.DateTimeFormat;
                string[] names = new string[7];

                for (int i = 0; i < 7; i++)
                    names[i] = format.AbbreviatedDayNames[((int)format.FirstDayOfWeek + i) % 7];

                return names;
            }
        }
    }
}
