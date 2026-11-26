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
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using StopWatch.Logging;

namespace StopWatch
{
    /// <summary>
    /// Replaces SettingsForm: every setting it had, plus the issue list's
    /// density.
    ///
    /// The settings object is only written when the dialog is accepted, which
    /// is what makes Cancel mean cancel. The caller reads the result with
    /// <c>await ShowDialog&lt;bool?&gt;(owner)</c>: true when accepted.
    /// </summary>
    internal partial class SettingsWindow : Window
    {
        #region public methods
        public SettingsWindow()
        {
            // Designer / previewer only: the real window is built with settings.
            InitializeComponent();
        }


        public SettingsWindow(Settings settings)
            : this()
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.settings = settings;

            Opened += SettingsWindow_Opened;
            lnkApiToken.PointerPressed += (s, e) => AppInfo.OpenUrl("https://id.atlassian.com/manage/api-tokens");
            lnkLogFolder.PointerPressed += (s, e) => OpenLogFolder();
            tbMaxIssues.AddHandler(TextInputEvent, Digits_TextInput, RoutingStrategies.Tunnel);
            tbMaxConcurrentTimers.AddHandler(TextInputEvent, Digits_TextInput, RoutingStrategies.Tunnel);

            // The minimize control exists wherever the tray does (spec
            // minimize-behavior): Windows and Linux. Taskbar Widget and its
            // monitor selector are Windows-only (spec taskbar-widget-view).
            gridMinimizeBehavior.IsVisible = OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

            tbJiraBaseUrl.Text = settings.JiraBaseUrl;
            tbUsername.Text = settings.Username;
            tbApiToken.Text = settings.ApiToken;

            cbAlwaysOnTop.IsChecked = settings.AlwaysOnTop;
            cbAllowMultipleTimers.IsChecked = settings.AllowMultipleTimers;
            tbMaxConcurrentTimers.Text = settings.MaxConcurrentTimers.ToString();
            cbIncludeProjectName.IsChecked = settings.IncludeProjectName;
            cbLoggingEnabled.IsChecked = settings.LoggingEnabled;
            cbCheckForUpdates.IsChecked = settings.CheckForUpdates;
            cbSubscribeToBetaReleases.IsChecked = settings.SubscribeToBetaReleases;

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

            Fill(cbTheme, settings.Theme,
                new Choice<ThemeMode>("Dark", ThemeMode.Dark),
                new Choice<ThemeMode>("Light", ThemeMode.Light));

            Fill(cbListDensity, settings.ListDensity,
                new Choice<ListDensity>("Compact", ListDensity.Compact),
                new Choice<ListDensity>("Spacious", ListDensity.Spacious));

            Fill(cbTimeDisplayFormat, settings.TimeDisplayFormat,
                new Choice<TimeDisplayFormat>("Jira (2h 15m)", TimeDisplayFormat.Jira),
                new Choice<TimeDisplayFormat>("Clock (2:15)", TimeDisplayFormat.Clock));

            // A Taskbar Widget setting that roamed in from Windows shows (and
            // behaves) as Mini View here.
            initialMinimizeBehavior = MinimizeBehaviors.Effective(settings.MinimizeBehavior, OperatingSystem.IsWindows());
            Fill(cbMinimizeBehavior, initialMinimizeBehavior,
                MinimizeBehaviors.Available(OperatingSystem.IsWindows())
                    .Select(b => new Choice<MinimizeBehavior>(MinimizeBehaviorText(b), b))
                    .ToArray());

            PopulateTaskbarWidgetMonitors();
            ApplyMinimizeBehaviorGating();

            cbMinimizeBehavior.SelectionChanged += (s, e) => ApplyMinimizeBehaviorGating();
            cbAllowMultipleTimers.IsCheckedChanged += (s, e) => ApplyMinimizeBehaviorGating();

            tbStartTransitions.Text = settings.StartTransitions;

            tbMaxIssues.Text = settings.MaxIssues.ToString();
            tbMaxIssues.TextChanged += (s, e) => tbMaxIssues.Classes.Set("invalid", !IsInRange(tbMaxIssues.Text, MinIssues, MaxIssues));
            tbMaxConcurrentTimers.TextChanged += (s, e) => tbMaxConcurrentTimers.Classes.Set("invalid", !IsInRange(tbMaxConcurrentTimers.Text, MinConcurrentTimers, MaxConcurrentTimersLimit));
        }
        #endregion


        #region private eventhandlers
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            settings.JiraBaseUrl = tbJiraBaseUrl.Text;
            settings.Username = tbUsername.Text;
            settings.ApiToken = tbApiToken.Text;

            settings.AlwaysOnTop = cbAlwaysOnTop.IsChecked == true;
            settings.AllowMultipleTimers = cbAllowMultipleTimers.IsChecked == true;
            settings.MaxConcurrentTimers = ParsedMaxConcurrentTimers;
            settings.IncludeProjectName = cbIncludeProjectName.IsChecked == true;
            settings.LoggingEnabled = cbLoggingEnabled.IsChecked == true;
            settings.CheckForUpdates = cbCheckForUpdates.IsChecked == true;
            settings.SubscribeToBetaReleases = cbSubscribeToBetaReleases.IsChecked == true;

            settings.SaveTimerState = Selected<SaveTimerSetting>(cbSaveTimerState);
            settings.PauseOnSessionLock = Selected<PauseAndResumeSetting>(cbPauseOnSessionLock);
            settings.PostWorklogComment = Selected<WorklogCommentSetting>(cbPostWorklogComment);
            settings.Theme = Selected<ThemeMode>(cbTheme);
            settings.ListDensity = Selected<ListDensity>(cbListDensity);
            settings.TimeDisplayFormat = Selected<TimeDisplayFormat>(cbTimeDisplayFormat);

            // Off Windows, leave a stored Taskbar Widget alone unless the user
            // picked something else (or turned on multiple timers, which it
            // excludes): it is what the setting means when it roams back.
            MinimizeBehavior minimizeBehavior = Selected<MinimizeBehavior>(cbMinimizeBehavior);
            if (minimizeBehavior != initialMinimizeBehavior || settings.AllowMultipleTimers)
                settings.MinimizeBehavior = minimizeBehavior;

            RadioButton selectedMonitor = panelTaskbarWidgetMonitors.Children
                .OfType<RadioButton>()
                .FirstOrDefault(r => r.IsChecked == true);
            if (selectedMonitor != null)
                settings.TaskbarWidgetMonitor = (int)selectedMonitor.Tag;
            else if (OperatingSystem.IsWindows())
                settings.TaskbarWidgetMonitor = 0;

            settings.StartTransitions = tbStartTransitions.Text;

            settings.MaxIssues = ParsedMaxIssues;

            Close(true);
        }


        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }


        /// <summary>
        /// Clamps the dialog to the working area of the screen it landed on,
        /// rather than a fixed guess. A fixed cap shorter than the settings
        /// list forced the inner ScrollViewer to scroll even on an ordinary
        /// monitor; the real limit is only ever the screen itself.
        /// </summary>
        private void SettingsWindow_Opened(object sender, EventArgs e)
        {
            if (OperatingSystem.IsWindows())
                NativeMethods.SetTitleBarDarkMode(this, StopWatch.Theme.Current.Mode == ThemeMode.Dark);

            Screen screen = Screens.ScreenFromWindow(this);
            if (screen != null)
                MaxHeight = Math.Max(screen.WorkingArea.Height / screen.Scaling - 40, 300);
        }


        private static void OpenLogFolder()
        {
            string folder = Path.GetDirectoryName(Logger.Instance.LogfilePath);

            AppInfo.OpenUrl(folder);
        }


        /// <summary>
        /// Keeps the field to digits, which is what the NumericUpDown did for
        /// free.
        /// </summary>
        private void Digits_TextInput(object sender, TextInputEventArgs e)
        {
            e.Handled = string.IsNullOrEmpty(e.Text) || !e.Text.All(c => c >= '0' && c <= '9');
        }


        private void MaxIssuesUp_Click(object sender, RoutedEventArgs e)
        {
            StepMaxIssues(1);
        }


        private void MaxIssuesDown_Click(object sender, RoutedEventArgs e)
        {
            StepMaxIssues(-1);
        }


        private void MaxConcurrentTimersUp_Click(object sender, RoutedEventArgs e)
        {
            StepMaxConcurrentTimers(1);
        }


        private void MaxConcurrentTimersDown_Click(object sender, RoutedEventArgs e)
        {
            StepMaxConcurrentTimers(-1);
        }
        #endregion


        #region private methods
        private static bool IsInRange(string text, int min, int max)
        {
            int parsed;
            return int.TryParse(text, out parsed) && parsed >= min && parsed <= max;
        }


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


        /// <summary>
        /// Moves the field by one step, clamped to the same range typing
        /// enforces. Reads back whatever the field currently holds rather than
        /// a remembered value, so a step lands correctly even after a manual
        /// edit the field hasn't been left yet.
        /// </summary>
        private void StepMaxIssues(int delta)
        {
            int current;
            if (!int.TryParse(tbMaxIssues.Text, out current))
                current = settings.MaxIssues;

            tbMaxIssues.Text = Math.Max(MinIssues, Math.Min(MaxIssues, current + delta)).ToString();
        }


        /// <summary>Same idea as <see cref="ParsedMaxIssues"/>, for the concurrent-timer cap.</summary>
        private int ParsedMaxConcurrentTimers
        {
            get
            {
                int parsed;
                if (!int.TryParse(tbMaxConcurrentTimers.Text, out parsed))
                    return settings.MaxConcurrentTimers;

                return Math.Max(MinConcurrentTimers, Math.Min(MaxConcurrentTimersLimit, parsed));
            }
        }


        private void StepMaxConcurrentTimers(int delta)
        {
            int current;
            if (!int.TryParse(tbMaxConcurrentTimers.Text, out current))
                current = settings.MaxConcurrentTimers;

            tbMaxConcurrentTimers.Text = Math.Max(MinConcurrentTimers, Math.Min(MaxConcurrentTimersLimit, current + delta)).ToString();
        }


        private static string MinimizeBehaviorText(MinimizeBehavior behavior)
        {
            switch (behavior)
            {
                case MinimizeBehavior.Tray:
                    return "Tray";
                case MinimizeBehavior.TaskbarWidget:
                    return "Taskbar Widget";
                default:
                    return "Mini View";
            }
        }


        /// <summary>
        /// One radio button per connected monitor (primary first, then
        /// secondaries ordered left-to-right/top-to-bottom - the same order
        /// <see cref="TaskbarInterop.GetSecondaryTrays"/> resolves monitor
        /// indices in): the widget shows on exactly one monitor, never
        /// several at once. Windows only; elsewhere there is no widget.
        /// </summary>
        private void PopulateTaskbarWidgetMonitors()
        {
            panelTaskbarWidgetMonitors.Children.Clear();

            if (!OperatingSystem.IsWindows())
                return;

            AddMonitorRadioButton(0, "Primary monitor");

            // The primary is one of the screens; the rest are the secondaries.
            int secondaries = Math.Max(ScreenAreas.Current().WorkingAreas.Count - 1, 0);
            for (int i = 0; i < secondaries; i++)
                AddMonitorRadioButton(i + 1, string.Format("Monitor {0}", i + 2));
        }


        private void AddMonitorRadioButton(int index, string label)
        {
            panelTaskbarWidgetMonitors.Children.Add(new RadioButton
            {
                Content = label,
                GroupName = "TaskbarWidgetMonitor",
                Tag = index,
                IsChecked = settings.TaskbarWidgetMonitor == index,
                Margin = new Avalonia.Thickness(0, 0, 0, 4)
            });
        }


        /// <summary>
        /// The taskbar widget only ever shows a single active timer, so it is
        /// mutually exclusive with allowing multiple timers at once: see the
        /// minimize-behavior spec, "Taskbar Widget es excluyente con permitir
        /// multiples timers".
        /// </summary>
        private void ApplyMinimizeBehaviorGating()
        {
            bool taskbarWidgetSelected = Selected<MinimizeBehavior>(cbMinimizeBehavior) == MinimizeBehavior.TaskbarWidget;

            gridTaskbarWidgetMonitors.IsVisible = taskbarWidgetSelected && OperatingSystem.IsWindows();

            // Disabling the checkbox alone leaves whatever it was already
            // checked to - and OK reads IsChecked regardless of IsEnabled -
            // so switching to Taskbar Widget has to clear it, not just grey
            // it out, or the two end up saved together.
            if (taskbarWidgetSelected)
                cbAllowMultipleTimers.IsChecked = false;

            cbAllowMultipleTimers.IsEnabled = !taskbarWidgetSelected;
            gridMaxConcurrentTimers.IsEnabled = !taskbarWidgetSelected && cbAllowMultipleTimers.IsChecked == true;
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

            /// <summary>
            /// Defensive fallback: ItemTemplate is what actually renders a
            /// choice, but anything that falls back to displaying the object
            /// itself (automation, a tooltip) should still say the friendly
            /// text, not the generic type name.
            /// </summary>
            public override string ToString()
            {
                return Text;
            }
        }
        #endregion


        #region private members
        private readonly Settings settings;
        private readonly MinimizeBehavior initialMinimizeBehavior;

        private const int MinIssues = 1;
        private const int MaxIssues = 40;

        private const int MinConcurrentTimers = 2;
        private const int MaxConcurrentTimersLimit = 20;
        #endregion
    }
}
