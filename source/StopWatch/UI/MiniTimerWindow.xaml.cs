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
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using Screen = System.Windows.Forms.Screen;

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
    /// geometry is in device pixels, WPF's Left/Top/Width/Height are in
    /// device-independent units, and the window is deliberately larger than the
    /// visible pill so the drop shadow has somewhere to fall - so "snap to the
    /// screen edge" means the pill's edge, not the window's.
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
        public MiniTimerWindow(ActiveTimerViewModel viewModel, Settings settings)
        {
            if (viewModel == null)
                throw new ArgumentNullException("viewModel");
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.viewModel = viewModel;
            this.settings = settings;

            InitializeComponent();

            DataContext = viewModel;
            rowsList.ItemsSource = rows;

            ticker = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
            ticker.Interval = TimeSpan.FromSeconds(1);
            ticker.Tick += ticker_Tick;

            IsVisibleChanged += MiniTimerWindow_IsVisibleChanged;
            MouseLeftButtonDown += MiniTimerWindow_MouseLeftButtonDown;
            Closing += MiniTimerWindow_Closing;
        }


        /// <summary>
        /// Positions the window at the remembered location and shows it. The
        /// remembered location is validated first: the screen it was saved on
        /// may not be connected any more.
        /// </summary>
        public void ShowAt()
        {
            viewModel.Refresh();

            // Showing first gives the window a PresentationSource, without
            // which the DPI scale of the screen it lands on is unknown.
            Show();

            // No previous on-screen position to anchor against yet, so build
            // the row list without the anchor-aware repositioning below - the
            // placement logic right after already accounts for however many
            // rows that leaves the pill.
            SyncRows(adjustPositionForRowCountChange: false);

            DrawingSize pill = PillSize;

            DrawingPoint desired;
            if (!ScreenPlacement.TryParseLocation(settings.MiniViewLocation, out desired))
                desired = ScreenPlacement.FallbackLocation(pill);

            MovePillTo(ScreenPlacement.EnsureOnScreen(desired, pill));
            RememberLocation();
        }
        #endregion


        #region private eventhandlers
        private void MiniTimerWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                viewModel.Refresh();
                SyncRows(adjustPositionForRowCountChange: false);
                ticker.Start();
            }
            else
            {
                ticker.Stop();
            }
        }


        private void ticker_Tick(object sender, EventArgs e)
        {
            viewModel.Refresh();
            SyncRows(adjustPositionForRowCountChange: true);
        }


        private void MiniTimerWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // A double click anywhere on the background goes back to the full
            // window, the same as the restore button - checked first so the
            // second click of the pair never starts a (zero-distance) drag.
            if (e.ClickCount == 2)
            {
                RaiseRestoreRequested();
                return;
            }

            // Only reached when no button handled the click first, so pressing
            // pause or restore never drags the window.
            if (e.ButtonState != MouseButtonState.Pressed)
                return;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // The button was already released. Nothing moved.
                return;
            }

            SnapAndRemember();
        }


        /// <summary>
        /// The main window is hidden while this one is up, so letting this
        /// window close would leave the application running with nothing on
        /// screen and no way to reach it. Alt+F4 therefore means "go back".
        /// </summary>
        private void MiniTimerWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ClosingForShutdown)
                return;

            e.Cancel = true;

            EventHandler handler = RestoreRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }


        private void RowToggle_Click(object sender, RoutedEventArgs e)
        {
            MiniTimerRowViewModel row = (sender as FrameworkElement)?.DataContext as MiniTimerRowViewModel;
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
        #endregion


        #region private methods
        private void RaiseRestoreRequested()
        {
            EventHandler handler = RestoreRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }


        /// <summary>
        /// Every source the mini view should list right now: every running
        /// timer, or - if none are running - the single issue
        /// <see cref="ActiveTimerViewModel"/> resolves, same as before this
        /// window could show more than one row.
        /// </summary>
        private List<ITimerSource> ResolveSources()
        {
            List<ITimerSource> running = viewModel.RunningSources.ToList();
            if (running.Count > 0)
                return running;

            return viewModel.ActiveSource != null
                ? new List<ITimerSource> { viewModel.ActiveSource }
                : new List<ITimerSource>();
        }


        /// <summary>
        /// Brings <see cref="rows"/> in line with the current sources
        /// (added/removed/reordered rows are matched by their underlying
        /// source, so an unchanged row is not rebuilt) and refreshes what
        /// each one shows.
        ///
        /// When the row count changes, the window's height changes with it
        /// (SizeToContent="Height" in the XAML). <paramref name="adjustPositionForRowCountChange"/>
        /// controls whether this method also repositions the window so it
        /// grows away from whichever screen edge it is anchored to, rather
        /// than always growing downward - pass false when there is no
        /// meaningful "before" position yet (the window is not visible, or is
        /// only just becoming visible).
        /// </summary>
        private void SyncRows(bool adjustPositionForRowCountChange)
        {
            List<ITimerSource> sources = ResolveSources();
            bool countChanged = rows.Count != sources.Count;

            bool adjust = countChanged && adjustPositionForRowCountChange && IsVisible;
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

            if (!adjust)
                return;

            // Force layout now so Height (and therefore PillSize) already
            // reflects the new row count before it is used below.
            UpdateLayout();

            DrawingPoint newLocation = anchor == AnchorEdge.Bottom
                ? new DrawingPoint(PillLocation.X, bottomBefore - PillSize.Height)
                : PillLocation;

            MovePillTo(ScreenPlacement.EnsureOnScreen(newLocation, PillSize));
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
        /// pegged to, by the same closeness threshold <see cref="SnapToEdges"/>
        /// uses to decide it snapped there in the first place.
        /// </summary>
        private AnchorEdge DetermineAnchor()
        {
            DrawingPoint location = PillLocation;
            DrawingSize size = PillSize;
            DrawingRectangle workingArea = Screen.FromPoint(location).WorkingArea;

            if (Math.Abs(workingArea.Bottom - (location.Y + size.Height)) <= SnapThreshold)
                return AnchorEdge.Bottom;
            if (Math.Abs(location.Y - workingArea.Top) <= SnapThreshold)
                return AnchorEdge.Top;

            return AnchorEdge.None;
        }


        /// <summary>
        /// Device pixels per device-independent unit, for the screen this
        /// window is currently on. Falls back to 1:1 before the window has a
        /// presentation source.
        /// </summary>
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


        /// <summary>The visible pill's size in device pixels, shadow margin excluded.</summary>
        private DrawingSize PillSize
        {
            get
            {
                double scaleX;
                double scaleY;
                GetScale(out scaleX, out scaleY);

                return new DrawingSize(
                    (int)Math.Ceiling((Width - 2 * ShadowMargin) * scaleX),
                    (int)Math.Ceiling((Height - 2 * ShadowMargin) * scaleY));
            }
        }


        /// <summary>The visible pill's top-left in device pixels.</summary>
        private DrawingPoint PillLocation
        {
            get
            {
                double scaleX;
                double scaleY;
                GetScale(out scaleX, out scaleY);

                return new DrawingPoint(
                    (int)Math.Round((Left + ShadowMargin) * scaleX),
                    (int)Math.Round((Top + ShadowMargin) * scaleY));
            }
        }


        /// <summary>Places the window so that the pill's top-left lands on the given pixel.</summary>
        private void MovePillTo(DrawingPoint pillLocation)
        {
            double scaleX;
            double scaleY;
            GetScale(out scaleX, out scaleY);

            Left = pillLocation.X / scaleX - ShadowMargin;
            Top = pillLocation.Y / scaleY - ShadowMargin;
        }


        private void SnapAndRemember()
        {
            DrawingPoint location = PillLocation;
            DrawingSize size = PillSize;

            DrawingRectangle workingArea = Screen.FromPoint(location).WorkingArea;
            DrawingPoint snapped = ScreenPlacement.SnapToEdges(location, size, workingArea, SnapThreshold);

            if (snapped != location)
                MovePillTo(snapped);

            RememberLocation();
        }


        private void RememberLocation()
        {
            settings.MiniViewLocation = ScreenPlacement.FormatLocation(PillLocation);
        }
        #endregion


        #region private members
        /// <summary>
        /// Must match the Margin on the root Border in the XAML: the window is
        /// that much bigger than the pill on every side so the shadow is not
        /// clipped.
        /// </summary>
        private const double ShadowMargin = 6;

        /// <summary>How close to an edge counts as "dropped on it", in pixels.</summary>
        private const int SnapThreshold = 20;

        private readonly ActiveTimerViewModel viewModel;
        private readonly Settings settings;
        private readonly DispatcherTimer ticker;
        private readonly ObservableCollection<MiniTimerRowViewModel> rows = new ObservableCollection<MiniTimerRowViewModel>();
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
