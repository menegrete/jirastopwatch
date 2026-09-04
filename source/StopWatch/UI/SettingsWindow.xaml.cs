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

using StopWatch.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace StopWatch
{
    /// <summary>
    /// Replaces SettingsForm: every setting it had, plus the issue list's
    /// density.
    ///
    /// The settings object is only written when the dialog is accepted, which
    /// is what makes Cancel mean cancel.
    /// </summary>
    internal partial class SettingsWindow : Window
    {
        #region public methods
        public SettingsWindow(Settings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.settings = settings;

            InitializeComponent();

            // Mono for MacOSX and Linux do not implement the notify icon, so
            // the feature is hidden where it does not exist.
            cbMinimizeToTray.Visibility = CrossPlatformHelpers.IsWindowsEnvironment()
                ? Visibility.Visible
                : Visibility.Collapsed;

            tbJiraBaseUrl.Text = settings.JiraBaseUrl;
            tbUsername.Text = settings.Username;
            tbApiToken.Text = settings.ApiToken;

            cbAlwaysOnTop.IsChecked = settings.AlwaysOnTop;
            cbMinimizeToTray.IsChecked = settings.MinimizeToTray;
            cbAllowMultipleTimers.IsChecked = settings.AllowMultipleTimers;
            cbIncludeProjectName.IsChecked = settings.IncludeProjectName;
            cbCheckForUpdate.IsChecked = settings.CheckForUpdate;
            cbLoggingEnabled.IsChecked = settings.LoggingEnabled;

            Fill(cbSaveTimerState, settings.SaveTimerState,
                new Choice<SaveTimerSetting>("Reset all timers on exit", SaveTimerSetting.NoSave),
                new Choice<SaveTimerSetting>("Save current timetracking, pause active timer", SaveTimerSetting.SavePause),
                new Choice<SaveTimerSetting>("Save current timetracking, active timer continues", SaveTimerSetting.SaveRunActive));

            Fill(cbPauseOnSessionLock, settings.PauseOnSessionLock,
                new Choice<PauseAndResumeSetting>("No pause", PauseAndResumeSetting.NoPause),
                new Choice<PauseAndResumeSetting>("Pause active timer", PauseAndResumeSetting.Pause),
                new Choice<PauseAndResumeSetting>("Pause and resume on unlock", PauseAndResumeSetting.PauseAndResume));

            Fill(cbPostWorklogComment, settings.PostWorklogComment,
                new Choice<WorklogCommentSetting>("Post only as part of worklog", WorklogCommentSetting.WorklogOnly),
                new Choice<WorklogCommentSetting>("Post only as a comment", WorklogCommentSetting.CommentOnly),
                new Choice<WorklogCommentSetting>("Post as both worklog and comment", WorklogCommentSetting.WorklogAndComment));

            // Qualified: Window has a ThemeMode property of its own, which
            // would otherwise shadow this enum inside a Window subclass.
            Fill(cbTheme, settings.Theme,
                new Choice<ThemeMode>("Dark", StopWatch.ThemeMode.Dark),
                new Choice<ThemeMode>("Light", StopWatch.ThemeMode.Light));

            Fill(cbListDensity, settings.ListDensity,
                new Choice<ListDensity>("Compact", ListDensity.Compact),
                new Choice<ListDensity>("Spacious", ListDensity.Spacious));

            tbStartTransitions.Text = settings.StartTransitions;

            tbMaxIssues.Text = settings.MaxIssues.ToString();
        }
        #endregion


        #region private eventhandlers
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            settings.JiraBaseUrl = tbJiraBaseUrl.Text;
            settings.Username = tbUsername.Text;
            settings.ApiToken = tbApiToken.Text;

            settings.AlwaysOnTop = cbAlwaysOnTop.IsChecked == true;
            settings.MinimizeToTray = cbMinimizeToTray.IsChecked == true;
            settings.AllowMultipleTimers = cbAllowMultipleTimers.IsChecked == true;
            settings.IncludeProjectName = cbIncludeProjectName.IsChecked == true;
            settings.CheckForUpdate = cbCheckForUpdate.IsChecked == true;
            settings.LoggingEnabled = cbLoggingEnabled.IsChecked == true;

            settings.SaveTimerState = Selected<SaveTimerSetting>(cbSaveTimerState);
            settings.PauseOnSessionLock = Selected<PauseAndResumeSetting>(cbPauseOnSessionLock);
            settings.PostWorklogComment = Selected<WorklogCommentSetting>(cbPostWorklogComment);
            settings.Theme = Selected<ThemeMode>(cbTheme);
            settings.ListDensity = Selected<ListDensity>(cbListDensity);

            settings.StartTransitions = tbStartTransitions.Text;

            settings.MaxIssues = ParsedMaxIssues;

            DialogResult = true;
        }


        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            new AboutWindow { Owner = this }.ShowDialog();
        }


        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            AppInfo.OpenUrl(e.Uri.ToString());
            e.Handled = true;
        }


        private void OpenLogFolder_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            string folder = Path.GetDirectoryName(Logger.Instance.LogfilePath);

            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            e.Handled = true;
        }


        /// <summary>
        /// Keeps the field to digits, which is what the NumericUpDown did for
        /// free.
        /// </summary>
        private void Digits_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }


        private void tbMaxIssues_TextChanged(object sender, TextChangedEventArgs e)
        {
            int parsed;
            bool valid = int.TryParse(tbMaxIssues.Text, out parsed)
                && parsed >= MinIssues
                && parsed <= MaxIssues;

            tbMaxIssues.Style = valid ? null : (Style)FindResource("InvalidInput");
        }
        #endregion


        #region private methods
        /// <summary>
        /// The value the field holds, clamped to the range the NumericUpDown
        /// used to enforce, so that an out-of-range entry cannot be saved.
        /// </summary>
        private int ParsedMaxIssues
        {
            get
            {
                int parsed;
                if (!int.TryParse(tbMaxIssues.Text, out parsed))
                    return settings.MaxIssues;

                return Math.Max(MinIssues, Math.Min(MaxIssues, parsed));
            }
        }


        private static void Fill<T>(ComboBox combo, T selected, params Choice<T>[] choices)
        {
            combo.ItemsSource = choices;
            combo.SelectedItem = choices.FirstOrDefault(c => Equals(c.Value, selected)) ?? choices[0];
        }


        private static T Selected<T>(ComboBox combo)
        {
            return ((Choice<T>)combo.SelectedItem).Value;
        }
        #endregion


        #region private classes
        /// <summary>One option of a settings drop-down: what it says, and what it means.</summary>
        private class Choice<T>
        {
            public string Text { get; private set; }
            public T Value { get; private set; }

            public Choice(string text, T value)
            {
                Text = text;
                Value = value;
            }
        }
        #endregion


        #region private members
        private readonly Settings settings;

        private const int MinIssues = 1;
        private const int MaxIssues = 40;
        #endregion
    }
}
