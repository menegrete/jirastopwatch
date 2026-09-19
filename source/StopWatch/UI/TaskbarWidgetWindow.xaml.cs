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
 *
 * The taskbar-docking and auto-hide-riding approach in this file is adapted
 * from mechanicwb2-hub's now-playing-taskbar-widget (MIT License,
 * Copyright (c) 2026 MechanicWB),
 * https://github.com/mechanicwb2-hub/now-playing-taskbar-widget
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using Screen = System.Windows.Forms.Screen;

namespace StopWatch
{
    /// <summary>
    /// The taskbar-docked view: the active issue and its running clock,
    /// anchored to the free space of the Windows taskbar and following its
    /// real position, auto-hide state and animation. See the
    /// taskbar-widget-view spec.
    /// </summary>
    internal partial class TaskbarWidgetWindow : Window
    {
        #region public events
        /// <summary>Raised when the user asks to go back to the full window (double click or the context menu).</summary>
        public event EventHandler RestoreRequested;

        /// <summary>Raised from the context menu's Exit item; the owner decides how the application actually shuts down.</summary>
        public event EventHandler ExitRequested;

        /// <summary>
        /// Raised when this window's HWND was destroyed by the OS rather than
        /// by <see cref="ClosedByApp"/> - which happens when its owning
        /// taskbar (explorer.exe) dies, since an owned window is destroyed
        /// along with its owner. The listener should create a fresh instance
        /// and show it again.
        /// </summary>
        public event EventHandler Died;
        #endregion


        #region public members
        /// <summary>Set before an intentional close, so <see cref="Died"/> is not raised for it.</summary>
        public bool ClosedByApp { get; set; }
        #endregion


        #region public methods
        public TaskbarWidgetWindow(ActiveTimerViewModel viewModel, Settings settings, Func<IntPtr> resolveTray)
        {
            if (viewModel == null)
                throw new ArgumentNullException("viewModel");
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (resolveTray == null)
                throw new ArgumentNullException("resolveTray");

            this.viewModel = viewModel;
            this.settings = settings;
            this.resolveTray = resolveTray;

            InitializeComponent();

            DataContext = viewModel;

            contentTicker = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
            contentTicker.Interval = TimeSpan.FromSeconds(1);
            contentTicker.Tick += (s, e) => viewModel.Refresh();

            positionTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
            positionTimer.Interval = TimeSpan.FromMilliseconds(500);
            positionTimer.Tick += (s, e) => UpdatePosition();

            Closed += TaskbarWidgetWindow_Closed;
        }


        /// <summary>Shows the widget and starts tracking the taskbar.</summary>
        public void ShowAt()
        {
            viewModel.Refresh();
            Show();
            contentTicker.Start();
            positionTimer.Start();
            UpdatePosition();
        }


        /// <summary>
        /// Stops tracking the taskbar and hides the widget - for leaving
        /// taskbar-widget mode altogether. Not what a transient auto-hide/
        /// no-room/fullscreen hide uses (see <see cref="HideWidget"/>): those
        /// keep polling so the widget can notice the taskbar becoming
        /// available again on its own, without anyone telling it to.
        /// </summary>
        public void StopAndHide()
        {
            contentTicker.Stop();
            positionTimer.Stop();
            CancelRide();
            Hide();
        }
        #endregion


        #region private eventhandlers
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            hwnd = new WindowInteropHelper(this).Handle;
            TaskbarInterop.ApplyToolWindowStyle(hwnd);

            // Re-checks position/visibility (fullscreen state in particular)
            // promptly when the foreground window changes, instead of only on
            // the next 500ms poll.
            fgProc = (hookHandle, eventType, eventHwnd, idObject, idChild, eventThread, eventTime) =>
            {
                if (updateQueued)
                    return;

                updateQueued = true;
                Dispatcher.BeginInvoke(new Action(UpdatePosition));
            };
            fgHook = TaskbarInterop.SetWinEventHook(
                TaskbarInterop.EVENT_SYSTEM_FOREGROUND, TaskbarInterop.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, fgProc, 0, 0, TaskbarInterop.WINEVENT_OUTOFCONTEXT);
        }


        private void TaskbarWidgetWindow_Closed(object sender, EventArgs e)
        {
            closed = true;

            contentTicker.Stop();
            positionTimer.Stop();
            CancelRide();

            if (trayLocHook != IntPtr.Zero)
                TaskbarInterop.UnhookWinEvent(trayLocHook);
            if (fgHook != IntPtr.Zero)
                TaskbarInterop.UnhookWinEvent(fgHook);

            if (!ClosedByApp)
            {
                EventHandler handler = Died;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }


        private void Pill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                RaiseRestoreRequested();
        }


        private void PauseResume_Click(object sender, RoutedEventArgs e)
        {
            viewModel.ToggleActive();
        }


        /// <summary>Rebuilds the Monitor submenu from the currently connected monitors each time it opens, so it never shows a monitor that has since been disconnected (or misses one just plugged in).</summary>
        private void WidgetMenu_Opened(object sender, RoutedEventArgs e)
        {
            ContextMenu menu = (ContextMenu)sender;

            // Suspend the widget's own periodic EnsureTopmost while the menu
            // is open: re-asserting the WIDGET's topmost status (positionTimer
            // ticks every 500ms, plus the foreground/location hooks) puts it
            // back above its own already-open menu popup, which pushes that
            // popup behind the real taskbar in turn - the same failure mode
            // the reference project's ReassertTopmost() comment describes for
            // its tooltip/volume popup, worked around the same way there.
            contextMenuOpen = true;

            // The Monitor entry is always the ContextMenu's first item - see
            // the XAML. Found via the tree rather than a named field: a
            // resource-scoped element does not get one from InitializeComponent.
            menuMonitor = (MenuItem)menu.Items[0];
            menuMonitor.Items.Clear();

            AddMonitorMenuItem(0, "Primary monitor");

            List<Screen> secondaries = Screen.AllScreens
                .Where(s => !s.Primary)
                .OrderBy(s => s.Bounds.Left)
                .ThenBy(s => s.Bounds.Top)
                .ToList();

            for (int i = 0; i < secondaries.Count; i++)
                AddMonitorMenuItem(i + 1, string.Format("Monitor {0}", i + 2));

            // The real Windows taskbar is itself always-on-top, and a WPF
            // Popup does not automatically become topmost just because the
            // control that owns it is - this gives the menu's own popup one
            // explicit nudge above it, on top of no longer being displaced by
            // the widget's own re-assertions. Deferred one dispatcher pass so
            // the popup's HwndSource already exists by the time this runs.
            Dispatcher.BeginInvoke(new Action(() => EnsureTopmostPopup(menu)));
        }


        private void WidgetMenu_Closed(object sender, RoutedEventArgs e)
        {
            contextMenuOpen = false;
            UpdatePosition();
        }


        /// <summary>Same problem as the main menu, for the Monitor submenu's own separate popup.</summary>
        private void MonitorSubmenu_Opened(object sender, RoutedEventArgs e)
        {
            MenuItem monitorItem = (MenuItem)sender;
            Dispatcher.BeginInvoke(new Action(() => EnsureTopmostPopup(monitorItem)));
        }


        /// <summary>
        /// Forces the Win32 window backing a popup (a ContextMenu or an open
        /// submenu) above the real taskbar. <paramref name="withinPopup"/> is
        /// any element that ends up inside that popup's own visual tree -
        /// the popup control itself for a ContextMenu, one of its generated
        /// item containers for a submenu, since a submenu's Popup has no
        /// element of its own to query before its items exist.
        /// </summary>
        private static void EnsureTopmostPopup(DependencyObject withinPopup)
        {
            DependencyObject target = withinPopup;
            if (target is MenuItem menuItem && menuItem.Items.Count > 0)
                target = menuItem.ItemContainerGenerator.ContainerFromIndex(0) as DependencyObject ?? target;

            Visual visual = target as Visual;
            HwndSource source = visual == null ? null : PresentationSource.FromVisual(visual) as HwndSource;
            if (source != null)
                TaskbarInterop.EnsureTopmost(source.Handle);
        }


        private void AddMonitorMenuItem(int index, string label)
        {
            MenuItem item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = settings.TaskbarWidgetMonitor == index,
                Tag = index
            };
            item.Click += MonitorMenuItem_Click;

            menuMonitor.Items.Add(item);
        }


        /// <summary>
        /// Single-select: picking one monitor always checks it and unchecks
        /// every other entry, regardless of which way this particular click
        /// toggled it - a plain <c>IsCheckable</c> MenuItem has no built-in
        /// "radio group" the way RadioButton does.
        /// </summary>
        private void MonitorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            int index = (int)item.Tag;

            foreach (MenuItem entry in menuMonitor.Items.OfType<MenuItem>())
                entry.IsChecked = ReferenceEquals(entry, item);

            settings.TaskbarWidgetMonitor = index;
            settings.Save();

            // resolveTray already reads the setting fresh on every poll, so
            // the widget would pick this up within 500ms on its own - calling
            // it now just makes the re-anchor feel immediate.
            UpdatePosition();
        }


        private void MenuRestore_Click(object sender, RoutedEventArgs e)
        {
            RaiseRestoreRequested();
        }


        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            EventHandler handler = ExitRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }


        private void RaiseRestoreRequested()
        {
            EventHandler handler = RestoreRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
        #endregion


        #region positioning
        /// <summary>Below this, the taskbar counts as fully off-screen.</summary>
        private const int HiddenThresholdPx = 2;

        /// <summary>Minimum gap from full height still counted as "fully settled".</summary>
        private const int SettledThresholdPx = 2;

        private void UpdatePosition()
        {
            updateQueued = false;

            if (closed || hwnd == IntPtr.Zero)
                return;

            // Pinned while the context menu is open: moving/clipping/
            // re-topmost-ing the widget out from under an open popup is what
            // pushes that popup behind the widget (and transitively behind
            // the real taskbar) - see WidgetMenu_Opened. WidgetMenu_Closed
            // runs one last update immediately after clearing this.
            if (contextMenuOpen)
                return;

            IntPtr tray = resolveTray();
            TaskbarInterop.RECT trayRectRaw;
            if (tray == IntPtr.Zero || !TaskbarInterop.GetWindowRect(tray, out trayRectRaw))
            {
                // resolveTray() re-resolves the configured monitor from
                // scratch on every call (see MainWindow.ResolveTargetTray),
                // so a monitor that just got disconnected recovers on its own
                // the moment resolveTray starts returning the primary
                // taskbar's handle instead - nothing more to do here than
                // wait for the next poll.
                CancelRide();
                barWasHidden = false;
                HideWidget();
                return;
            }

            EnsureTrayLocationHook(tray);

            // Owned by the taskbar: the window manager keeps this above its
            // owner by construction, and the OS destroys it for free if the
            // taskbar's process restarts (see TaskbarWidgetWindow_Closed).
            if (tray != ownerTray)
            {
                TaskbarInterop.SetOwner(hwnd, tray);
                ownerTray = tray;
                TaskbarInterop.EnsureTopmost(hwnd);
            }

            DrawingRectangle trayRect = ToRectangle(trayRectRaw);

            TaskbarInterop.RECT monitorRectRaw;
            TaskbarInterop.RECT workAreaRectRaw;
            TaskbarInterop.TryGetTrayMonitorRects(trayRectRaw, out monitorRectRaw, out workAreaRectRaw);
            DrawingRectangle monitorRect = ToRectangle(monitorRectRaw);

            int trayHeightPx = trayRect.Height;
            int visiblePx = TaskbarPlacement.GetVisiblePixels(trayRect, monitorRect);
            int reservedPx = monitorRectRaw.Bottom - workAreaRectRaw.Bottom;
            double dpiScale = GetDpiScale();
            int bandHeightPx = TaskbarPlacement.GetBandHeightPx(trayHeightPx, reservedPx, dpiScale);

            bool autoHide = TaskbarInterop.IsAutoHideEnabled();

            if (rideAnimating)
                return; // the ride's own timer owns the position until it finishes

            if (visiblePx <= HiddenThresholdPx)
            {
                if (Visibility == Visibility.Visible && autoHide && lastSizePx.Width > 0)
                {
                    StartRide(lastLeftPx, lastTopPx, BelowEdgeTopPx(monitorRectRaw.Bottom), down: true, monitorRectRaw.Bottom);
                    return;
                }

                barWasHidden = true;
                HideWidget();
                return;
            }

            if (visiblePx < trayHeightPx - SettledThresholdPx)
            {
                // Mid-slide. Visible = the taskbar just started hiding: ride
                // down in sync. Not visible = a reveal is in progress: wait
                // for it to settle, handled below once it does.
                if (Visibility == Visibility.Visible && autoHide && lastSizePx.Width > 0)
                {
                    double hiddenPhase = TaskbarPlacement.GetHiddenPhase(visiblePx, trayHeightPx);
                    StartRide(lastLeftPx, lastTopPx, BelowEdgeTopPx(monitorRectRaw.Bottom), down: true, monitorRectRaw.Bottom, hiddenPhase);
                }
                return;
            }

            // Settled on screen: figure out where the widget goes, sizing
            // itself down (dropping the summary first) if the free space is
            // tight, and hiding outright if not even the minimal content fits.
            RefreshAnchors(tray);

            TaskbarPlacement.AnchorPoints anchors;
            lock (anchorLock)
            {
                anchors = new TaskbarPlacement.AnchorPoints
                {
                    WidgetsRightPx = widgetsRightPx,
                    StartLeftPx = startLeftPx,
                    TaskButtonsRightPx = taskButtonsRightPx,
                    TrayNotifyLeftPx = TaskbarInterop.GetTrayNotifyLeft(tray)
                };
            }

            bool leftAligned = IsTaskbarLeftAligned();

            TaskbarPlacement.PlacementResult? placed = TryFit(trayRect, bandHeightPx, anchors, leftAligned, withSummary: true, dpiScale)
                ?? TryFit(trayRect, bandHeightPx, anchors, leftAligned, withSummary: false, dpiScale);

            if (placed == null)
            {
                barWasHidden = false;
                HideWidget();
                return;
            }

            if (!autoHide && TaskbarInterop.IsForegroundFullscreenOnMonitor(hwnd, monitorRectRaw))
            {
                barWasHidden = false;
                HideWidget();
                return;
            }

            DrawingSize sizePx = new DrawingSize((int)Math.Ceiling(ActualWidth * dpiScale), (int)Math.Ceiling(ActualHeight * dpiScale));

            if (barWasHidden && autoHide)
            {
                barWasHidden = false;
                StartRide(placed.Value.PositionPx.X, BelowEdgeTopPx(monitorRectRaw.Bottom), placed.Value.PositionPx.Y, down: false, monitorRectRaw.Bottom);
                lastLeftPx = placed.Value.PositionPx.X;
                lastTopPx = placed.Value.PositionPx.Y;
                lastSizePx = sizePx;
                return;
            }
            barWasHidden = false;

            TaskbarInterop.MoveWindowTo(hwnd, placed.Value.PositionPx.X, placed.Value.PositionPx.Y);
            TaskbarInterop.ClipWindowBottom(hwnd, sizePx.Width, sizePx.Height, monitorRectRaw.Bottom - placed.Value.PositionPx.Y);

            lastLeftPx = placed.Value.PositionPx.X;
            lastTopPx = placed.Value.PositionPx.Y;
            lastSizePx = sizePx;

            if (Visibility != Visibility.Visible)
                Visibility = Visibility.Visible;
            TaskbarInterop.EnsureTopmost(hwnd);
        }


        /// <summary>
        /// Tries to lay out the pill with (or without) the summary shown and
        /// see if the result fits the taskbar's free space. The summary is
        /// the first thing dropped when space is tight - the key and elapsed
        /// time are always kept.
        /// </summary>
        private TaskbarPlacement.PlacementResult? TryFit(DrawingRectangle trayRect, int bandHeightPx, TaskbarPlacement.AnchorPoints anchors, bool leftAligned, bool withSummary, double dpiScale)
        {
            txtSummary.Visibility = withSummary ? Visibility.Visible : Visibility.Collapsed;
            UpdateLayout();

            DrawingSize sizePx = new DrawingSize((int)Math.Ceiling(ActualWidth * dpiScale), (int)Math.Ceiling(ActualHeight * dpiScale));

            TaskbarPlacement.PlacementResult result = TaskbarPlacement.ComputePosition(trayRect, bandHeightPx, anchors, leftAligned, sizePx, sizePx.Width);
            return result.Fits ? (TaskbarPlacement.PlacementResult?)result : null;
        }


        /// <summary>Where the widget "would be" with the taskbar settled fully off-screen - start point for a hide ride, target for a reveal ride.</summary>
        private static int BelowEdgeTopPx(int monitorBottomPx)
        {
            return monitorBottomPx - 2;
        }


        private void HideWidget()
        {
            if (Visibility != Visibility.Hidden)
                Visibility = Visibility.Hidden;
        }


        private double GetDpiScale()
        {
            double scale = TaskbarInterop.GetDpiForWindow(hwnd) / 96.0;
            return scale <= 0 ? 1.0 : scale;
        }


        private static DrawingRectangle ToRectangle(TaskbarInterop.RECT r)
        {
            return DrawingRectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }


        private static bool IsTaskbarLeftAligned()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                {
                    object value = key == null ? null : key.GetValue("TaskbarAl");
                    return value is int taskbarAl && taskbarAl == 0;
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion


        #region anchor refresh
        private readonly object anchorLock = new object();
        private int? widgetsRightPx;
        private int? startLeftPx;
        private int? taskButtonsRightPx;
        private IntPtr anchorsTray;
        private DateTime lastAnchorQuery = DateTime.MinValue;
        private bool anchorQueryRunning;

        private void RefreshAnchors(IntPtr tray)
        {
            if (tray != anchorsTray)
            {
                anchorsTray = tray;
                lastAnchorQuery = DateTime.MinValue;
                lock (anchorLock)
                {
                    widgetsRightPx = null;
                    startLeftPx = null;
                    taskButtonsRightPx = null;
                }
            }

            if ((DateTime.UtcNow - lastAnchorQuery).TotalSeconds < 5)
                return;
            // Watchdog: a hung query (UI Automation against a dying shell)
            // cannot be allowed to freeze the anchors forever.
            if (anchorQueryRunning && (DateTime.UtcNow - lastAnchorQuery).TotalSeconds < 15)
                return;

            anchorQueryRunning = true;
            lastAnchorQuery = DateTime.UtcNow;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    TaskbarAnchors.Result result = TaskbarAnchors.Get(tray);
                    lock (anchorLock)
                    {
                        if (tray != anchorsTray || !result.Ok)
                            return; // stale target, or a failed read: keep the previous anchors

                        widgetsRightPx = result.WidgetsRightPx;
                        startLeftPx = result.StartLeftPx;
                        taskButtonsRightPx = result.TaskButtonsRightPx;
                    }
                }
                finally
                {
                    anchorQueryRunning = false;
                }
            });
        }
        #endregion


        #region taskbar location hook (auto-hide riding)
        private IntPtr hookedTray;
        private IntPtr trayLocHook;
        private TaskbarInterop.WinEventDelegate trayLocProc;
        private bool updateQueued;

        private void EnsureTrayLocationHook(IntPtr tray)
        {
            if (tray == hookedTray)
                return;

            if (trayLocHook != IntPtr.Zero)
            {
                TaskbarInterop.UnhookWinEvent(trayLocHook);
                trayLocHook = IntPtr.Zero;
            }

            hookedTray = tray;
            uint threadId = TaskbarInterop.GetWindowThreadProcessId(tray, out uint processId);

            trayLocProc = trayLocProc ?? ((hookHandle, eventType, eventHwnd, idObject, idChild, eventThread, eventTime) =>
            {
                if (eventHwnd != hookedTray || idObject != 0 || updateQueued)
                    return;

                updateQueued = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(UpdatePosition));
            });

            trayLocHook = TaskbarInterop.SetWinEventHook(
                TaskbarInterop.EVENT_OBJECT_LOCATIONCHANGE, TaskbarInterop.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero, trayLocProc, processId, threadId, TaskbarInterop.WINEVENT_OUTOFCONTEXT);
        }
        #endregion


        #region slide animation
        private bool barWasHidden;
        private bool rideAnimating;
        private bool rideDown;
        private DispatcherTimer rideTimer;

        private int lastLeftPx;
        private int lastTopPx;
        private DrawingSize lastSizePx;

        private TaskbarInterop.WinEventDelegate fgProc;
        private IntPtr fgHook;

        /// <summary>
        /// Animates the widget between its settled position and just off the
        /// bottom of the monitor, matching the taskbar's own auto-hide
        /// animation feel (fast start when hiding, slow finish when showing).
        /// </summary>
        private void StartRide(int leftPx, int fromTopPx, int toTopPx, bool down, int monitorBottomPx, double startPhase = 0)
        {
            if (lastSizePx.Width <= 0)
                return;

            var stopwatch = Stopwatch.StartNew();
            const double durationMs = 220;

            rideAnimating = true;
            rideDown = down;

            double eased0 = TaskbarPlacement.Ease(startPhase, down);
            int startTopPx = TaskbarPlacement.Interpolate(fromTopPx, toTopPx, eased0);
            TaskbarInterop.MoveWindowTo(hwnd, leftPx, startTopPx);
            TaskbarInterop.ClipWindowBottom(hwnd, lastSizePx.Width, lastSizePx.Height, monitorBottomPx - startTopPx);
            if (Visibility != Visibility.Visible)
                Visibility = Visibility.Visible;
            TaskbarInterop.EnsureTopmost(hwnd);

            rideTimer?.Stop();
            rideTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TimeSpan.FromMilliseconds(10) };
            rideTimer.Tick += (s, e) =>
            {
                double t = Math.Min(1.0, startPhase + stopwatch.ElapsedMilliseconds / durationMs * (1 - startPhase));
                double eased = TaskbarPlacement.Ease(t, down);
                int topPx = TaskbarPlacement.Interpolate(fromTopPx, toTopPx, eased);
                TaskbarInterop.MoveWindowTo(hwnd, leftPx, topPx);
                TaskbarInterop.ClipWindowBottom(hwnd, lastSizePx.Width, lastSizePx.Height, monitorBottomPx - topPx);

                if (t >= 1.0)
                {
                    rideTimer.Stop();
                    rideAnimating = false;
                    lastTopPx = topPx;
                    if (down)
                    {
                        barWasHidden = true;
                        Visibility = Visibility.Hidden;
                    }
                }
            };
            rideTimer.Start();
        }


        private void CancelRide()
        {
            rideTimer?.Stop();
            rideAnimating = false;
        }
        #endregion


        #region private members
        private readonly ActiveTimerViewModel viewModel;
        private readonly Settings settings;
        private MenuItem menuMonitor;
        private readonly Func<IntPtr> resolveTray;
        private readonly DispatcherTimer contentTicker;
        private readonly DispatcherTimer positionTimer;

        private IntPtr hwnd;
        private IntPtr ownerTray;
        private bool closed;
        private bool contextMenuOpen;
        #endregion
    }
}
