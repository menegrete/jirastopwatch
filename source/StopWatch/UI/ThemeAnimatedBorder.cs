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
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace StopWatch
{
    /// <summary>
    /// A Border whose background crosses between two theme colours instead of
    /// jumping, used wherever colour reports a state change - a timer starting
    /// or pausing, a row becoming the selected one.
    ///
    /// See the ui-theming spec, "Los cambios de estado se presentan con una
    /// transicion". The crossing is a <see cref="BrushTransition"/> on
    /// Background, which gives the things that spec asks for without a
    /// storyboard:
    ///
    /// - The arrival colour belongs to the theme in effect now: the brush is
    ///   looked up in the theme variant the control is actually showing, and
    ///   again whenever that variant changes.
    /// - A second state change replaces the transition in flight rather than
    ///   queueing behind it (assigning Background restarts the transition from
    ///   the current value).
    /// - The colour is never the only carrier of the state. Whoever uses this
    ///   puts a glyph or a stripe alongside it; this class only animates.
    ///
    /// The colours are named by palette key (the Theme property names, as in
    /// <see cref="ThemeBrushes"/>), not by value, so the palette stays the
    /// single source of colour.
    /// </summary>
    internal class ThemeAnimatedBorder : Border
    {
        #region public members
        /// <summary>Which of the two colours to show.</summary>
        public static readonly StyledProperty<bool> IsActiveProperty =
            AvaloniaProperty.Register<ThemeAnimatedBorder, bool>(nameof(IsActive));

        /// <summary>Palette key to use when active.</summary>
        public static readonly StyledProperty<string> ActiveColorProperty =
            AvaloniaProperty.Register<ThemeAnimatedBorder, string>(nameof(ActiveColor), "SurfaceActive");

        /// <summary>Palette key to use when inactive.</summary>
        public static readonly StyledProperty<string> InactiveColorProperty =
            AvaloniaProperty.Register<ThemeAnimatedBorder, string>(nameof(InactiveColor), "Surface");

        public bool IsActive
        {
            get { return GetValue(IsActiveProperty); }
            set { SetValue(IsActiveProperty, value); }
        }

        public string ActiveColor
        {
            get { return GetValue(ActiveColorProperty); }
            set { SetValue(ActiveColorProperty, value); }
        }

        public string InactiveColor
        {
            get { return GetValue(InactiveColorProperty); }
            set { SetValue(InactiveColorProperty, value); }
        }


        /// <summary>
        /// Long enough to read as a transition, short enough not to delay
        /// reading the new state.
        /// </summary>
        public static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(180);
        #endregion


        #region public methods
        static ThemeAnimatedBorder()
        {
            IsActiveProperty.Changed.AddClassHandler<ThemeAnimatedBorder>((border, e) => border.Animate());
            ActiveColorProperty.Changed.AddClassHandler<ThemeAnimatedBorder>((border, e) => border.Animate());
            InactiveColorProperty.Changed.AddClassHandler<ThemeAnimatedBorder>((border, e) => border.Animate());
        }


        public ThemeAnimatedBorder()
        {
            transitions = new Transitions
            {
                new BrushTransition
                {
                    Property = BackgroundProperty,
                    Duration = TransitionDuration,
                    Easing = new QuadraticEaseOut()
                }
            };

            ActualThemeVariantChanged += (s, e) => SnapToState();
        }


        /// <summary>
        /// Jumps to the colour the current state calls for, with no animation.
        /// Used at startup and after a theme change, where there is no state
        /// change to report.
        /// </summary>
        public void SnapToState()
        {
            Transitions = null;
            Background = Target;
            Transitions = transitions;
        }
        #endregion


        #region protected methods
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            // A row that arrives already selected, or already running, should
            // start at its colour rather than animate into it.
            SnapToState();
        }


        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
        }
        #endregion


        #region private methods
        private void Animate()
        {
            // Attached: the transition installed by SnapToState crosses over
            // to the new brush, and replaces one that is still running.
            // Detached: Background just takes the colour, nothing to animate.
            Background = Target;
        }


        /// <summary>The brush the current state calls for, in the theme being shown.</summary>
        private IBrush Target
        {
            get { return Resolve(IsActive ? ActiveColor : InactiveColor); }
        }


        private IBrush Resolve(string themeColorName)
        {
            if (!string.IsNullOrEmpty(themeColorName)
                && this.TryFindResource(themeColorName, ActualThemeVariant, out object found)
                && found is IBrush brush)
                return brush;

            // Not published yet (before the application's resources are
            // installed): the palette itself is the fallback.
            return ThemeBrushes.Current(themeColorName ?? "");
        }
        #endregion


        #region private members
        private readonly Transitions transitions;
        #endregion
    }
}
