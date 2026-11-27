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
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace StopWatch
{
    /// <summary>
    /// Replaces EditTimeForm: sets a row's elapsed time to a value typed in
    /// Jira notation or clock notation (whichever - both are always
    /// accepted), and says so when the value does not parse.
    ///
    /// Closes with true when accepted, false when cancelled.
    /// </summary>
    internal partial class EditTimeWindow : Window
    {
        #region public members
        /// <summary>The time the user entered. Only meaningful when the dialog was accepted.</summary>
        public TimeSpan Time { get; private set; }
        #endregion


        #region public methods
        public EditTimeWindow()
        {
            // Designer / previewer only.
            InitializeComponent();
        }


        public EditTimeWindow(TimeSpan time)
            : this()
        {
            Time = time;
            tbTime.Text = JiraTimeHelpers.TimeSpanToDisplayTime(Time);

            // Set after the initial text, so loading the value is not a "change".
            tbTime.TextChanged += (s, e) => tbTime.Classes.Set("invalid", false);

            Opened += (s, e) =>
            {
                if (OperatingSystem.IsWindows())
                    NativeMethods.SetTitleBarDarkMode(this, StopWatch.Theme.Current.Mode == ThemeMode.Dark);

                tbTime.SelectAll();
                tbTime.Focus();
            };
        }
        #endregion


        #region private eventhandlers
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            TimeSpan? parsed = JiraTimeHelpers.DisplayTimeToTimeSpan(tbTime.Text);

            if (parsed == null)
            {
                // The dialog deliberately stays open: the value has to be
                // fixed, and the field says which value that is.
                tbTime.Classes.Set("invalid", true);
                tbTime.Focus();
                return;
            }

            Time = parsed.Value;
            Close(true);
        }


        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
        #endregion
    }
}
