/**************************************************************************
Copyright 2016 Carsten Gehling

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
**************************************************************************/
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StopWatch
{
    static public class JiraTimeHelpers
    {
        public static TimeTrackingConfiguration Configuration { get; set; }

        /// <summary>
        /// How <see cref="TimeSpanToDisplayTime"/> presents a duration. Kept as
        /// a static, same as <see cref="Configuration"/>, because the row-level
        /// view model that reads it (IssueViewModel) has no reference to
        /// Settings of its own.
        /// </summary>
        public static TimeDisplayFormat TimeDisplayFormat { get; set; }

        public static string DateTimeToJiraDateTime(DateTimeOffset date)
        {
            string formatted = date.ToString("yyyy-MM-dd\\THH:mm:ss.fffzzzz", CultureInfo.InvariantCulture);
            return formatted.Substring(0, formatted.Length - 3) + formatted.Substring(formatted.Length - 2);
        }

        /// <summary>
        /// Formats a duration as a running clock: "1:23:45" past the hour,
        /// "23:45" below it.
        ///
        /// Deliberately NOT Jira notation. TimeSpanToJiraTime has minute
        /// resolution, which is right for a worklog but leaves a live display
        /// frozen for a whole minute at a time.
        /// </summary>
        public static string TimeSpanToClockTime(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero)
                ts = TimeSpan.Zero;

            int totalHours = (int)ts.TotalHours;

            if (totalHours > 0)
                return String.Format("{0}:{1:00}:{2:00}", totalHours, ts.Minutes, ts.Seconds);

            return String.Format("{0}:{1:00}", ts.Minutes, ts.Seconds);
        }

        /// <summary>
        /// Formats a duration as hours:minutes, no seconds, always with the
        /// hours part even at zero ("0:45", "2:15"). This is the Main Window's
        /// "clock" display option - deliberately not the same shape as
        /// <see cref="TimeSpanToClockTime"/>, which shows seconds for the live
        /// Mini Timer/Taskbar Widget display.
        /// </summary>
        public static string TimeSpanToClockHoursMinutes(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero)
                ts = TimeSpan.Zero;

            return String.Format("{0}:{1:00}", (int)ts.TotalHours, ts.Minutes);
        }

        /// <summary>
        /// Formats a duration the way the Main Window is currently configured
        /// to show it - see <see cref="TimeDisplayFormat"/>. Purely cosmetic:
        /// nothing that posts a worklog to Jira goes through this.
        /// </summary>
        public static string TimeSpanToDisplayTime(TimeSpan ts)
        {
            return TimeDisplayFormat == TimeDisplayFormat.Clock
                ? TimeSpanToClockHoursMinutes(ts)
                : TimeSpanToJiraTime(ts);
        }

        public static string TimeSpanToJiraTime(TimeSpan ts)
        {
            if (Configuration == null || ts.TotalHours < Configuration.workingHoursPerDay)
            {
                if (ts.Days > 0)
                    return String.Format("{0:%d}d {0:%h}h {0:%m}m", ts);

                if (ts.Hours > 0)
                    return String.Format("{0:%h}h {0:%m}m", ts);

                return String.Format("{0:%m}m", ts);
            }
            else
            {
                int days =(int) Math.Floor(ts.TotalMinutes / (Configuration.workingHoursPerDay * 60));
                int hours = (int) Math.Floor(ts.TotalHours - (days * Configuration.workingHoursPerDay));
                int minutes = (int) Math.Floor(ts.TotalMinutes - ((days * Configuration.workingHoursPerDay) + hours) * 60);
                if (days > 0)
                {
                    return String.Format("{0}d {1}h {2}m", days, hours, minutes);
                }
                else if (hours > 0)
                {
                    return String.Format("{0}h {1}m", hours, minutes);
                }
                else
                {
                    return String.Format("{0}m", minutes);
                }
            }

        }


        /// <summary>
        /// Parses the "H:mm" clock notation <see cref="TimeSpanToClockHoursMinutes"/>
        /// produces (e.g. "2:15", "0:45"). Returns null for anything else,
        /// including Jira notation - the two never overlap, since Jira
        /// notation always ends in d/h/m and this never does.
        /// </summary>
        public static TimeSpan? ClockHoursMinutesToTimeSpan(string time)
        {
            time = time.Trim();

            Match match = Regex.Match(time, @"^([0-9]+):([0-5][0-9])$");
            if (!match.Success)
                return null;

            int hours = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            int minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

            return new TimeSpan(hours, minutes, 0);
        }

        /// <summary>
        /// Parses whatever <see cref="TimeSpanToDisplayTime"/> would have shown
        /// for the current <see cref="TimeDisplayFormat"/>, falling back to the
        /// other notation so a value typed in either shape is still accepted -
        /// the two notations never overlap, so there is no ambiguity to resolve.
        /// </summary>
        public static TimeSpan? DisplayTimeToTimeSpan(string time)
        {
            if (TimeDisplayFormat == TimeDisplayFormat.Clock)
                return ClockHoursMinutesToTimeSpan(time) ?? JiraTimeToTimeSpan(time);

            return JiraTimeToTimeSpan(time) ?? ClockHoursMinutesToTimeSpan(time);
        }

        public static TimeSpan? JiraTimeToTimeSpan(string time)
        {
            string s;
            decimal t;
            int minutes = 0;
            bool validFormat = true;

            time = time.Trim();

            if (time == "0")
                return TimeSpan.Zero;

            MatchCollection matches = new Regex(@"([-]?[0-9,\.]+[dhm] *?)+?", RegexOptions.IgnoreCase).Matches(time);
            if (matches.Count == 0)
                return null;

            foreach (Match match in matches)
            {
                s = match.Value.ToUpper();
                s = s.Trim();

                if (!s.Contains("M") && !s.Contains("H") && !s.Contains("D"))
                {
                    validFormat = false;
                    break;
                }

                if (!decimal.TryParse(s.Replace("M", "").Replace("H", "").Replace("D", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out t))
                {
                    validFormat = false;
                    break;
                }

                if (s.Contains("M"))
                    minutes += (int)t;

                if (s.Contains("H"))
                    minutes += (int)(t * 60);

                if (s.Contains("D"))
                    minutes += (int)(t * 60 * 24);
                
                if (minutes < 0)
                    minutes = 0;
            }

            if (!validFormat)
                return null;

            return new TimeSpan(minutes / 60, minutes % 60, 0);
        }
    }
}

