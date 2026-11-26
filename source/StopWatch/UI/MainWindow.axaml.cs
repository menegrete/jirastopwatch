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
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopWatch.Update;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;

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
    /// Ported from WPF to Avalonia. Differences worth knowing when reading
    /// the code: dialogs are awaited (ShowDialog never blocks), the clipboard
    /// is async, the tray is a <see cref="TrayService"/>, and the window's
    /// "normal" geometry is tracked here because Avalonia has no RestoreBounds.
    /// </summary>
    internal partial class MainWindow : Window
    {
        #region public methods
        public MainWindow()
        {
            InitializeComponent();
        }


        public MainWindow(Settings settings, AppComposition composition)
            : this()
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (composition == null)
                throw new ArgumentNullException("composition");

            this.settings = settings;

            // (StopWatch.Theme is spelled out throughout: inside a control,
            // "Theme" is Avalonia's Control.Theme.)
            StopWatch.Theme.Current = StopWatch.Theme.ForMode(settings.Theme);

            restClientFactory = composition.RestClientFactory;
            jiraClient = composition.JiraClient;
            jiraService = composition.JiraService;
            timeLoad = composition.TimeLoad;
            timeLoad.Notifier = new TimeLoadNotifier(this);

            issues = composition.Issues;
            issues.TimerStarted += issues_TimerStarted;
            issues.PropertyChanged += issues_PropertyChanged;

            activeTimer = composition.ActiveTimer;

            updateService = new AutoUpdateService(new GitHubReleaseSource());
            updatePlatform = UpdatePlatforms.Current();
            updateInstaller = UpdateInstallers.ForPlatform(updatePlatform);

            Title = $"Jira StopWatch v{AppInfo.Version}";

            DataContext = issues;
            ApplyDensity();

            // Given a real width (and, where there is one, a position) before
            // the window is ever shown, so that SizeToContent stays
            // Height-only and the window does not flash at the wrong place;
            // Opened corrects both against the screen it really landed on.
            // See design.md D7.
            RestoreWidth();
            RestoreLocation();

            RegisterCommands();

            // Avalonia has no PreviewKeyDown: the tunnelling stage of KeyDown
            // is the same thing. See MainWindow_PreviewKeyDown.
            AddHandler(KeyDownEvent, MainWindow_PreviewKeyDown, RoutingStrategies.Tunnel);

            ticker = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(firstDelay) };
            ticker.Tick += ticker_Tick;

            Opened += MainWindow_Opened;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;
            PositionChanged += (s, e) => ScheduleNormalGeometryCapture();
            Resized += (s, e) => ScheduleNormalGeometryCapture();

            ApplyTheme();
        }


        /// <summary>
        /// Repaints with the active theme. Called at construction and again
        /// whenever the user changes the theme setting.
        ///
        /// This is the whole of what ThemeApplier's 579-line walk over the
        /// control tree used to do: the styles reference the theme through
        /// DynamicResource, so replacing the brushes repaints everything
        /// (ThemeAnimatedBorder re-reads its own colour when the theme
        /// variant changes). What is left is the title bar, which Windows
        /// draws itself.
        /// </summary>
        public void ApplyTheme()
        {
            ThemeBrushes.ApplyToApplication();

            if (OperatingSystem.IsWindows())
            {
                bool dark = StopWatch.Theme.Current.Mode == StopWatch.ThemeMode.Dark;
                var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

                // Every window open now, not only this one.
                var windows = new List<Window> { this };
                if (lifetime != null)
                    windows.AddRange(lifetime.Windows.Where(w => w != this));

                foreach (Window window in windows)
                    NativeMethods.SetTitleBarDarkMode(window, dark);
            }
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

            if (OperatingSystem.IsWindows() && inTaskbarWidget)
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
        private void MainWindow_Opened(object sender, EventArgs e)
        {
            // Opened is raised every time the window is shown again after a
            // Hide(); this is first-show work.
            if (loaded)
                return;

            if (OperatingSystem.IsWindows())
            {
                // The title bar is drawn by DWM, not by Avalonia: tell it the
                // theme. Needs the handle, hence here and not the constructor.
                NativeMethods.SetTitleBarDarkMode(this, StopWatch.Theme.Current.Mode == StopWatch.ThemeMode.Dark);

                // WM_SHOWME, broadcast by a second instance, is picked off the
                // window's handle with a hook. See the openspec design, D8.
                Win32Properties.AddWndProcHookCallback(this, WndProcHook);
            }

            // Re-validated against the screen the window actually ended up on
            // (the constructor only had the primary one to go by).
            RestoreWidth();
            RestoreLocation();
            ClampHeightToWorkingArea();

            // Applied after the position restore: a maximized window's exact
            // pixel geometry is decided by the OS for whichever monitor it
            // ends up on, not by the remembered position.
            if (settings.MainWindowMaximized)
            {
                // What to come back to when un-maximized, taken before the
                // maximize replaces it.
                normalPosition = Position;
                normalWidth = Width;
                haveNormalGeometry = true;

                // Set here, not left to the state-change handler's own
                // SizeToContent=Manual: the window would otherwise land at the
                // remembered Normal size with Maximized chrome.
                SizeToContent = SizeToContent.Manual;
                WindowState = WindowState.Maximized;
            }

            loaded = true;
            ScheduleNormalGeometryCapture();

            Topmost = settings.AlwaysOnTop;
            UpdateMiniViewButtonVisibility();

            issues.Hydrate();
            UpdateAddIssueTooltip();

            if (settings.FirstRun)
            {
                settings.FirstRun = false;

                // Deferred rather than called inline: showing a modal dialog
                // from within Opened races this window's own activation, and
                // the dialog can end up behind it.
                Dispatcher.UIThread.Post(() => EditSettingsAsync().FireAndForget());
            }
            else if (IsJiraEnabled)
            {
                LoadApiTokenAndAuthenticateAsync().FireAndForget();
            }

            ticker.Start();

            lastUpdateCheckUtc = DateTime.UtcNow;
            CheckForUpdatesAsync().FireAndForget();
        }


        private IntPtr WndProcHook(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (uint)NativeMethods.WM_SHOWME)
                ShowOnTop();

            return IntPtr.Zero;
        }


        /// <summary>
        /// Captures Normal-state geometry while the window still exists, for
        /// the final save on exit (the periodic save takes it from
        /// <see cref="SaveSettingsAndIssueStates"/>).
        /// </summary>
        private void MainWindow_Closing(object sender, WindowClosingEventArgs e)
        {
            CaptureNormalGeometry();
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

            if (OperatingSystem.IsWindows() && taskbarWidget != null)
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
                updateInstaller.ApplyOnExit(pendingUpdate, AppContext.BaseDirectory);
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

            PendingUpdate update = await updateService.CheckAndStageAsync(settings.CheckForUpdates, settings.SubscribeToBetaReleases, AppInfo.Version, AppInfo.IsSelfContained, updatePlatform, stagingBaseDir);
            if (update == null)
                return;

            pendingUpdate = update;
            lblUpdateReady.Text = update.IsPrerelease
                ? $"v{update.Version} (beta) ready — restart to apply"
                : $"v{update.Version} ready — restart to apply";
            lblUpdateReady.IsVisible = true;

            lblUpdateWhatsNew.IsVisible = true;
        }


        private void lblUpdateWhatsNew_Click(object sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton != MouseButton.Left || pendingUpdate == null)
                return;

            AppInfo.OpenUrl(new Uri(pendingUpdate.ReleaseUrl).ToString());
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
            btnMiniView.IsVisible = EffectiveMinimizeBehavior != MinimizeBehavior.TaskbarWidget;
        }


        /// <summary>
        /// What minimizing really does. The taskbar widget exists only on
        /// Windows; a setting inherited from there behaves as the mini view
        /// anywhere else (spec taskbar-widget-view).
        /// </summary>
        private MinimizeBehavior EffectiveMinimizeBehavior
        {
            get { return MinimizeBehaviors.Effective(settings.MinimizeBehavior, OperatingSystem.IsWindows()); }
        }
        #endregion


        #region size and position
        /// <summary>
        /// Avalonia has no RestoreBounds, so the window's Normal-state width
        /// and position are tracked here: updated whenever the window moves or
        /// is resized while it is Normal and visible, and read back by
        /// <see cref="RememberNormalGeometry"/> and by the mini view /
        /// taskbar widget when they put this window away. Never taken while
        /// Maximized or Minimized, whose own coordinates are not the ones to
        /// come back to. See design.md D9.
        /// </summary>
        private void CaptureNormalGeometry()
        {
            if (!loaded || !IsVisible || WindowState != WindowState.Normal)
                return;

            normalPosition = Position;
            normalWidth = ClientSize.Width;
            haveNormalGeometry = true;
        }


        /// <summary>
        /// Position and size events arrive in the middle of a maximize, when
        /// the window is still reporting Normal with the maximized rectangle,
        /// so the read is deferred until the state has settled.
        /// </summary>
        private void ScheduleNormalGeometryCapture()
        {
            if (!loaded || captureScheduled)
                return;

            captureScheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                captureScheduled = false;
                CaptureNormalGeometry();
            }, DispatcherPriority.Background);
        }


        /// <summary>
        /// Writes the window's Normal-state width and position into the
        /// settings, right before <see cref="Settings.Save"/> - called from
        /// <see cref="SaveSettingsAndIssueStates"/>, i.e. every ticker tick
        /// and on close.
        /// </summary>
        private void RememberNormalGeometry()
        {
            CaptureNormalGeometry();

            if (!haveNormalGeometry)
                return;

            settings.MainWindowWidth = (int)Math.Round(normalWidth);
            settings.MainWindowLocation = ScreenPlacement.FormatLocation(
                new DrawingPoint(normalPosition.X, normalPosition.Y));
        }


        /// <summary>
        /// Restores the remembered width, validated against the screen that is
        /// actually there - the same problem the mini view's position has, so
        /// the same helper answers it. Both are in device-independent units.
        /// </summary>
        private void RestoreWidth()
        {
            Width = ScreenPlacement.ClampWidth(
                settings.MainWindowWidth,
                (int)MinWidth,
                WorkingAreaInDips);
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

            PlaceOnScreen(new PixelPoint(desired.X, desired.Y), Width);
        }


        /// <summary>
        /// Puts the window at <paramref name="desired"/> (device pixels) - or
        /// at the nearest place that is on a connected screen - given a width
        /// in device-independent units. The height is whatever the rows have
        /// made it by now.
        /// </summary>
        private void PlaceOnScreen(PixelPoint desired, double width)
        {
            double scale = ScaleAt(desired);
            double height = Bounds.Height > 0 ? Bounds.Height : 200;

            DrawingSize size = new DrawingSize(
                (int)Math.Ceiling(width * scale),
                (int)Math.Ceiling(height * scale));

            DrawingPoint onScreen = ScreenAreas.From(Screens).EnsureOnScreen(new DrawingPoint(desired.X, desired.Y), size);

            // Not read back: on X11 Position updates asynchronously.
            Position = new PixelPoint(onScreen.X, onScreen.Y);
        }


        /// <summary>Device pixels per device-independent unit at a point of the desktop.</summary>
        private double ScaleAt(PixelPoint point)
        {
            double scale = Screens.ScreenFromPoint(point)?.Scaling ?? DesktopScaling;
            return scale > 0 ? scale : 1.0;
        }


        /// <summary>
        /// The derived height stops at the working area. Past that the list
        /// scrolls, which is the overflow of this limit rather than the normal
        /// way to use the window.
        /// </summary>
        private void ClampHeightToWorkingArea()
        {
            // MaxHeight limits the client area; the title bar and borders are
            // on top of it.
            double chrome = FrameSize.HasValue ? Math.Max(0, FrameSize.Value.Height - ClientSize.Height) : 0;

            MaxHeight = Math.Max(WorkingAreaInDips.Height - chrome, MinHeight);
        }


        /// <summary>
        /// The working area of the screen the window is on (the primary one
        /// until it has been shown), in device-independent units - the unit
        /// the window's own Width and Height are in.
        /// </summary>
        private System.Drawing.Rectangle WorkingAreaInDips
        {
            get
            {
                Screen screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
                if (screen == null)
                    return new System.Drawing.Rectangle(0, 0, 1280, 720);

                double scale = screen.Scaling > 0 ? screen.Scaling : 1.0;
                PixelRect area = screen.WorkingArea;

                return new System.Drawing.Rectangle(
                    (int)Math.Round(area.X / scale),
                    (int)Math.Round(area.Y / scale),
                    (int)Math.Round(area.Width / scale),
                    (int)Math.Round(area.Height / scale));
            }
        }
        #endregion


        #region window state and the tray icon
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // WPF's StateChanged. Raised while the base class is still being
            // constructed, before there are settings to consult.
            if (change.Property == WindowStateProperty && settings != null)
                MainWindow_StateChanged();
            else if (change.Property == SizeToContentProperty && settings != null)
                KeepHeightDerived();
        }


        /// <summary>
        /// The window's own state drives what minimizing does: routes to the
        /// tray icon, the mini view or the taskbar widget depending on
        /// settings.MinimizeBehavior.
        /// </summary>
        private void MainWindow_StateChanged()
        {
            // While the mini view or the taskbar widget is up this window is
            // hidden on purpose. The tray icon must not appear as well: two
            // stand-ins for one hidden window is one too many.
            if (inMiniView || inTaskbarWidget)
                return;

            if (WindowState == WindowState.Minimized)
            {
                // Deferred out of the platform's own notification: this hides
                // the window, which is not something to do in the middle of
                // the resize message that minimized it.
                Dispatcher.UIThread.Post(MinimizeAway);
            }
            else if (WindowState == WindowState.Normal)
            {
                HideTrayIcon();

                // Reinstate auto-height now that the window is done being
                // maximized - see the Maximized branch below for why it was
                // turned off. Maximizing also overwrote Width and Height with
                // the screen's size, which would otherwise be what the window
                // comes back at.
                if (haveNormalGeometry)
                    Width = normalWidth;

                Height = double.NaN;
                SizeToContent = SizeToContent.Height;

                RememberMaximized(false);
            }
            else if (WindowState == WindowState.Maximized)
            {
                // A maximized window must fill the screen, not shrink back to
                // its content size on the next layout pass.
                SizeToContent = SizeToContent.Manual;

                RememberMaximized(true);
            }
        }


        private void MinimizeAway()
        {
            // The window may have been restored again in the meantime.
            if (WindowState != WindowState.Minimized || inMiniView || inTaskbarWidget)
                return;

            switch (EffectiveMinimizeBehavior)
            {
                case MinimizeBehavior.MiniView:
                    EnterMiniView();
                    break;

                case MinimizeBehavior.TaskbarWidget:
                    EnterTaskbarWidget();
                    break;

                default:
                    ShowTrayIcon();
                    Hide();
                    break;
            }
        }


        /// <summary>
        /// The height is derived, and the user cannot choose it: dragging the
        /// bottom edge makes Avalonia drop the Height auto-size, which is put
        /// back here (issue-list spec, "el alto no cambia"). Not while the
        /// window is Maximized, which turns auto-size off on purpose.
        /// </summary>
        private void KeepHeightDerived()
        {
            if (!loaded || SizeToContent.HasFlag(SizeToContent.Height))
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (WindowState == WindowState.Normal && !SizeToContent.HasFlag(SizeToContent.Height))
                    SizeToContent = SizeToContent.Height;
            });
        }


        /// <summary>
        /// Persists maximized/normal, guarded by <c>loaded</c> so applying
        /// the remembered state at startup doesn't immediately overwrite
        /// itself. Minimized is never passed in here - see the
        /// main-window-placement spec, "Minimizing does not change the
        /// remembered state".
        /// </summary>
        private void RememberMaximized(bool maximized)
        {
            if (!loaded)
                return;

            settings.MainWindowMaximized = maximized;
        }


        private void ShowTrayIcon()
        {
            if (tray == null)
            {
                tray = new TrayService();
                tray.Clicked += tray_Clicked;
                tray.SetMenu(BuildTrayMenu());
            }

            tray.Visible = true;
        }


        /// <summary>
        /// The tray context menu, built from the plugins' tray commands - or
        /// empty when there are none, so a right-click behaves exactly as it
        /// did before plugins existed.
        /// </summary>
        private IEnumerable<TrayMenuEntry> BuildTrayMenu()
        {
            var entries = new List<TrayMenuEntry>();

            foreach (var group in plugins.GetCommands(Plugin.PluginCommandLocation.Tray).GroupBy(c => c.Plugin))
            {
                if (entries.Count > 0)
                    entries.Add(TrayMenuEntry.Separator());

                entries.Add(TrayMenuEntry.Header(group.Key.Name));

                foreach (Plugins.PluginCommandItem command in group)
                {
                    Plugins.PluginCommandItem captured = command;
                    entries.Add(TrayMenuEntry.Item(captured.Title, () => RunPluginCommand(captured)));
                }
            }

            return entries;
        }


        private void RunPluginCommand(Plugins.PluginCommandItem command)
        {
            string error = command.Run();
            if (error != null)
                ShowMessage(error, "Plugin error");
        }


        /// <summary>
        /// Hands the loaded plugins to the window, which shows their commands
        /// in the tray menu and the Plugins button. Called once, before the
        /// window is first shown.
        /// </summary>
        internal void SetPlugins(Plugins.PluginManager manager)
        {
            plugins = manager ?? Plugins.PluginManager.Empty;

            btnPlugins.IsVisible = plugins.HasPlugins;

            if (tray != null)
                tray.SetMenu(BuildTrayMenu());
        }


        private void btnPlugins_Click(object sender, RoutedEventArgs e)
        {
            var menu = new MenuFlyout { Placement = PlacementMode.Top };

            foreach (var group in plugins.GetCommands(Plugin.PluginCommandLocation.MainWindow).GroupBy(c => c.Plugin))
            {
                menu.Items.Add(new MenuItem { Header = group.Key.Name, IsEnabled = false, FontWeight = FontWeight.Bold });

                foreach (Plugins.PluginCommandItem command in group)
                {
                    Plugins.PluginCommandItem captured = command;
                    var item = new MenuItem { Header = captured.Title };
                    item.Click += (s, args) => RunPluginCommand(captured);
                    menu.Items.Add(item);
                }
            }

            if (menu.Items.Count > 0)
                menu.Items.Add(new Separator());

            var status = new MenuItem { Header = "Plugin status..." };
            status.Click += (s, args) => ShowPluginStatus();
            menu.Items.Add(status);

            menu.ShowAt(btnPlugins);
        }


        private void ShowPluginStatus()
        {
            string text = string.Join(Environment.NewLine, plugins.Plugins.Select(p => p.StatusText));
            ShowMessage(text, "Plugins");
        }


        private void HideTrayIcon()
        {
            if (tray != null)
                tray.Visible = false;
        }


        private void DisposeTrayIcon()
        {
            if (tray == null)
                return;

            tray.Dispose();
            tray = null;
        }


        /// <summary>
        /// A left click restores the window. A right click only ever opens the
        /// menu (native behavior), so with no plugin commands it does nothing,
        /// as before.
        /// </summary>
        private void tray_Clicked(object sender, EventArgs e)
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

            RememberRestoreGeometry();

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

            BringBack();
        }


        /// <summary>
        /// What this window was doing when it was put away, kept so that
        /// <see cref="BringBack"/> can put it back. The position is the Normal
        /// one: the window is usually minimized by then, and a minimized
        /// window's own coordinates are not worth coming back to.
        /// </summary>
        private void RememberRestoreGeometry()
        {
            CaptureNormalGeometry();

            restorePosition = normalPosition;
            restoreWidth = normalWidth;
            restoreWindowState = WindowState;
        }


        /// <summary>Shows this window again in the state and place <see cref="RememberRestoreGeometry"/> kept.</summary>
        private void BringBack()
        {
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
            if (!haveNormalGeometry)
                return;

            PlaceOnScreen(restorePosition, restoreWidth);
            Width = restoreWidth;
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
        /// connected. See the taskbar-widget-view spec. Windows only: anywhere
        /// else the setting is treated as the mini view
        /// (<see cref="EffectiveMinimizeBehavior"/>).
        /// </summary>
        private void EnterTaskbarWidget()
        {
            if (inTaskbarWidget || !OperatingSystem.IsWindows())
                return;

            RememberRestoreGeometry();

            inTaskbarWidget = true;

            // The tray icon belongs to the minimize-to-tray feature, not here.
            HideTrayIcon();

            EnsureTaskbarWidget();
            taskbarWidget.ShowAt();

            Hide();
        }


        /// <summary>Takes the taskbar widget down and brings this window back as it was.</summary>
        [SupportedOSPlatform("windows")]
        private void ExitTaskbarWidget()
        {
            if (!inTaskbarWidget)
                return;

            inTaskbarWidget = false;

            if (taskbarWidget != null)
                taskbarWidget.StopAndHide();

            BringBack();
        }


        /// <summary>Creates the widget window on first use.</summary>
        [SupportedOSPlatform("windows")]
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
        [SupportedOSPlatform("windows")]
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


        [SupportedOSPlatform("windows")]
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
        [SupportedOSPlatform("windows")]
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


        /// <summary>
        /// Startup connection: the token comes from the secret store, which
        /// may block (a locked keyring), so it is awaited, not waited on. If it
        /// cannot be read the user is told and the connection is attempted
        /// without a token, which leaves the window in its "not connected" state.
        /// </summary>
        private async Task LoadApiTokenAndAuthenticateAsync()
        {
            lblConnectionStatus.Text = "Connecting...";

            try
            {
                await settings.LoadApiTokenAsync();
            }
            catch (SecretStoreException ex)
            {
                ShowMessage(ex.Message, "Jira API token");
            }

            AuthenticateJira(settings.Username, settings.ApiToken);
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
                connected ? StopWatch.Theme.Current.Success : StopWatch.Theme.Current.Danger);

            lblConnectionStatus.TextDecorations = connected ? null : TextDecorations.Underline;
            lblConnectionStatus.Cursor = connected ? Cursor.Default : new Cursor(StandardCursorType.Hand);
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
                issue.StatusName = result.StatusName;
                issue.StatusCategory = result.StatusCategory;
            }
        }


        private void ChangeIssueState(IssueViewModel issue)
        {
            if (string.IsNullOrWhiteSpace(settings.StartTransitions))
                return;

            ChangeIssueStateAsync(issue).FireAndForget();
        }


        private async Task ChangeIssueStateAsync(IssueViewModel issue)
        {
            string issueKey = issue.IssueKey;
            string[] startTransitions = settings.StartTransitions
                .Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().ToLower()).ToArray();

            bool transitioned = await Task.Run(() =>
            {
                AvailableTransitions available = jiraClient.GetAvailableTransitions(issueKey);
                if (available == null || available.Transitions.Count() == 0)
                    return false;

                foreach (Transition t in available.Transitions)
                {
                    if (startTransitions.Any(t.Name.ToLower().Contains))
                        return jiraClient.DoTransition(issueKey, t.Id);
                }

                return false;
            });

            // Show the new status now rather than on the next 30-second tick -
            // unless the row was pointed at another key in the meantime.
            if (transitioned && issue.IssueKey == issueKey)
                UpdateSummary(issue);
        }
        #endregion


        #region status bar handlers
        private void lblConnectionStatus_Click(object sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton != MouseButton.Left)
                return;

            if (jiraClient.SessionValid)
                return;

            string msg = string.Format("Jira StopWatch could not connect to your Jira server. Error returned:{0}{0}{1}", Environment.NewLine, jiraClient.ErrorMessage);
            ShowMessage(msg, "Connection error");
        }


        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            EditSettingsAsync().FireAndForget();
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
            Control element = sender as Control;
            return element == null ? null : element.DataContext as IssueViewModel;
        }


        private void Row_Selected(object sender, RoutedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            if (issue != null)
                issues.SetCurrent(issue);
        }


        /// <summary>
        /// Same as Row_Selected, for the pointer: a click anywhere on a row
        /// (its background, or the time field) selects it.
        /// </summary>
        private void Row_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
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


        /// <summary>A double click on the time field opens the edit dialog.</summary>
        private void Time_DoubleTapped(object sender, TappedEventArgs e)
        {
            IssueViewModel issue = RowOf(sender);
            if (issue == null)
                return;

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
        /// Takes over the key field's own Paste, which its right-click context
        /// menu invokes directly - unlike Ctrl+V, that never reaches
        /// MainWindow_PreviewKeyDown, so without this the context menu would
        /// paste the raw clipboard text instead of parsing a URL to a key.
        /// </summary>
        private void tbIssueKey_PastingFromClipboard(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            PasteKey(RowOf(sender));
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
        /// issue-key field has focus and mark it handled, so the key bindings
        /// below never see the keystroke.
        ///
        /// Catching them here, at the window's tunnelling stage, wins the
        /// race: the event reaches the window before it reaches the focused
        /// TextBox, so this runs first regardless of what has focus.
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Control)
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

            if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
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
            StopWatchCommands.SelectPrevious.Bind(this, () => { issues.SelectPrevious(); BringCurrentIntoView(); });
            StopWatchCommands.SelectNext.Bind(this, () => { issues.SelectNext(); BringCurrentIntoView(); });
            StopWatchCommands.MoveUp.Bind(this, () => { issues.MoveUp(issues.Current); BringCurrentIntoView(); });
            StopWatchCommands.MoveDown.Bind(this, () => { issues.MoveDown(issues.Current); BringCurrentIntoView(); });
            StopWatchCommands.TogglePlay.Bind(this, () => TogglePlay(issues.Current));
            StopWatchCommands.PostWorklog.Bind(this, () => PostWorklog(issues.Current));
            StopWatchCommands.EditTime.Bind(this, () => EditTime(issues.Current));
            StopWatchCommands.ResetTimer.Bind(this, () => ResetTimer(issues.Current));
            StopWatchCommands.RemoveIssue.Bind(this, () => RemoveIssue(issues.Current));
            StopWatchCommands.FocusKey.Bind(this, () => FocusKey(issues.Current));
            StopWatchCommands.AddIssue.Bind(this, IssueAdd);
            StopWatchCommands.CopyKey.Bind(this, () => CopyKey(issues.Current));
            StopWatchCommands.PasteKey.Bind(this, () => PasteKey(issues.Current));
            StopWatchCommands.OpenInBrowser.Bind(this, () => OpenInBrowser(issues.Current));
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
            RemoveIssueAsync(issue).FireAndForget();
        }


        private async Task RemoveIssueAsync(IssueViewModel issue)
        {
            if (issue == null || !issues.CanRemove)
                return;

            // Same condition that enables the row's load-time button: if the
            // time could still be loaded, removing the row would lose it.
            if (issue.CanPost)
            {
                var dialog = new ConfirmRemoveWindow(issue.IssueKey, issue.WatchTimer.TimeElapsed, issue.IsRunning);
                if (await dialog.ShowDialog<bool?>(this) != true)
                    return;
            }

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

            CopyToClipboardAsync(issue.IssueKey).FireAndForget();
        }


        private void CopyParentKey(IssueViewModel issue)
        {
            if (issue == null || !issue.HasParent)
                return;

            CopyToClipboardAsync(issue.ParentKey).FireAndForget();
        }


        private async Task CopyToClipboardAsync(string text)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(text);
        }


        private void PasteKey(IssueViewModel issue)
        {
            if (issue == null)
                return;

            PasteKeyAsync(issue).FireAndForget();
        }


        private async Task PasteKeyAsync(IssueViewModel issue)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                return;

            string text = await clipboard.TryGetTextAsync();
            if (string.IsNullOrEmpty(text))
                return;

            issue.IssueKey = JiraKeyHelpers.ParseUrlToKey(text);
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
        /// <summary>
        /// A message box. Owned by the main window only while it can be seen:
        /// with the mini view or the tray up the window is hidden and an
        /// invisible owner would keep the box out of sight. Not awaited by
        /// callers: Avalonia dialogs never block the code that opens them.
        /// </summary>
        private void ShowMessage(string text, string title)
        {
            bool canOwn = IsVisible && WindowState != WindowState.Minimized;

            MessageDialog.Show(canOwn ? this : null, text, title).FireAndForget();
        }


        private async Task EditSettingsAsync()
        {
            ThemeMode themeBefore = settings.Theme;
            int maxIssuesBefore = settings.MaxIssues;
            ListDensity densityBefore = settings.ListDensity;
            TimeDisplayFormat timeDisplayFormatBefore = settings.TimeDisplayFormat;

            string apiTokenBefore = settings.ApiToken;

            var dialog = new SettingsWindow(settings);
            if (await dialog.ShowDialog<bool?>(this) != true)
                return;

            settings.Save();

            if (settings.ApiToken != apiTokenBefore)
            {
                try
                {
                    await settings.SaveApiTokenAsync();
                }
                catch (SecretStoreException ex)
                {
                    // The token is still in memory, so this session keeps working.
                    ShowMessage(ex.Message, "Jira API token");
                }
            }

            restClientFactory.BaseUrl = settings.JiraBaseUrl;
            Logging.Logger.Instance.Enabled = settings.LoggingEnabled;

            if (settings.Theme != themeBefore)
            {
                StopWatch.Theme.Current = StopWatch.Theme.ForMode(settings.Theme);
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
            EditTimeAsync(issue).FireAndForget();
        }


        private async Task EditTimeAsync(IssueViewModel issue)
        {
            if (issue == null)
                return;

            var dialog = new EditTimeWindow(issue.WatchTimer.TimeElapsed);
            if (await dialog.ShowDialog<bool?>(this) != true)
                return;

            issue.SetTimeElapsed(dialog.Time);
            issues.Refresh();
            activeTimer.Refresh();
        }


        private void PostWorklog(IssueViewModel issue)
        {
            PostWorklogDialogAsync(issue).FireAndForget();
        }


        private async Task PostWorklogDialogAsync(IssueViewModel issue)
        {
            if (issue == null || !issue.CanPost)
                return;

            // A plugin may be holding an earlier load of this issue open, with
            // its own dialog and no timeout: do not start a second one.
            if (loadingIssues.Contains(issue))
                return;

            var dialog = new WorklogWindow(
                issue.WatchTimer.GetInitialStartTime(),
                issue.WatchTimer.TimeElapsedNearestMinute,
                issue.Comment,
                issue.EstimateUpdateMethod,
                issue.EstimateUpdateValue);

            // Deliberately not awaited: the dialog has to come up right away,
            // and the estimate fills itself in once Jira answers.
            FillRemainingEstimateAsync(dialog, issue.IssueKey).FireAndForget();

            await dialog.ShowDialog(this);

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
            ClearTimeLoadNotice();

            var input = new TimeLoadInput
            {
                IssueKey = issue.IssueKey,
                StartTime = startTime,
                TimeElapsed = issue.WatchTimer.TimeElapsedNearestMinute,
                Comment = issue.Comment,
                EstimateUpdateMethod = issue.EstimateUpdateMethod,
                EstimateUpdateValue = issue.EstimateUpdateValue,
                Source = Plugin.TimeLoadSource.User,

                // The timer may keep running while a plugin waits for a person;
                // a notice counts what remains from what it holds at that point.
                CurrentElapsed = () => issue.WatchTimer.TimeElapsedNearestMinute
            };

            // The wait cursor belongs to the host's own load only: while a
            // plugin handler waits for a person it would be misleading.
            Cursor previous = null;
            Action<bool> setBusy = busy =>
            {
                if (busy)
                {
                    previous = Cursor;
                    Cursor = new Cursor(StandardCursorType.Wait);
                }
                else
                {
                    Cursor = previous;
                }
            };

            loadingIssues.Add(issue);
            try
            {
                TimeLoadPipelineResult result = await timeLoad.LoadAsync(input, setBusy);

                // Reset on success; after a partial load, reduced by what reached
                // Jira so a retry loads only the rest. The row's comment and
                // estimate are left as the user had them.
                TimerAfterLoad.Apply(
                    result,
                    () => issue.WatchTimer.TimeElapsed,
                    elapsed => issue.SetTimeElapsed(elapsed),
                    () => ResetTimer(issue));
            }
            finally
            {
                loadingIssues.Remove(issue);
            }
        }


        private void ShowTimeLoadNotice(string message)
        {
            lblTimeLoadNotice.Text = message;
            ToolTip.SetTip(lblTimeLoadNotice, message + Environment.NewLine + "(click to dismiss)");
            lblTimeLoadNotice.IsVisible = true;
        }


        private void ClearTimeLoadNotice()
        {
            lblTimeLoadNotice.IsVisible = false;
            lblTimeLoadNotice.Text = "";
        }


        private void lblTimeLoadNotice_Click(object sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                ClearTimeLoadNotice();
        }


        private void ShowTimeLoadWarning(string message)
        {
            ShowMessage(message, "Time load");
        }


        /// <summary>How the pipeline's notices reach the user: a short line for the harmless one, a message box for the others.</summary>
        private class TimeLoadNotifier : ITimeLoadNotifier
        {
            private readonly MainWindow window;

            public TimeLoadNotifier(MainWindow window)
            {
                this.window = window;
            }

            public void Info(string message)
            {
                window.ShowTimeLoadNotice(message);
            }

            public void Warning(string message)
            {
                window.ShowTimeLoadWarning(message);
            }
        }
        #endregion


        #region private methods
        private void issues_TimerStarted(object sender, IssueViewModel issue)
        {
            ChangeIssueState(issue);

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


        private void issues_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Density")
                ApplyDensity();
        }


        /// <summary>
        /// Picks between the two row templates: change the density and every
        /// row is re-templated, timers and order untouched.
        /// </summary>
        private void ApplyDensity()
        {
            string key = issues.Density == ListDensity.Spacious ? "IssueRowSpacious" : "IssueRowCompact";

            issueList.ItemTemplate = (Avalonia.Controls.Templates.IDataTemplate)Resources[key];
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
                ToolTip.SetTip(btnAddIssue, "Add another issue row (CTRL-N)");
                btnAddIssue.Cursor = new Cursor(StandardCursorType.Hand);
            }
            else
            {
                ToolTip.SetTip(btnAddIssue, string.Format("You have reached the max limit of {0} issues and cannot add another", settings.MaxIssues));
                btnAddIssue.Cursor = new Cursor(StandardCursorType.No);
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

            Control container = ContainerOf(current);
            if (container != null)
            {
                container.BringIntoView();
                return;
            }

            // A row that has just been added has no container until the next
            // layout pass.
            Dispatcher.UIThread.Post(() => ContainerOf(current)?.BringIntoView(), DispatcherPriority.Loaded);
        }


        private TextBox KeyFieldOf(IssueViewModel issue)
        {
            Control container = ContainerOf(issue);
            return container == null ? null : container.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        }


        private Control ContainerOf(IssueViewModel issue)
        {
            if (issue == null)
                return null;

            return issueList.ContainerFromItem(issue);
        }
        #endregion


        #region private members
        private readonly Settings settings;

        private readonly RestClientFactory restClientFactory;
        private readonly JiraClient jiraClient;
        private readonly IssueJiraService jiraService;
        private readonly TimeLoadPipeline timeLoad;
        private readonly HashSet<IssueViewModel> loadingIssues = new HashSet<IssueViewModel>();

        private readonly IssueListViewModel issues;
        private readonly ActiveTimerViewModel activeTimer;

        private readonly AutoUpdateService updateService;
        private readonly UpdatePlatform updatePlatform;
        private readonly IUpdateInstaller updateInstaller;
        private PendingUpdate pendingUpdate;
        private DateTime lastUpdateCheckUtc;

        private readonly DispatcherTimer ticker;

        private TrayService tray;
        private Plugins.PluginManager plugins = Plugins.PluginManager.Empty;

        private MiniTimerWindow miniView;
        private bool inMiniView;

        private TaskbarWidgetWindow taskbarWidget;
        private bool inTaskbarWidget;

        // First-show work is done, so state changes are the user's from here on.
        private bool loaded;

        // The window's Normal-state geometry (device pixels / DIPs), see CaptureNormalGeometry.
        private bool haveNormalGeometry;
        private bool captureScheduled;
        private PixelPoint normalPosition;
        private double normalWidth;

        // What to come back to from the mini view or the taskbar widget.
        private PixelPoint restorePosition;
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
