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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using StopWatch.Update;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;
using Screen = System.Windows.Forms.Screen;

namespace StopWatch
{
    /// <summary>
    /// The main window: the list of issue rows, and the status bar with the
    /// connection state, the mini-view/settings/help actions, the add button
    /// and the total.
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

            issues = new IssueListViewModel(settings);
            issues.TimerStarted += issues_TimerStarted;

            activeTimer = new ActiveTimerViewModel(() => issues.Issues.Cast<ITimerSource>());

            updateService = new AutoUpdateService(new GitHubReleaseSource());

            InitializeComponent();

            // Has to happen before the window is ever shown, not just in
            // Loaded: with SizeToContent="Height" and no Width assigned yet,
            // WPF's first measure pass is unconstrained on both axes, so it
            // picks up the issue row template's own natural (unconstrained)
            // width instead of leaving Width alone for Height-only auto-size.
            // Giving Width a real value before that first pass is what makes
            // SizeToContent stay Height-only. See design.md D7.
            RestoreWidth();

            Title = $"Jira StopWatch v{AppInfo.Version}";

            DataContext = issues;

            RegisterCommands();

            ticker = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Normal, Dispatcher);
            ticker.Interval = TimeSpan.FromMilliseconds(firstDelay);
            ticker.Tick += ticker_Tick;

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;
            StateChanged += MainWindow_StateChanged;

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
            NativeMethods.SetTitleBarDarkMode(this, Theme.Current.Mode == StopWatch.ThemeMode.Dark);

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
            // Launching the app again while the mini view (or the taskbar
            // widget) is up means the user wants the full window, not a
            // second stand-in next to it.
            if (inMiniView)
            {
                ExitMiniView();
                return;
            }

            if (inTaskbarWidget)
            {
                ExitTaskbarWidget();
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

            // The constructor's own ApplyTheme() call ran before the window
            // had a handle, so the title bar's dark-mode flag never reached
            // DWM then; now that SourceInitialized has fired, it can.
            NativeMethods.SetTitleBarDarkMode(this, Theme.Current.Mode == StopWatch.ThemeMode.Dark);
        }


        private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_SHOWME)
                ShowOnTop();

            return IntPtr.Zero;
        }


        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Re-clamps against the screen the window actually ended up on
            // (WorkingArea only knows that once IsLoaded is true). The
            // constructor's own call already gave Width a real value before
            // the first SizeToContent measure pass - this just corrects it
            // if that screen turns out narrower than the primary one assumed
            // back then.
            RestoreWidth();

            RestoreLocation();
            ClampHeightToWorkingArea();

            // Applied after the position restore: a maximized window's exact
            // pixel geometry is decided by Windows for whichever monitor it
            // ends up on, not by the remembered Left/Top.
            if (settings.MainWindowMaximized)
            {
                // Set here, not left to MainWindow_StateChanged's own
                // SizeToContent=Manual: an interactive maximize (the taskbar
                // button) has Windows fill the screen natively before
                // StateChanged ever fires, so that handler's job there is
                // only to stop the next layout pass shrinking it back down.
                // A WindowState assigned from code has no such native resize
                // to piggyback on - WPF computes the target size itself, in
                // the same step, still using SizeToContent="Height" unless
                // it is already Manual by then. Setting it first, ourselves,
                // is what makes this startup path actually fill the screen
                // instead of landing at the remembered Normal size with
                // Maximized chrome.
                SizeToContent = SizeToContent.Manual;
                WindowState = WindowState.Maximized;
            }

            Topmost = settings.AlwaysOnTop;
            UpdateMiniViewButtonVisibility();

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

            lastUpdateCheckUtc = DateTime.UtcNow;
            CheckForUpdatesAsync().FireAndForget();
        }


        /// <summary>
        /// Captures Normal-state geometry while the window (and its HWND)
        /// still exists. <c>Closed</c> fires after the HWND is already torn
        /// down, at which point <c>RestoreBounds</c> - a live Win32 query -
        /// has nothing left to answer from; this is why the same capture
        /// inside <see cref="SaveSettingsAndIssueStates"/> is not enough on
        /// its own for the final save on exit, only for the periodic one.
        /// </summary>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            RememberNormalGeometry();
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

            if (taskbarWidget != null)
            {
                taskbarWidget.ClosedByApp = true;
                taskbarWidget.Close();
                taskbarWidget = null;
            }

            DisposeTrayIcon();
            SaveSettingsAndIssueStates();

            // Applying an update only ever happens on this, a normal exit -
            // never while the app is running, never by force-closing it. See
            // the auto-update spec, "La actualización se aplica sin
            // interrumpir una sesión en curso".
            if (pendingUpdate != null)
                UpdateApplier.ApplyOnExit(pendingUpdate, AppContext.BaseDirectory);
        }


        /// <summary>
        /// Checks for, downloads and stages a newer release in the
        /// background. A no-op, logged rather than surfaced, on any failure -
        /// see the auto-update spec, "El chequeo falla".
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            string stagingBaseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StopWatch", "updates");

            PendingUpdate update = await updateService.CheckAndStageAsync(settings.CheckForUpdates, AppInfo.Version, AppInfo.IsSelfContained, stagingBaseDir);
            if (update == null)
                return;

            pendingUpdate = update;
            lblUpdateReady.Text = $"v{update.Version} ready — restart to apply";
            lblUpdateReady.Visibility = Visibility.Visible;

            hlUpdateWhatsNew.NavigateUri = new Uri(update.ReleaseUrl);
            lblUpdateWhatsNew.Visibility = Visibility.Visible;
        }


        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            AppInfo.OpenUrl(e.Uri.ToString());
            e.Handled = true;
        }


        /// <summary>
        /// The mini-view toolbar button only makes sense when minimizing
        /// doesn't already go to the taskbar widget - otherwise clicking it
        /// would show the same running timer twice at once (widget + mini
        /// view). Called at load and whenever MinimizeBehavior changes in
        /// Settings.
        /// </summary>
        private void UpdateMiniViewButtonVisibility()
        {
            btnMiniView.Visibility = settings.MinimizeBehavior == MinimizeBehavior.TaskbarWidget
                ? Visibility.Collapsed
                : Visibility.Visible;
        }


        /// <summary>
        /// Captures the window's Normal-state width and position, right
        /// before <see cref="Settings.Save"/> - called from
        /// <see cref="SaveSettingsAndIssueStates"/>, i.e. every ticker tick
        /// and on close, the same cadence width-saving already used before
        /// this existed.
        ///
        /// Reads <see cref="RestoreBounds"/> rather than
        /// <c>ActualWidth</c>/<c>Left</c>/<c>Top</c> directly: those reflect
        /// whatever the window's CURRENT state is, which is wrong while
        /// maximized (screen-filling size, and Windows' maximized
        /// coordinates run a few pixels past the working area for the
        /// invisible resize border). <c>RestoreBounds</c> is WPF's live view
        /// of Win32's own <c>WINDOWPLACEMENT.rcNormalPosition</c> - the
        /// "where this window would be if it were Normal" the OS itself
        /// maintains, correct regardless of whether the window is currently
        /// Normal, Maximized or Minimized, and not subject to a
        /// maximize-transition's own transient geometry. See design.md D9.
        /// </summary>
        private void RememberNormalGeometry()
        {
            Rect normal = RestoreBounds;
            if (normal.IsEmpty)
                return;

            double scaleX;
            double scaleY;
            GetScale(out scaleX, out scaleY);

            settings.MainWindowWidth = (int)Math.Round(normal.Width);
            settings.MainWindowLocation = ScreenPlacement.FormatLocation(
                new DrawingPoint((int)Math.Round(normal.Left * scaleX), (int)Math.Round(normal.Top * scaleY)));
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
        /// Restores the remembered position, validated against the screen
        /// that is actually there - same problem the width and the mini
        /// view's position have, so the same helper answers it. Left
        /// untouched if nothing was saved yet (empty or malformed value,
        /// e.g. first run).
        /// </summary>
        private void RestoreLocation()
        {
            DrawingPoint desired;
            if (!ScreenPlacement.TryParseLocation(settings.MainWindowLocation, out desired))
                return;

            double scaleX;
            double scaleY;
            GetScale(out scaleX, out scaleY);

            DrawingSize size = new DrawingSize(
                (int)Math.Ceiling(Width * scaleX),
                (int)Math.Ceiling((ActualHeight > 0 ? ActualHeight : Height) * scaleY));

            DrawingPoint onScreen = ScreenPlacement.EnsureOnScreen(desired, size);

            Left = onScreen.X / scaleX;
            Top = onScreen.Y / scaleY;
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
        /// WPF has no equivalent of MainForm_Resize, so the window's own state
        /// drives what minimizing does: routes to the tray icon or to
        /// EnterMiniView() depending on settings.MinimizeBehavior.
        /// </summary>
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (!CrossPlatformHelpers.IsWindowsEnvironment())
                return;

            // While the mini view or the taskbar widget is up this window is
            // hidden on purpose. The tray icon must not appear as well: two
            // stand-ins for one hidden window is one too many.
            if (inMiniView || inTaskbarWidget)
                return;

            if (WindowState == WindowState.Minimized)
            {
                if (settings.MinimizeBehavior == MinimizeBehavior.MiniView)
                {
                    EnterMiniView();
                }
                else if (settings.MinimizeBehavior == MinimizeBehavior.TaskbarWidget)
                {
                    EnterTaskbarWidget();
                }
                else
                {
                    ShowTrayIcon();
                    Hide();
                }
            }
            else if (WindowState == WindowState.Normal)
            {
                HideTrayIcon();

                // Reinstate auto-height now that the window is done being
                // maximized - see the Maximized branch below for why it was
                // turned off.
                SizeToContent = SizeToContent.Height;

                RememberMaximized(false);
            }
            else if (WindowState == WindowState.Maximized)
            {
                // WPF re-applies SizeToContent after a maximize, which shrinks
                // the window straight back down to its content size instead
                // of filling the screen - a known WPF quirk, not specific to
                // this window. Switching to Manual before that layout pass
                // runs is what makes Maximized actually fill the screen.
                SizeToContent = SizeToContent.Manual;

                RememberMaximized(true);
            }
        }


        /// <summary>
        /// Persists maximized/normal, guarded by <c>IsLoaded</c> so applying
        /// the remembered state at startup doesn't immediately overwrite
        /// itself. Minimized is never passed in here - see the
        /// main-window-placement spec, "Minimizing does not change the
        /// remembered state".
        /// </summary>
        private void RememberMaximized(bool maximized)
        {
            if (!IsLoaded)
                return;

            settings.MainWindowMaximized = maximized;
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

            // The mini view ticks every second; this window's own ticker
            // only every 30 (it doubles as the Jira poll). Without this, the
            // row coming back into view can lag behind what the mini view
            // just showed by up to that long.
            IssueViewModel active = activeTimer.ActiveSource as IssueViewModel;
            if (active != null)
                active.Refresh();

            Show();

            WindowState = restoreWindowState == WindowState.Minimized
                ? WindowState.Normal
                : restoreWindowState;

            RestorePosition();

            Activate();
        }


        /// <summary>
        /// Applies the remembered position and width, after checking the
        /// position still falls on a connected screen - the screen this
        /// window was on may have been disconnected while the mini view was
        /// up. Same mechanism <c>MiniTimerWindow.ShowAt()</c> already uses for
        /// its own remembered position.
        /// </summary>
        private void RestorePosition()
        {
            double scaleX;
            double scaleY;
            GetScale(out scaleX, out scaleY);

            DrawingSize size = new DrawingSize(
                (int)Math.Ceiling(restoreWidth * scaleX),
                (int)Math.Ceiling(ActualHeight * scaleY));

            DrawingPoint desired = new DrawingPoint(
                (int)Math.Round(restoreLeft * scaleX),
                (int)Math.Round(restoreTop * scaleY));

            DrawingPoint onScreen = ScreenPlacement.EnsureOnScreen(desired, size);

            Left = onScreen.X / scaleX;
            Top = onScreen.Y / scaleY;
            Width = restoreWidth;
        }


        /// <summary>Device pixels per device-independent unit, for the screen this window is currently on.</summary>
        private void GetScale(out double scaleX, out double scaleY)
        {
            scaleX = 1.0;
            scaleY = 1.0;

            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;

                if (scaleX <= 0)
                    scaleX = 1.0;
                if (scaleY <= 0)
                    scaleY = 1.0;
            }
        }


        private void miniView_RestoreRequested(object sender, EventArgs e)
        {
            ExitMiniView();
        }
        #endregion


        #region the taskbar widget
        /// <summary>
        /// Hides this window and shows the taskbar widget in its place, on
        /// the single monitor <see cref="Settings.TaskbarWidgetMonitor"/>
        /// names - or the primary monitor if that one is not currently
        /// connected. See the taskbar-widget-view spec.
        /// </summary>
        private void EnterTaskbarWidget()
        {
            if (inTaskbarWidget)
                return;

            restoreLeft = Left;
            restoreTop = Top;
            restoreWidth = ActualWidth;
            restoreWindowState = WindowState;

            inTaskbarWidget = true;

            // The tray icon belongs to the minimize-to-tray feature, not here.
            HideTrayIcon();

            EnsureTaskbarWidget();
            taskbarWidget.ShowAt();

            Hide();
        }


        /// <summary>Takes the taskbar widget down and brings this window back as it was.</summary>
        private void ExitTaskbarWidget()
        {
            if (!inTaskbarWidget)
                return;

            inTaskbarWidget = false;

            if (taskbarWidget != null)
                taskbarWidget.StopAndHide();

            Show();

            WindowState = restoreWindowState == WindowState.Minimized
                ? WindowState.Normal
                : restoreWindowState;

            RestorePosition();

            Activate();
        }


        /// <summary>Creates the widget window on first use.</summary>
        private void EnsureTaskbarWidget()
        {
            if (taskbarWidget != null)
                return;

            taskbarWidget = new TaskbarWidgetWindow(activeTimer, settings, ResolveTargetTray);
            taskbarWidget.RestoreRequested += TaskbarWidget_RestoreRequested;
            taskbarWidget.ExitRequested += TaskbarWidget_ExitRequested;
            taskbarWidget.Died += TaskbarWidget_Died;
        }


        /// <summary>
        /// The taskbar to anchor to, re-resolved fresh on every call (the
        /// widget polls this): the user's chosen monitor if it currently
        /// resolves to a connected taskbar, otherwise the primary monitor's.
        /// Re-resolving from scratch each time is what makes a disconnected
        /// monitor recover on its own, without any change-notification
        /// plumbing - the very next poll just starts resolving to the
        /// primary taskbar instead.
        /// </summary>
        private IntPtr ResolveTargetTray()
        {
            int configured = settings.TaskbarWidgetMonitor;
            if (configured > 0)
            {
                List<IntPtr> secondaries = TaskbarInterop.GetSecondaryTrays();
                if (configured - 1 < secondaries.Count)
                    return secondaries[configured - 1];
            }

            return TaskbarInterop.GetPrimaryTray();
        }


        private void TaskbarWidget_RestoreRequested(object sender, EventArgs e)
        {
            ExitTaskbarWidget();
        }


        private void TaskbarWidget_ExitRequested(object sender, EventArgs e)
        {
            Close();
        }


        /// <summary>
        /// The widget window's HWND was destroyed along with its owning
        /// taskbar (an Explorer restart) rather than by the app - recreate it
        /// so the feature survives that transient.
        /// </summary>
        private void TaskbarWidget_Died(object sender, EventArgs e)
        {
            taskbarWidget = null;

            if (inTaskbarWidget)
            {
                EnsureTaskbarWidget();
                taskbarWidget.ShowAt();
            }
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

            // Re-check for a release published mid-session. Skipped once an
            // update is already staged - no point re-checking while it just
            // sits waiting for the next normal close.
            if (pendingUpdate == null && DateTime.UtcNow - lastUpdateCheckUtc >= updateCheckInterval)
            {
                lastUpdateCheckUtc = DateTime.UtcNow;
                CheckForUpdatesAsync().FireAndForget();
            }
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

            UpdateSummaries();
        }


        private void SetConnectionStatus(bool connected)
        {
            lblConnectionStatus.Text = connected ? "Connected" : "Not connected";

            // Sitting in the plain status bar now, not the accent band, so the
            // state colours are the same ones a row uses for success/failure.
            lblConnectionStatus.Foreground = ThemeBrushes.ToBrush(
                connected ? Theme.Current.Success : Theme.Current.Danger);

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
            // Null happens: LostFocus can still fire for a row's key field
            // while its container is being torn down (removed via Ctrl+Delete,
            // for instance), by which point RowOf(sender) reads a DataContext
            // that has already been cleared.
            if (issue == null)
                return;

            UpdateSummaryAsync(issue).FireAndForget();
        }


        private async Task UpdateSummaryAsync(IssueViewModel issue)
        {
            IssueSummaryResult result = await jiraService.GetSummaryAsync(issue.IssueKey);

            // null means Jira refused the request - leave the summary the row
            // already has.
            if (result != null)
            {
                issue.Summary = result.Summary;
                issue.ParentKey = result.ParentKey;
            }
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


        #region status bar handlers
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


        private void btnCopyKey_Click(object sender, RoutedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            issues.SetCurrent(issue);
            CopyKey(issue);
            GlyphButtonHelpers.FlashCopyConfirmation((Button)sender);
        }


        private void btnCopyParentKey_Click(object sender, RoutedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            issues.SetCurrent(issue);
            CopyParentKey(issue);
            GlyphButtonHelpers.FlashCopyConfirmation((Button)sender);
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


        private void btnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            issues.SetCurrent(issue);
            issues.MoveUp(issue);
        }


        private void btnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            issues.SetCurrent(issue);
            issues.MoveDown(issue);
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


        private void tbIssueKey_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateSummary(RowOf(sender));
        }


        private void tbIssueKey_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            TextBox field = (TextBox)sender;
            IssueViewModel issue = RowOf(sender);
            if (issue == null)
                return;

            issue.IssueKey = field.Text;
            UpdateSummary(issue);
            e.Handled = true;
        }


        /// <summary>
        /// Backs the key field's own Paste command, which its right-click
        /// context menu invokes directly - unlike Ctrl+V, that never reaches
        /// MainWindow_PreviewKeyDown, so without this the context menu would
        /// paste the raw clipboard text instead of parsing a URL to a key.
        /// </summary>
        private void tbIssueKey_PasteCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Clipboard.ContainsText();
            e.Handled = true;
        }


        private void tbIssueKey_PasteExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            PasteKey(RowOf(sender));
            e.Handled = true;
        }
        #endregion


        #region actions, one per shortcut
        /// <summary>
        /// Six of the thirteen shortcuts share their gesture with a built-in
        /// TextBox editing command - Ctrl+Up/Down (move by paragraph), Ctrl+C/V
        /// (copy/paste the selection), Ctrl+Delete (delete next word) and
        /// Ctrl+I (toggle italic). Ctrl+Shift+Up/Down (MoveUp/MoveDown) share
        /// theirs too - TextBox maps that gesture to "extend selection by
        /// paragraph". All of those claim the key first whenever the
        /// issue-key field has focus and mark it handled, even though a
        /// single-line field has no paragraph to move by, select by, or
        /// formatting to toggle - so the CommandBindings below never see the
        /// keystroke.
        ///
        /// Catching them here, at the window's Preview (tunnelling) stage,
        /// wins the race: the event reaches the window before it reaches the
        /// focused TextBox, so this runs first regardless of what has focus.
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Up:
                        issues.SelectPrevious();
                        BringCurrentIntoView();
                        break;

                    case Key.Down:
                        issues.SelectNext();
                        BringCurrentIntoView();
                        break;

                    case Key.C:
                        CopyKey(issues.Current);
                        break;

                    case Key.V:
                        PasteKey(issues.Current);
                        break;

                    case Key.Delete:
                        RemoveIssue(issues.Current);
                        break;

                    case Key.I:
                        FocusKey(issues.Current);
                        break;

                    default:
                        return;
                }

                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                switch (e.Key)
                {
                    case Key.Up:
                        issues.MoveUp(issues.Current);
                        BringCurrentIntoView();
                        break;

                    case Key.Down:
                        issues.MoveDown(issues.Current);
                        BringCurrentIntoView();
                        break;

                    default:
                        return;
                }

                e.Handled = true;
            }
        }


        private void RegisterCommands()
        {
            Bind(StopWatchCommands.SelectPrevious, () => { issues.SelectPrevious(); BringCurrentIntoView(); });
            Bind(StopWatchCommands.SelectNext, () => { issues.SelectNext(); BringCurrentIntoView(); });
            Bind(StopWatchCommands.MoveUp, () => { issues.MoveUp(issues.Current); BringCurrentIntoView(); });
            Bind(StopWatchCommands.MoveDown, () => { issues.MoveDown(issues.Current); BringCurrentIntoView(); });
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
            issues.NotifyRunningSetMayHaveChanged();
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
            TextBox field = KeyFieldOf(issue);
            if (field != null)
                field.Focus();
        }


        private void CopyKey(IssueViewModel issue)
        {
            if (issue == null || string.IsNullOrEmpty(issue.IssueKey))
                return;

            Clipboard.SetText(issue.IssueKey);
        }


        private void CopyParentKey(IssueViewModel issue)
        {
            if (issue == null || !issue.HasParent)
                return;

            Clipboard.SetText(issue.ParentKey);
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

            string url = JiraKeyHelpers.BuildIssueUrl(settings.JiraBaseUrl, issue.IssueKey);
            if (url == null)
                return;

            AppInfo.OpenUrl(url);
        }


        #endregion


        #region dialogs
        private void EditSettings()
        {
            ThemeMode themeBefore = settings.Theme;
            int maxIssuesBefore = settings.MaxIssues;
            ListDensity densityBefore = settings.ListDensity;
            TimeDisplayFormat timeDisplayFormatBefore = settings.TimeDisplayFormat;

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
            UpdateMiniViewButtonVisibility();

            if (settings.MaxIssues != maxIssuesBefore)
                UpdateAddIssueTooltip();

            issues.NotifyRunningSetMayHaveChanged();

            // The dialog wrote the new density straight to Settings, bypassing
            // IssueListViewModel.Density's own change notification, so the
            // list has to be told explicitly or it keeps the old row template
            // until the app restarts.
            if (settings.ListDensity != densityBefore)
                issues.NotifyDensityChanged();

            if (settings.TimeDisplayFormat != timeDisplayFormatBefore)
                issues.NotifyTimeDisplayFormatChanged();

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
            {
                issues.PauseAllBut(issue);
            }
            else if (issues.Running.Count() > settings.MaxConcurrentTimers)
            {
                // Reached the configured cap: undo the start rather than let
                // one more timer run, the same after-the-fact pattern as the
                // single-timer rule above. The issue was never really
                // "started" from the rest of the app's point of view, so
                // activeTimer does not need to hear about it.
                issue.Pause();
                issues.NotifyRunningSetMayHaveChanged();
                return;
            }

            activeTimer.NotifyTimerStarted(issue);
            issues.NotifyRunningSetMayHaveChanged();
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
            RememberNormalGeometry();
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


        private TextBox KeyFieldOf(IssueViewModel issue)
        {
            FrameworkElement container = ContainerOf(issue);
            return container == null ? null : Descendants<TextBox>(container).FirstOrDefault();
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

        private readonly IssueListViewModel issues;
        private readonly ActiveTimerViewModel activeTimer;

        private readonly AutoUpdateService updateService;
        private PendingUpdate pendingUpdate;
        private DateTime lastUpdateCheckUtc;

        private readonly System.Windows.Threading.DispatcherTimer ticker;

        private System.Windows.Forms.NotifyIcon trayIcon;

        private MiniTimerWindow miniView;
        private bool inMiniView;

        private TaskbarWidgetWindow taskbarWidget;
        private bool inTaskbarWidget;

        private double restoreLeft;
        private double restoreTop;
        private double restoreWidth;
        private WindowState restoreWindowState = WindowState.Normal;

        private IssueViewModel lastRunningIssue;
        #endregion


        #region private consts
        private const int firstDelay = 500;
        private const int defaultDelay = 30000;
        private static readonly TimeSpan updateCheckInterval = TimeSpan.FromHours(1);
        #endregion
    }
}
