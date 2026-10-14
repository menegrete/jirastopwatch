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
using System.Windows;

namespace StopWatch
{
    /// <summary>
    /// Asks for confirmation before a row that still has time not loaded to
    /// Jira is removed. Cancel is both the default (Enter) and the escape
    /// (Esc) button, so neither key discards the time.
    /// </summary>
    internal partial class ConfirmRemoveWindow : Window
    {
        #region public methods
        public ConfirmRemoveWindow(string issueKey, TimeSpan pendingTime, bool running)
        {
            InitializeComponent();

            string time = JiraTimeHelpers.TimeSpanToDisplayTime(pendingTime);
            tbMessage.Text = string.IsNullOrEmpty(issueKey)
                ? $"This row has {time} that hasn't been loaded to Jira. Remove it anyway?"
                : $"{issueKey} has {time} that hasn't been loaded to Jira. Remove it anyway?";
            tbRunning.Visibility = running ? Visibility.Visible : Visibility.Collapsed;

            SourceInitialized += (s, e) => NativeMethods.SetTitleBarDarkMode(this, Theme.Current.Mode == StopWatch.ThemeMode.Dark);
        }
        #endregion


        #region private eventhandlers
        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        #endregion
    }
}
