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
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Screen = System.Windows.Forms.Screen;

namespace StopWatch
{
    /// <summary>
    /// The main window: the filter and connection bar, the list of issue rows,
    /// and the total at the bottom.
    ///
    /// Replaces MainForm. Two things it deliberately does differently:
    ///
    /// - The list is a collection rendered by template, not N controls
    ///   positioned by hand at a fixed row height. That is what lets a row be
    ///   as tall as its own summary needs.
    /// - The height is derived from the rows, as it was, but as the sum of
    ///   their individual heights rather than count times a constant. The width
    ///   is the user's to choose and is remembered.
    ///
    /// WinForms is still referenced on purpose, for the tray icon and for
    /// enumerating screens. See the openspec design, Non-Goals.
    /// </summary>
    internal partial class MainWindow : Window
    {
        #region public methods
        public MainWindow(Settings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.settings = settings;

            Theme.Current = Theme.ForMode(settings.Theme);

            jiraApiRequestFactory = new JiraApiRequestFactory(new RestRequestFactory());

            restClientFactory = new RestClientFactory();
            restClientFactory.BaseUrl = settings.JiraBaseUrl;

            jiraClient = new JiraClient(jiraApiRequestFactory, new JiraApiRequester(restClientFactory, jiraApiRequestFactory));

            jiraService = new IssueJiraService(jiraClient, settings);

            filterProvider = new FilterProvider(jiraClient, settings);
            filterProvider.FiltersLoaded += filterProvider_FiltersLoaded;

            issues = new IssueListViewModel(settings);
            issues.TimerStarted += issues_TimerStarted;

            activeTimer = new ActiveTimerViewModel(() => issues.Issues.Cast<ITimerSource>());

            InitializeComponent();

            DataContext = issues;

            RegisterCommands();

            ticker = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Normal, Dispatcher);
            ticker.Interval = TimeSpan.FromMilliseconds(firstDelay);
            ticker.Tick += ticker_Tick;

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            StateChanged += MainWindow_StateChanged;
            SizeChanged += MainWindow_SizeChanged;

            ApplyTheme();
        }


        /// <summary>
        /// Repaints with the active theme. Called at construction and again
        /// whenever the user changes the theme setting.
        ///
        /// This is the whole of what ThemeApplier's 579-line walk over the
        /// control tree used to do: the styles reference the theme through
        /// DynamicResource, so replacing the brushes repaints everything.
        /// </summary>
        public void ApplyTheme()
        {
            ThemeBrushes.ApplyToApplication();

            // Animated colours are held in each element's own brush rather than
            // in the dictionary, so they have to be told to re-read the theme.
            foreach (ThemeAnimatedBorder border in Descendants<ThemeAnimatedBorder>(this))
                border.SnapToState();
        }


        /// <summary>
        /// Brings this window to the front. Called when a second instance of
        /// the application starts.
        /// </summary>
        public void ShowOnTop()
        {
            // Launching the app again while the mini view is up means the user
            // wants the full window, not a second stand-in next to it.
            if (inMiniView)
            {
                ExitMiniView();
                return;
            }

            if (WindowState == WindowState.Minimized)
            {
                Show();
                WindowState = WindowState.Normal;
                HideTrayIcon();
            }

            // Jump to the top of everything, then hand topmost back to whatever
            // the setting says it should be.
            bool wanted = Topmost;
            Topmost = true;
            Topmost = wanted;
            Activate();
        }


        public void HandleSessionLock()
        {
            if (settings.PauseOnSessionLock == PauseAndResumeSetting.NoPause)
                return;

            // As before, only the first running row is paused and remembered.
            IssueViewModel issue = issues.Running.FirstOrDefault();
            if (issue == null)
                return;

            lastRunningIssue = issue;
            issue.Pause();
            activeTimer.Refresh();
        }


        public void HandleSessionUnlock()
        {
            if (settings.PauseOnSessionLock != PauseAndResumeSetting.PauseAndResume)
                return;

            if (lastRunningIssue == null)
                return;

            IssueViewModel resumed = lastRunningIssue;
            lastRunningIssue = null;

            // Resuming here deliberately skips the single-timer rule, exactly
            // as the WinForms window did: it restores what was running, it does
            // not start something new.
            resumed.WatchTimer.Start();
            resumed.Refresh();
            activeTimer.NotifyTimerStarted(resumed);
        }
        #endregion


        #region window lifetime
        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            // WM_SHOWME used to be caught by overriding the form's WndProc.
            // A WPF window has no WndProc to override, so the same message is
            // picked off the window's handle with a hook. See the openspec
            // design, D8.
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
                source.AddHook(WndProcHook);
        }


        private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_SHOWME)
                ShowOnTop();

            return IntPtr.Zero;
        }


        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreWidth();
            ClampHeightToWorkingArea();

            Topmost = settings.AlwaysOnTop;

            issues.Hydrate();
            UpdateAddIssueTooltip();

            if (settings.FirstRun)
            {
                settings.FirstRun = false;

                // Deferred rather than called inline: showing a modal dialog
                // from within Loaded races this window's own activation, and
                // the dialog can end up behind it.
                Dispatcher.BeginInvoke(new Action(EditSettings));
            }
            else if (IsJiraEnabled)
            {
                AuthenticateJira(settings.Username, settings.ApiToken);
            }

            ticker.Start();
        }


        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // Reached even while hidden behind the mini view - a session
            // shutdown, for instance - so the mini view has to come down with
            // it, and settings still get written exactly as before.
            if (miniView != null)
            {
                miniView.ClosingForShutdown = true;
                miniView.Close();
                miniView = null;
            }

            DisposeTrayIcon();
            SaveSettingsAndIssueStates();
        }


        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged || !IsLoaded)
                return;

            // Only the width is the user's, so only the width is remembered.
            settings.MainWindowWidth = (int)Math.Round(ActualWidth);
        }
        #endregion


        #region size
        /// <summary>
        /// Restores the remembered width, validated against the screen that is
        /// actually there - the same problem the mini view's position has, so
        /// the same helper answers it.
        /// </summary>
        private void RestoreWidth()
        {
            Width = ScreenPlacement.ClampWidth(
                settings.MainWindowWidth,
                (int)MinWidth,
                WorkingArea);
        }


        /// <summary>
        /// The derived height stops at the working area. Past that the list
        /// scrolls, which is the overflow of this limit rather than the normal
        /// way to use the window.
        /// </summary>
        private void ClampHeightToWorkingArea()
        {
            MaxHeight = WorkingArea.Height;
        }


        private System.Drawing.Rectangle WorkingArea
        {
            get
            {
                // WinForms answers this, deliberately and permanently: WPF has
                // no screen enumeration of its own.
                if (!IsLoaded)
                    return Screen.PrimaryScreen.WorkingArea;

                return Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            }
        }
        #endregion


        #region the tray icon
        /// <summary>
        /// The tray icon behaves exactly as it did, only its trigger changed:
        /// WPF has no equivalent of MainForm_Resize, so the window's own state
        /// drives it. The five rules recorded in the change's inventory.md are
        /// all here.
        /// </summary>
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (!CrossPlatformHelpers.IsWindowsEnvironment())
                return;

            // While the mini view is up this window is hidden on purpose. The
            // tray icon must not appear as well: two stand-ins for one hidden
            // window is one too many.
            if (inMiniView)
                return;

            if (!settings.MinimizeToTray)
                return;

            if (WindowState == WindowState.Minimized)
            {
                ShowTrayIcon();
                Hide();
            }
            else if (WindowState == WindowState.Normal)
            {
                HideTrayIcon();
            }
        }


        private void ShowTrayIcon()
        {
            if (trayIcon == null)
            {
                trayIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = Properties.Resources.stopwatchicon,
                    Text = "Jira StopWatch"
                };
                trayIcon.Click += trayIcon_Click;
            }

            trayIcon.Visible = true;
        }


        private void HideTrayIcon()
        {
            if (trayIcon != null)
                trayIcon.Visible = false;
        }


        private void DisposeTrayIcon()
        {
            if (trayIcon == null)
                return;

            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayIcon = null;
        }


        private void trayIcon_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
        #endregion


        #region the mini view
        /// <summary>
        /// Hides this window and puts the floating mini view up in its place.
        /// The window's geometry is remembered so that coming back lands
        /// exactly where the user left it.
        /// </summary>
        private void EnterMiniView()
        {
            if (inMiniView)
                return;

            restoreLeft = Left;
            restoreTop = Top;
            restoreWidth = ActualWidth;
            restoreWindowState = WindowState;

            if (miniView == null)
            {
                // Created on first use: nobody should pay for a view they never
                // open.
                miniView = new MiniTimerWindow(activeTimer, settings);
                miniView.RestoreRequested += miniView_RestoreRequested;
            }

            inMiniView = true;

            // The tray icon belongs to the minimize-to-tray feature, not here.
            HideTrayIcon();

            miniView.ShowAt();
            Hide();
        }


        /// <summary>Takes the mini view down and brings this window back as it was.</summary>
        private void ExitMiniView()
        {
            if (!inMiniView)
                return;

            inMiniView = false;

            if (miniView != null)
                miniView.Hide();

            Show();

            WindowState = restoreWindowState == WindowState.Minimized
                ? WindowState.Normal
                : restoreWindowState;

            Left = restoreLeft;
            Top = restoreTop;
            Width = restoreWidth;

            Activate();
        }


        private void miniView_RestoreRequested(object sender, EventArgs e)
        {
            ExitMiniView();
        }
        #endregion


        #region the ticker
        private void ticker_Tick(object sender, EventArgs e)
        {
            bool firstTick = ticker.Interval.TotalMilliseconds == firstDelay;

            ticker.Interval = TimeSpan.FromMilliseconds(defaultDelay);

            UpdateJiraRelatedData(firstTick);
            issues.Refresh();
            activeTimer.Refresh();
            SaveSettingsAndIssueStates();

            if (firstTick)
                CheckForUpdates();
        }
        #endregion


        #region Jira
        private bool IsJiraEnabled
        {
            get { return !string.IsNullOrEmpty(settings.JiraBaseUrl); }
        }


        private void AuthenticateJira(string username, string apiToken)
        {
            AuthenticateJiraAsync(username, apiToken).FireAndForget();
        }


        private async Task AuthenticateJiraAsync(string username, string apiToken)
        {
            lblConnectionStatus.Text = "Connecting...";

            bool authenticated = await Task.Run(() => jiraClient.Authenticate(username, apiToken));

            if (authenticated)
                UpdateSummaries();

            UpdateJiraRelatedData(true);
        }


        private void UpdateJiraRelatedData(bool firstTick)
        {
            UpdateJiraRelatedDataAsync(firstTick).FireAndForget();
        }


        private async Task UpdateJiraRelatedDataAsync(bool firstTick)
        {
            if (!IsJiraEnabled)
            {
                SetConnectionStatus(false);
                return;
            }

            bool valid = jiraClient.SessionValid || await Task.Run(() => jiraClient.ValidateSession());

            SetConnectionStatus(valid);

            if (!valid)
                return;

            if (firstTick)
                LoadFilters();

            UpdateSummaries();
        }


        private void SetConnectionStatus(bool connected)
        {
            lblConnectionStatus.Text = connected ? "Connected" : "Not connected";

            // The band is the accent colour, so connected reads as plain accent
            // text and a failure as the danger colour, which stays legible on
            // it. A failure is also the only one that invites a click.
            lblConnectionStatus.Foreground = connected
                ? (Brush)FindResource("AccentText")
                : ThemeBrushes.ToBrush(Theme.Current.DangerSurface);

            lblConnectionStatus.TextDecorations = connected ? null : TextDecorations.Underline;
            lblConnectionStatus.Cursor = connected ? Cursors.Arrow : Cursors.Hand;
        }


        /// <summary>Re-resolves every row's summary from Jira.</summary>
        private void UpdateSummaries()
        {
            foreach (IssueViewModel issue in issues.Issues)
                UpdateSummary(issue);
        }


        private void UpdateSummary(IssueViewModel issue)
        {
            UpdateSummaryAsync(issue).FireAndForget();
        }


        private async Task UpdateSummaryAsync(IssueViewModel issue)
        {
            string summary = await jiraService.GetSummaryAsync(issue.IssueKey);

            // null means Jira refused the request - leave the summary the row
            // already has.
            if (summary != null)
                issue.Summary = summary;
        }


        private void LoadFilters()
        {
            // Fire and forget: the combo repaints itself from FiltersLoaded
            // once the answer arrives.
            filterProvider.LoadAsync().FireAndForget();
        }


        private void filterProvider_FiltersLoaded(object sender, EventArgs e)
        {
            FilterItem current = filterProvider.Current;

            loadingFilters = true;
            try
            {
                cbFilters.Items.Clear();
                foreach (FilterItem filter in filterProvider.Filters)
                    cbFilters.Items.Add(filter);

                cbFilters.SelectedItem = current;
            }
            finally
            {
                loadingFilters = false;
            }
        }


        private void CheckForUpdates()
        {
            if (!settings.CheckForUpdate)
                return;

            CheckForUpdatesAsync().FireAndForget();
        }


        private async Task CheckForUpdatesAsync()
        {
            GithubRelease latestRelease = await Task.Run(() => ReleaseHelper.GetLatestVersion());
            if (latestRelease == null)
                return;

            string currentVersion = AppInfo.Version;
            if (string.Compare(latestRelease.TagName, currentVersion) <= 0)
                return;

            string msg = string.Format("There is a newer version available of Jira StopWatch.{0}{0}Latest release is {1}. You are running version {2}.{0}{0}Do you want to download latest release?",
                Environment.NewLine,
                latestRelease.TagName,
                currentVersion);

            if (MessageBox.Show(this, msg, "New version available", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                AppInfo.OpenUrl("https://github.com/jirastopwatch/jirastopwatch/releases/latest");
        }


        private void ChangeIssueState(string issueKey)
        {
            if (string.IsNullOrWhiteSpace(settings.StartTransitions))
                return;

            ChangeIssueStateAsync(issueKey).FireAndForget();
        }


        private async Task ChangeIssueStateAsync(string issueKey)
        {
            string[] startTransitions = settings.StartTransitions
                .Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().ToLower()).ToArray();

            await Task.Run(() =>
            {
                AvailableTransitions available = jiraClient.GetAvailableTransitions(issueKey);
                if (available == null || available.Transitions.Count() == 0)
                    return;

                foreach (Transition t in available.Transitions)
                {
                    if (startTransitions.Any(t.Name.ToLower().Contains))
                    {
                        jiraClient.DoTransition(issueKey, t.Id);
                        return;
                    }
                }
            });
        }
        #endregion


        #region top and bottom bar handlers
        private void cbFilters_DropDownOpened(object sender, EventArgs e)
        {
            LoadFilters();
        }


        private void cbFilters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Repainting the combo from the provider is not the user choosing.
            if (loadingFilters)
                return;

            filterProvider.Current = cbFilters.SelectedItem as FilterItem;
        }


        private void lblConnectionStatus_Click(object sender, MouseButtonEventArgs e)
        {
            if (jiraClient.SessionValid)
                return;

            string msg = string.Format("Jira StopWatch could not connect to your Jira server. Error returned:{0}{0}{1}", Environment.NewLine, jiraClient.ErrorMessage);
            MessageBox.Show(this, msg, "Connection error");
        }


        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            EditSettings();
        }


        private void btnMiniView_Click(object sender, RoutedEventArgs e)
        {
            EnterMiniView();
        }


        private void btnHelp_Click(object sender, RoutedEventArgs e)
        {
            AppInfo.OpenUrl("http://jirastopwatch.com/doc");
        }


        private void btnAddIssue_Click(object sender, RoutedEventArgs e)
        {
            IssueAdd();
        }
        #endregion


        #region row handlers
        /// <summary>The row a control inside a row template belongs to.</summary>
        private static IssueViewModel RowOf(object sender)
        {
            FrameworkElement element = sender as FrameworkElement;
            return element == null ? null : element.DataContext as IssueViewModel;
        }


        private void Row_Selected(object sender, RoutedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            if (issue != null)
                issues.SetCurrent(issue);
        }


        /// <summary>
        /// Same as Row_Selected, for the mouse events. A separate method
        /// because MouseLeftButtonUp wants a MouseButtonEventHandler, and a
        /// RoutedEventHandler will not bind to it.
        /// </summary>
        private void Row_MouseSelected(object sender, MouseButtonEventArgs e)
        {
            Row_Selected(sender, e);
        }


        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            issues.SetCurrent(issue);
            OpenInBrowser(issue);
        }


        private void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            issues.SetCurrent(issue);
            TogglePlay(issue);
        }


        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            issues.SetCurrent(issue);
            ResetTimer(issue);
        }


        private void btnPost_Click(object sender, RoutedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            issues.SetCurrent(issue);
            PostWorklog(issue);
        }


        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            RemoveIssue(RowOf(sender));
        }


        /// <summary>
        /// A double click on the time field opens the edit dialog. Border has
        /// no MouseDoubleClick of its own - that is a Control event - so the
        /// click count is read off the button press.
        /// </summary>
        private void Time_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            IssueViewModel issue = RowOf(sender);
            issues.SetCurrent(issue);
            EditTime(issue);
            e.Handled = true;
        }


        private void cbJira_DropDownOpened(object sender, EventArgs e)
        {
            LoadIssues(RowOf(sender));
        }


        private void cbJira_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            Issue picked = combo.SelectedItem as Issue;
            if (picked == null)
                return;

            IssueViewModel issue = RowOf(sender);
            if (issue == null)
                return;

            issues.SetCurrent(issue);
            issue.IssueKey = picked.Key;
            UpdateSummary(issue);
        }


        private void cbJira_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateSummary(RowOf(sender));
        }


        private void cbJira_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            ComboBox combo = (ComboBox)sender;
            IssueViewModel issue = RowOf(sender);
            if (issue == null)
                return;

            issue.IssueKey = combo.Text;
            UpdateSummary(issue);
            e.Handled = true;
        }
        #endregion


        #region actions, one per shortcut
        private void RegisterCommands()
        {
            Bind(StopWatchCommands.SelectPrevious, () => { issues.SelectPrevious(); BringCurrentIntoView(); });
            Bind(StopWatchCommands.SelectNext, () => { issues.SelectNext(); BringCurrentIntoView(); });
            Bind(StopWatchCommands.TogglePlay, () => TogglePlay(issues.Current));
            Bind(StopWatchCommands.PostWorklog, () => PostWorklog(issues.Current));
            Bind(StopWatchCommands.EditTime, () => EditTime(issues.Current));
            Bind(StopWatchCommands.ResetTimer, () => ResetTimer(issues.Current));
            Bind(StopWatchCommands.RemoveIssue, () => RemoveIssue(issues.Current));
            Bind(StopWatchCommands.FocusKey, () => FocusKey(issues.Current));
            Bind(StopWatchCommands.AddIssue, IssueAdd);
            Bind(StopWatchCommands.CopyKey, () => CopyKey(issues.Current));
            Bind(StopWatchCommands.PasteKey, () => PasteKey(issues.Current));
            Bind(StopWatchCommands.OpenInBrowser, () => OpenInBrowser(issues.Current));
            Bind(StopWatchCommands.OpenKeyList, () => OpenKeyList(issues.Current));
        }


        private void Bind(RoutedUICommand command, Action action)
        {
            CommandBindings.Add(new CommandBinding(command, (s, e) => { action(); e.Handled = true; }));
        }


        private void TogglePlay(IssueViewModel issue)
        {
            if (issue == null)
                return;

            issue.StartStop();
            activeTimer.Refresh();
        }


        private void ResetTimer(IssueViewModel issue)
        {
            if (issue == null)
                return;

            issue.Comment = null;
            issue.EstimateUpdateMethod = EstimateUpdateMethods.Auto;
            issue.EstimateUpdateValue = null;
            issue.Reset();

            issues.NotifyReset(issue);
            activeTimer.Refresh();
        }


        private void IssueAdd()
        {
            if (issues.Add() == null)
                return;

            UpdateAddIssueTooltip();
            ClampHeightToWorkingArea();
            BringCurrentIntoView();
        }


        private void RemoveIssue(IssueViewModel issue)
        {
            if (issue == null || !issues.CanRemove)
                return;

            issues.Remove(issue);
            UpdateAddIssueTooltip();
            activeTimer.Refresh();
        }


        private void FocusKey(IssueViewModel issue)
        {
            ComboBox combo = KeyFieldOf(issue);
            if (combo != null)
                combo.Focus();
        }


        private void OpenKeyList(IssueViewModel issue)
        {
            ComboBox combo = KeyFieldOf(issue);
            if (combo == null)
                return;

            combo.Focus();
            combo.IsDropDownOpen = true;
        }


        private void CopyKey(IssueViewModel issue)
        {
            if (issue == null || string.IsNullOrEmpty(issue.IssueKey))
                return;

            Clipboard.SetText(issue.IssueKey);
        }


        private void PasteKey(IssueViewModel issue)
        {
            if (issue == null || !Clipboard.ContainsText())
                return;

            issue.IssueKey = JiraKeyHelpers.ParseUrlToKey(Clipboard.GetText());
            UpdateSummary(issue);
        }


        private void OpenInBrowser(IssueViewModel issue)
        {
            if (issue == null || !issue.CanOpen)
                return;

            if (string.IsNullOrEmpty(settings.JiraBaseUrl))
                return;

            string url = settings.JiraBaseUrl;
            if (!url.EndsWith("/"))
                url += "/";
            url += "browse/" + issue.IssueKey.Trim();

            AppInfo.OpenUrl(url);
        }


        private void LoadIssues(IssueViewModel issue)
        {
            LoadIssuesAsync(issue).FireAndForget();
        }


        private async Task LoadIssuesAsync(IssueViewModel issue)
        {
            if (issue == null)
                return;

            // The active filter's JQL is handed over rather than looked up by
            // walking the window's controls, which is what the row used to do.
            var available = await jiraService.GetIssuesAsync(filterProvider.CurrentJql);

            if (available.Count > 0)
                issue.AvailableIssues = available;
        }
        #endregion


        #region dialogs
        private void EditSettings()
        {
            ThemeMode themeBefore = settings.Theme;
            int maxIssuesBefore = settings.MaxIssues;

            var dialog = new SettingsWindow(settings) { Owner = this };
            if (dialog.ShowDialog() != true)
                return;

            settings.Save();

            restClientFactory.BaseUrl = settings.JiraBaseUrl;

            if (settings.Theme != themeBefore)
            {
                Theme.Current = Theme.ForMode(settings.Theme);
                ApplyTheme();
            }

            Topmost = settings.AlwaysOnTop;

            if (settings.MaxIssues != maxIssuesBefore)
                UpdateAddIssueTooltip();

            ClampHeightToWorkingArea();

            if (IsJiraEnabled)
                AuthenticateJira(settings.Username, settings.ApiToken);
        }


        private void EditTime(IssueViewModel issue)
        {
            if (issue == null)
                return;

            var dialog = new EditTimeWindow(issue.WatchTimer.TimeElapsed) { Owner = this };
            if (dialog.ShowDialog() != true)
                return;

            issue.SetTimeElapsed(dialog.Time);
            issues.Refresh();
            activeTimer.Refresh();
        }


        private void PostWorklog(IssueViewModel issue)
        {
            if (issue == null || !issue.CanPost)
                return;

            var dialog = new WorklogWindow(
                issue.WatchTimer.GetInitialStartTime(),
                issue.WatchTimer.TimeElapsedNearestMinute,
                issue.Comment,
                issue.EstimateUpdateMethod,
                issue.EstimateUpdateValue) { Owner = this };

            // Deliberately not awaited: the dialog has to come up right away,
            // and the estimate fills itself in once Jira answers.
            FillRemainingEstimateAsync(dialog, issue.IssueKey).FireAndForget();

            dialog.ShowDialog();

            if (dialog.Result == WorklogResult.Post)
            {
                issue.Comment = dialog.Comment.Trim();
                issue.EstimateUpdateMethod = dialog.EstimateUpdateMethod;
                issue.EstimateUpdateValue = dialog.EstimateValue;

                PostWorklogAsync(issue, dialog.InitialStartTime).FireAndForget();
            }
            else if (dialog.Result == WorklogResult.SaveForLater)
            {
                issue.Comment = string.Format("{0}:{1}{2}", DateTime.Now.ToString("g"), Environment.NewLine, dialog.Comment.Trim());
                issue.EstimateUpdateMethod = dialog.EstimateUpdateMethod;
                issue.EstimateUpdateValue = dialog.EstimateValue;
            }
        }


        private async Task FillRemainingEstimateAsync(WorklogWindow dialog, string key)
        {
            RemainingEstimate estimate = await jiraService.GetRemainingEstimateAsync(key);

            if (estimate.Seconds < 0)
                return;

            dialog.SetRemainingEstimate(estimate.Text, estimate.Seconds);
        }


        private async Task PostWorklogAsync(IssueViewModel issue, DateTimeOffset startTime)
        {
            Cursor previous = Cursor;
            Cursor = Cursors.Wait;

            try
            {
                PostWorklogResult result = await jiraService.PostWorklogAsync(
                    issue.IssueKey,
                    startTime,
                    issue.WatchTimer.TimeElapsedNearestMinute,
                    issue.Comment,
                    issue.EstimateUpdateMethod,
                    issue.EstimateUpdateValue);

                if (result.Success)
                    ResetTimer(issue);
            }
            finally
            {
                Cursor = previous;
            }
        }
        #endregion


        #region private methods
        private void issues_TimerStarted(object sender, IssueViewModel issue)
        {
            ChangeIssueState(issue.IssueKey);

            if (!settings.AllowMultipleTimers)
                issues.PauseAllBut(issue);

            activeTimer.NotifyTimerStarted(issue);
        }


        /// <summary>
        /// Says why the add button will not do anything once the list is full.
        /// The button stays enabled on purpose: a disabled one shows no tooltip,
        /// and the tooltip is the whole explanation.
        /// </summary>
        private void UpdateAddIssueTooltip()
        {
            if (issues.CanAdd)
            {
                btnAddIssue.ToolTip = "Add another issue row (CTRL-N)";
                btnAddIssue.Cursor = Cursors.Hand;
            }
            else
            {
                btnAddIssue.ToolTip = string.Format("You have reached the max limit of {0} issues and cannot add another", settings.MaxIssues);
                btnAddIssue.Cursor = Cursors.No;
            }
        }


        private void SaveSettingsAndIssueStates()
        {
            issues.Persist();
            settings.Save();
        }


        /// <summary>Scrolls the selected row into view, as the panel used to.</summary>
        private void BringCurrentIntoView()
        {
            IssueViewModel current = issues.Current;
            if (current == null)
                return;

            FrameworkElement container = ContainerOf(current);
            if (container != null)
                container.BringIntoView();
        }


        private ComboBox KeyFieldOf(IssueViewModel issue)
        {
            FrameworkElement container = ContainerOf(issue);
            return container == null ? null : Descendants<ComboBox>(container).FirstOrDefault();
        }


        private FrameworkElement ContainerOf(IssueViewModel issue)
        {
            if (issue == null)
                return null;

            return issueList.ItemContainerGenerator.ContainerFromItem(issue) as FrameworkElement;
        }


        /// <summary>
        /// Every descendant of a given type. Needed because the rows are built
        /// from a template, so their controls have no field to reach them by.
        /// </summary>
        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
                yield break;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);

                T match = child as T;
                if (match != null)
                    yield return match;

                foreach (T descendant in Descendants<T>(child))
                    yield return descendant;
            }
        }

        #endregion


        #region private members
        private readonly Settings settings;

        private readonly JiraApiRequestFactory jiraApiRequestFactory;
        private readonly RestClientFactory restClientFactory;
        private readonly JiraClient jiraClient;
        private readonly IssueJiraService jiraService;
        private readonly FilterProvider filterProvider;

        private readonly IssueListViewModel issues;
        private readonly ActiveTimerViewModel activeTimer;

        private readonly System.Windows.Threading.DispatcherTimer ticker;

        private System.Windows.Forms.NotifyIcon trayIcon;

        private MiniTimerWindow miniView;
        private bool inMiniView;
        private double restoreLeft;
        private double restoreTop;
        private double restoreWidth;
        private WindowState restoreWindowState = WindowState.Normal;

        private IssueViewModel lastRunningIssue;

        private bool loadingFilters;
        #endregion


        #region private consts
        private const int firstDelay = 500;
        private const int defaultDelay = 30000;
        #endregion
    }
}
