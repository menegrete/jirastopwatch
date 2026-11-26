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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

namespace StopWatch
{
    /// <summary>
    /// The floating mini view: issue key, summary and a running clock, always
    /// on top and out of the way. See the openspec capability
    /// "mini-timer-view".
    ///
    /// Owns the one-second UI tick. The application's own ticker runs at 30
    /// seconds because it is doing Jira round trips; a clock refreshed at that
    /// rate looks broken, so the window drives its own refresh while it is
    /// visible and stops as soon as it is not.
    ///
    /// Positions are handled in two coordinate systems on purpose. Screen
    /// geometry (working areas, <see cref="Window.Position"/>) is in device
    /// pixels, Avalonia's Width/Height are in device-independent units, and the
    /// window is deliberately larger than the visible pill so the drop shadow
    /// has somewhere to fall - so "snap to the screen edge" means the pill's
    /// edge, not the window's.
    ///
    /// <see cref="Window.Position"/> is never read back to learn where the
    /// window is: on X11 it only catches up with a requested move some time
    /// later. The window keeps its own record (<see cref="windowPos"/>) of the
    /// position it asked for, which <see cref="Window.PositionChanged"/>
    /// corrects when the window manager moves it (the user dragging it).
    /// The height is likewise not read from layout: it is a function of the
    /// number of rows, which is set explicitly.
    /// </summary>
    internal partial class MiniTimerWindow : Window
    {
        #region public events
        /// <summary>Raised when the user asks to go back to the full window.</summary>
        public event EventHandler RestoreRequested;
        #endregion


        #region public members
        /// <summary>
        /// Set while the application is shutting down, so that closing this
        /// window really closes it instead of being turned into a restore.
        /// </summary>
        public bool ClosingForShutdown { get; set; }
        #endregion


        #region public methods
        /// <summary>Designer / XAML loader only.</summary>
        public MiniTimerWindow()
        {
            InitializeComponent();
        }


        public MiniTimerWindow(ActiveTimerViewModel viewModel, Settings settings)
            : this()
        {
            if (viewModel == null)
                throw new ArgumentNullException("viewModel");
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.viewModel = viewModel;
            this.settings = settings;

            Width = DefaultPillWidth + 2 * ShadowMargin;
            Height = HeightForRows(1);

            rowsList.ItemsSource = rows;

            ticker = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
            ticker.Tick += ticker_Tick;

            dragEndTimer = new DispatcherTimer(DispatcherPriority.Normal);
            dragEndTimer.Tick += (s, e) => EndDrag();

            scale = RenderScaling;
            ScalingChanged += (s, e) => scale = RenderScaling;
            PositionChanged += MiniTimerWindow_PositionChanged;
            Opened += MiniTimerWindow_Opened;
            PointerPressed += MiniTimerWindow_PointerPressed;
            PointerReleased += MiniTimerWindow_PointerReleased;
        }


        /// <summary>
        /// Positions the window at the remembered location and shows it. The
        /// remembered location is validated first: the screen it was saved on
        /// may not be connected any more.
        /// </summary>
        public void ShowAt()
        {
            viewModel.Refresh();

            // No previous on-screen position to anchor against yet, so build
            // the row list without the anchor-aware repositioning - the
            // placement right after already accounts for however many rows
            // that leaves the pill.
            SyncRows(adjustPositionForRowCountChange: false);

            PlaceAtRememberedLocation();

            Show();
        }
        #endregion


        #region protected overrides
        /// <summary>
        /// The main window is hidden while this one is up, so letting this
        /// window close would leave the application running with nothing on
        /// screen and no way to reach it. Alt+F4 therefore means "go back".
        /// </summary>
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (ClosingForShutdown
                || e.CloseReason == WindowCloseReason.ApplicationShutdown
                || e.CloseReason == WindowCloseReason.OSShutdown)
            {
                ticker?.Stop();
                dragEndTimer?.Stop();
                return;
            }

            e.Cancel = true;
            RaiseRestoreRequested();
        }


        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property != IsVisibleProperty || ticker == null)
                return;

            if (change.GetNewValue<bool>())
            {
                viewModel.Refresh();
                SyncRows(adjustPositionForRowCountChange: false);
                ticker.Start();
            }
            else
            {
                ticker.Stop();
                dragEndTimer.Stop();
                dragging = false;
            }
        }
        #endregion


        #region private eventhandlers
        private void ticker_Tick(object sender, EventArgs e)
        {
            viewModel.Refresh();
            SyncRows(adjustPositionForRowCountChange: true);
        }


        /// <summary>
        /// The scale of the screen the window really landed on is only certain
        /// once it is open. If it is not the one the placement assumed, the
        /// width and position are worked out again with the right one.
        /// </summary>
        private void MiniTimerWindow_Opened(object sender, EventArgs e)
        {
            double actual = RenderScaling;
            if (Math.Abs(actual - scale) < 0.001)
                return;

            scale = actual;
            PlaceAtRememberedLocation();
        }


        /// <summary>
        /// The window manager moved the window (the user dragging it). While a
        /// width drag is running the position is this window's to decide, so
        /// whatever is reported then is ignored.
        /// </summary>
        private void MiniTimerWindow_PositionChanged(object sender, PixelPointEventArgs e)
        {
            if (resizing)
                return;

            windowPos = e.Point;

            if (dragging)
            {
                dragMoved = true;
                RestartDragEndTimer();
            }
        }


        private void MiniTimerWindow_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            // A double click anywhere on the background goes back to the full
            // window, the same as the restore button - checked first so the
            // second click of the pair never starts a (zero-distance) drag.
            // Only reached when no button handled the press first, so pressing
            // pause or restore never drags the window.
            if (e.ClickCount == 2)
            {
                RaiseRestoreRequested();
                return;
            }

            BeginDrag(e);
        }


        /// <summary>Not always delivered: the window manager owns the pointer for the length of a drag.</summary>
        private void MiniTimerWindow_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            EndDrag();
        }


        private void RowToggle_Click(object sender, RoutedEventArgs e)
        {
            MiniTimerRowViewModel row = RowOf(sender);
            if (row == null)
                return;

            row.ToggleActive();
            viewModel.Refresh();
            SyncRows(adjustPositionForRowCountChange: true);
        }


        private void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            RaiseRestoreRequested();
        }


        private void btnCopyKey_Click(object sender, RoutedEventArgs e)
        {
            MiniTimerRowViewModel row = RowOf(sender);
            if (row == null || string.IsNullOrEmpty(row.IssueKey))
                return;

            CopyToClipboardAsync(row.IssueKey).FireAndForget();
            GlyphButtonHelpers.FlashCopyConfirmation((Button)sender);
        }


        private async Task CopyToClipboardAsync(string text)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(text);
        }


        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            MiniTimerRowViewModel row = RowOf(sender);
            if (row == null || string.IsNullOrEmpty(row.Summary))
                return;

            string url = JiraKeyHelpers.BuildIssueUrl(settings.JiraBaseUrl, row.IssueKey);
            if (url == null)
                return;

            AppInfo.OpenUrl(url);
        }


        /// <summary>
        /// Shows a row's copy/open buttons and cancels any pending hide from a
        /// previous, still-grace-period PointerExited.
        /// </summary>
        private void keyArea_PointerEntered(object sender, PointerEventArgs e)
        {
            Grid keyArea = (Grid)sender;
            CancelPendingHide(keyArea);
            SetActionButtonsVisible(keyArea, visible: true);
        }


        /// <summary>
        /// Hides a row's copy/open buttons after a short delay instead of
        /// immediately - moving from the key text to a button crosses a gap
        /// that is briefly outside keyArea (or can be skipped over entirely by
        /// a single coalesced fast pointer move), which would otherwise hide
        /// the buttons out from under the cursor before a click ever lands.
        /// </summary>
        private void keyArea_PointerExited(object sender, PointerEventArgs e)
        {
            Grid keyArea = (Grid)sender;
            CancelPendingHide(keyArea);

            var hideTimer = new DispatcherTimer { Interval = HoverGracePeriod };
            hideTimer.Tick += (s, args) =>
            {
                hideTimer.Stop();
                keyArea.Tag = null;
                SetActionButtonsVisible(keyArea, visible: false);
            };
            keyArea.Tag = hideTimer;
            hideTimer.Start();
        }


        private static void CancelPendingHide(Grid keyArea)
        {
            var pending = keyArea.Tag as DispatcherTimer;
            if (pending == null)
                return;

            pending.Stop();
            keyArea.Tag = null;
        }


        private static void SetActionButtonsVisible(Grid keyArea, bool visible)
        {
            foreach (Control child in keyArea.Children)
            {
                Button button = child as Button;
                if (button != null)
                    button.IsVisible = visible;
            }
        }
        #endregion


        #region moving
        /// <summary>
        /// Hands the drag to the window manager. Where that call blocks until
        /// the button is released (Windows runs the move loop inside it) the
        /// drag is over as soon as it returns; where it returns at once (X11)
        /// there may be no release event either, so the drag is taken to have
        /// ended when the window stops reporting moves for a moment.
        /// </summary>
        private void BeginDrag(PointerPressedEventArgs e)
        {
            dragging = true;
            dragMoved = false;
            dragStart = windowPos;

            try
            {
                BeginMoveDrag(e);
            }
            catch (InvalidOperationException)
            {
                // The button was already released. Nothing moved.
                dragging = false;
                return;
            }

            if (OperatingSystem.IsWindows())
                EndDrag();
            else
                RestartDragEndTimer();
        }


        private void RestartDragEndTimer()
        {
            if (OperatingSystem.IsWindows())
                return;

            dragEndTimer.Stop();
            dragEndTimer.Interval = dragMoved ? DragIdleAfterMove : DragIdleBeforeMove;
            dragEndTimer.Start();
        }


        private void EndDrag()
        {
            dragEndTimer.Stop();

            if (!dragging)
                return;

            dragging = false;

            if (dragMoved || windowPos != dragStart)
                SnapAndRemember();
        }


        private void SnapAndRemember()
        {
            DrawingPoint location = PillLocation;
            DrawingSize size = PillSize;

            DrawingRectangle workingArea = ScreenAreas.From(Screens).NearestWorkingArea(location);
            DrawingPoint snapped = ScreenPlacement.SnapToEdges(location, size, workingArea, SnapThreshold);

            if (snapped != location)
                MovePillTo(snapped);

            RememberLocation();
        }
        #endregion


        #region resizing
        private void grip_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            // Handled, so the window's own press handler (drag, double click)
            // never sees it.
            e.Handled = true;

            resizing = true;
            resizeGrip = (Control)sender;
            resizeAreas = ScreenAreas.From(Screens);
            resizeStartPointerX = PointerScreenX(e);
            resizeStartLocation = PillLocation;
            resizeStartWidth = PillSize.Width;

            e.Pointer.Capture(resizeGrip);
        }


        private void grip_PointerMoved(object sender, PointerEventArgs e)
        {
            if (!resizing || !ReferenceEquals(sender, resizeGrip))
                return;

            int moved = (int)Math.Round(PointerScreenX(e) - resizeStartPointerX);

            if (ReferenceEquals(sender, gripRight))
                ResizePill(resizeStartWidth + moved, keepRightEdge: false);
            else
                ResizePill(resizeStartWidth - moved, keepRightEdge: true);
        }


        private void grip_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            e.Pointer.Capture(null);
            EndResize();
        }


        private void grip_PointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            EndResize();
        }


        private void EndResize()
        {
            if (!resizing)
                return;

            resizing = false;
            resizeGrip = null;

            settings.MiniViewWidth = (int)Math.Round(Width - 2 * ShadowMargin);

            // Dragging the left edge moves the window as well as widening it.
            RememberLocation();
        }


        /// <summary>
        /// Where the pointer is on the screen, in device pixels, worked out
        /// from the window's own position (the one it was last asked for or
        /// reported) rather than by asking the system, which would answer with
        /// a position that may not have caught up with a move just made.
        /// </summary>
        private double PointerScreenX(PointerEventArgs e)
        {
            return windowPos.X + e.GetPosition(this).X * scale;
        }


        /// <summary>
        /// Resizes the pill to <paramref name="desiredWidth"/> (device pixels,
        /// before limits), keeping the edge the user is not dragging where it
        /// was when the drag started. Measured from where the drag started
        /// rather than step by step, so hitting a limit and coming back does
        /// not leave the edge out of step with the pointer.
        /// </summary>
        private void ResizePill(int desiredWidth, bool keepRightEdge)
        {
            DrawingRectangle workingArea = resizeAreas.NearestWorkingArea(resizeStartLocation);

            SetPillWidth(ClampPillWidth(desiredWidth, workingArea));

            DrawingSize size = PillSize;
            int x = keepRightEdge
                ? resizeStartLocation.X + resizeStartWidth - size.Width
                : resizeStartLocation.X;

            // Widening towards a screen edge may need the other edge to give
            // way. The top-left is brought onto the screen first, because
            // EnsureOnScreen treats a position on no screen as "forgotten"
            // and would send the pill to the fallback corner.
            DrawingPoint desired = new DrawingPoint(
                Math.Min(Math.Max(x, workingArea.Left), workingArea.Right - 1),
                resizeStartLocation.Y);

            MovePillTo(ScreenPlacement.EnsureOnScreen(desired, size, new[] { workingArea }, workingArea));
        }
        #endregion


        #region private methods
        /// <summary>The row a control inside the row template belongs to.</summary>
        private static MiniTimerRowViewModel RowOf(object sender)
        {
            Control element = sender as Control;
            return element == null ? null : element.DataContext as MiniTimerRowViewModel;
        }


        private void RaiseRestoreRequested()
        {
            EventHandler handler = RestoreRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }


        /// <summary>
        /// Brings <see cref="rows"/> in line with the sources
        /// <see cref="ActiveTimerViewModel.TrackedSources"/> says to list right
        /// now (added/removed/reordered rows are matched by their underlying
        /// source, so an unchanged row is not rebuilt) and refreshes what
        /// each one shows.
        ///
        /// When the row count changes, the window's height changes with it.
        /// <paramref name="adjustPositionForRowCountChange"/> controls whether
        /// this method also repositions the window so it grows away from
        /// whichever screen edge it is anchored to, rather than always growing
        /// downward - pass false when there is no meaningful "before" position
        /// yet (the window is not visible, or is only just becoming visible).
        /// </summary>
        private void SyncRows(bool adjustPositionForRowCountChange)
        {
            List<ITimerSource> sources = viewModel.TrackedSources.ToList();
            bool countChanged = rows.Count != sources.Count;

            bool adjust = countChanged && adjustPositionForRowCountChange && IsVisible && !dragging && !resizing;
            AnchorEdge anchor = adjust ? DetermineAnchor() : AnchorEdge.None;
            int bottomBefore = anchor == AnchorEdge.Bottom ? PillLocation.Y + PillSize.Height : 0;

            for (int i = rows.Count - 1; i >= 0; i--)
                if (!sources.Contains(rows[i].Source))
                    rows.RemoveAt(i);

            for (int i = 0; i < sources.Count; i++)
            {
                int existingIndex = IndexOfSource(sources[i]);
                if (existingIndex == -1)
                    rows.Insert(Math.Min(i, rows.Count), new MiniTimerRowViewModel(sources[i]));
                else if (existingIndex != i)
                    rows.Move(existingIndex, i);
            }

            foreach (MiniTimerRowViewModel row in rows)
                row.Refresh();

            double height = HeightForRows(rows.Count);
            if (Height != height)
                Height = height;

            if (!adjust)
                return;

            DrawingPoint newLocation = anchor == AnchorEdge.Bottom
                ? new DrawingPoint(PillLocation.X, bottomBefore - PillSize.Height)
                : PillLocation;

            MovePillTo(ScreenAreas.From(Screens).EnsureOnScreen(newLocation, PillSize));
            RememberLocation();
        }


        private int IndexOfSource(ITimerSource source)
        {
            for (int i = 0; i < rows.Count; i++)
                if (ReferenceEquals(rows[i].Source, source))
                    return i;

            return -1;
        }


        /// <summary>
        /// Which edge of its screen's working area the pill is currently
        /// pegged to, by the same closeness threshold <see cref="SnapAndRemember"/>
        /// uses to decide it snapped there in the first place.
        /// </summary>
        private AnchorEdge DetermineAnchor()
        {
            DrawingPoint location = PillLocation;
            DrawingSize size = PillSize;
            DrawingRectangle workingArea = ScreenAreas.From(Screens).NearestWorkingArea(location);

            if (Math.Abs(workingArea.Bottom - (location.Y + size.Height)) <= SnapThreshold)
                return AnchorEdge.Bottom;
            if (Math.Abs(location.Y - workingArea.Top) <= SnapThreshold)
                return AnchorEdge.Top;

            return AnchorEdge.None;
        }


        /// <summary>
        /// Applies the remembered width (or the default) and puts the pill at
        /// the remembered location, brought back onto a connected screen if
        /// need be. The width comes first: the position depends on the pill's
        /// final size.
        /// </summary>
        private void PlaceAtRememberedLocation()
        {
            ScreenAreas areas = ScreenAreas.From(Screens);

            DrawingPoint desired;
            bool remembered = ScreenPlacement.TryParseLocation(settings.MiniViewLocation, out desired);

            // The width is fitted to the screen the pill will land on, which
            // is not known until the position is: the remembered one, or else
            // the primary screen the fallback uses.
            DrawingRectangle workingArea = remembered ? areas.NearestWorkingArea(desired) : areas.PrimaryWorkingArea;
            scale = ScaleOf(workingArea);

            double units = settings.MiniViewWidth > 0 ? settings.MiniViewWidth : DefaultPillWidth;
            SetPillWidth(ClampPillWidth((int)Math.Round(units * scale), workingArea));

            DrawingSize pill = PillSize;

            if (!remembered)
                desired = areas.FallbackLocation(pill);

            MovePillTo(areas.EnsureOnScreen(desired, pill));
            RememberLocation();
        }


        /// <summary>Device pixels per device-independent unit on the screen that holds this working area.</summary>
        private double ScaleOf(DrawingRectangle workingArea)
        {
            var center = new PixelPoint(workingArea.Left + workingArea.Width / 2, workingArea.Top + workingArea.Height / 2);
            double screenScale = Screens?.ScreenFromPoint(center)?.Scaling ?? RenderScaling;

            return screenScale <= 0 ? 1.0 : screenScale;
        }


        /// <summary>The window's height for a number of rows: each one is fixed, plus the pill's border and the shadow margin.</summary>
        private static double HeightForRows(int rowCount)
        {
            return Math.Max(1, rowCount) * RowHeight + 2 * PillBorder + 2 * ShadowMargin;
        }


        /// <summary>The shadow margin in whole device pixels, so that pill and window positions convert both ways exactly.</summary>
        private int MarginPx
        {
            get { return (int)Math.Round(ShadowMargin * scale); }
        }


        /// <summary>The visible pill's size in device pixels, shadow margin excluded.</summary>
        private DrawingSize PillSize
        {
            get
            {
                return new DrawingSize(
                    (int)Math.Round((Width - 2 * ShadowMargin) * scale),
                    (int)Math.Round((Height - 2 * ShadowMargin) * scale));
            }
        }


        /// <summary>The visible pill's top-left in device pixels.</summary>
        private DrawingPoint PillLocation
        {
            get { return new DrawingPoint(windowPos.X + MarginPx, windowPos.Y + MarginPx); }
        }


        /// <summary>Places the window so that the pill's top-left lands on the given pixel.</summary>
        private void MovePillTo(DrawingPoint pillLocation)
        {
            windowPos = new PixelPoint(pillLocation.X - MarginPx, pillLocation.Y - MarginPx);
            Position = windowPos;
        }


        /// <summary>Sets the pill's width in device pixels, shadow margin added on top.</summary>
        private void SetPillWidth(int pillWidthPixels)
        {
            Width = pillWidthPixels / scale + 2 * ShadowMargin;
        }


        /// <summary>
        /// Brings a pill width in device pixels within what fits the given
        /// screen: no narrower than <see cref="MinPillWidth"/>, no wider than
        /// the working area.
        /// </summary>
        private int ClampPillWidth(int desiredPixels, DrawingRectangle workingArea)
        {
            return ScreenPlacement.ClampWidth(desiredPixels, (int)Math.Round(MinPillWidth * scale), workingArea);
        }


        private void RememberLocation()
        {
            settings.MiniViewLocation = ScreenPlacement.FormatLocation(PillLocation);
        }
        #endregion


        #region private members
        /// <summary>
        /// Must match the Margin on the pill Border in the XAML: the window is
        /// that much bigger than the pill on every side so the shadow is not
        /// clipped.
        /// </summary>
        private const double ShadowMargin = 6;

        /// <summary>Must match the Height of a row in the XAML.</summary>
        private const double RowHeight = 28;

        /// <summary>Must match the pill Border's BorderThickness in the XAML (one side).</summary>
        private const double PillBorder = 1;

        /// <summary>
        /// Width of the visible pill, in device-independent units, until the
        /// user drags it to something else. Also what a saved width of 0
        /// ("never resized") stands for.
        /// </summary>
        private const double DefaultPillWidth = 340;

        /// <summary>
        /// Narrowest the pill can be dragged, in device-independent units:
        /// enough for the key, the clock, the pause button and the restore
        /// button that floats over the top-right corner.
        /// </summary>
        private const double MinPillWidth = 340;

        /// <summary>How close to an edge counts as "dropped on it", in pixels.</summary>
        private const int SnapThreshold = 20;

        /// <summary>
        /// How long a row's copy/open buttons stay visible after the pointer
        /// leaves its key area before hiding, so crossing the gap between the
        /// key and a button - or a fast pointer move that skips it in one
        /// event - doesn't hide them before a click lands.
        /// </summary>
        private static readonly TimeSpan HoverGracePeriod = TimeSpan.FromMilliseconds(300);

        /// <summary>Where the window manager does not tell us the drag ended: quiet time after the last move that counts as "dropped".</summary>
        private static readonly TimeSpan DragIdleAfterMove = TimeSpan.FromMilliseconds(300);

        /// <summary>The same, while the window has not moved yet (the button is held but still).</summary>
        private static readonly TimeSpan DragIdleBeforeMove = TimeSpan.FromMilliseconds(1500);

        private readonly ActiveTimerViewModel viewModel;
        private readonly Settings settings;
        private readonly DispatcherTimer ticker;
        private readonly DispatcherTimer dragEndTimer;
        private readonly ObservableCollection<MiniTimerRowViewModel> rows = new ObservableCollection<MiniTimerRowViewModel>();

        /// <summary>
        /// Where this window is, in device pixels: the position it was last
        /// asked to take, or the one the window manager last reported. Not
        /// <see cref="Window.Position"/>, which may be stale (see the class
        /// summary).
        /// </summary>
        private PixelPoint windowPos;

        /// <summary>Device pixels per device-independent unit of the screen the window is on (or is about to be placed on).</summary>
        private double scale = 1.0;

        private bool dragging;
        private bool dragMoved;
        private PixelPoint dragStart;

        private bool resizing;
        private Control resizeGrip;
        private ScreenAreas resizeAreas;

        /// <summary>Pointer X, in screen pixels, when the current width drag started.</summary>
        private double resizeStartPointerX;

        /// <summary>Pill location, in device pixels, when the current width drag started.</summary>
        private DrawingPoint resizeStartLocation;

        /// <summary>Pill width, in device pixels, when the current width drag started.</summary>
        private int resizeStartWidth;
        #endregion


        #region private types
        /// <summary>Which edge of the screen, if any, the pill is currently pegged to. See <see cref="DetermineAnchor"/>.</summary>
        private enum AnchorEdge
        {
            None,
            Top,
            Bottom
        }
        #endregion
    }
}
