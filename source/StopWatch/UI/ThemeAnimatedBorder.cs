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
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StopWatch
{
    /// <summary>
    /// A Border whose background crosses between two theme colours instead of
    /// jumping, used wherever colour reports a state change - a timer starting
    /// or pausing, a row becoming the selected one.
    ///
    /// See the ui-theming spec, "Los cambios de estado se presentan con una
    /// transicion", and the openspec design, D7. Three things that spec asks
    /// for are why this is code rather than a XAML storyboard:
    ///
    /// - The arrival colour has to belong to the theme in effect when the
    ///   animation ends, not when it started, so it is resolved again on
    ///   completion.
    /// - A second state change has to replace the animation in flight rather
    ///   than queue behind it, so each run hands over the same brush and WPF
    ///   drops the previous animation on it.
    /// - The colour is never the only carrier of the state. Whoever uses this
    ///   puts a glyph or a stripe alongside it; this class only animates.
    ///
    /// The colours are named by Theme property, not by value, so the palette
    /// stays the single source of colour.
    /// </summary>
    internal class ThemeAnimatedBorder : Border
    {
        #region public members
        /// <summary>Which of the two colours to show.</summary>
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(
                "IsActive", typeof(bool), typeof(ThemeAnimatedBorder),
                new PropertyMetadata(false, OnStateChanged));

        /// <summary>Name of the <see cref="Theme"/> property to use when active.</summary>
        public static readonly DependencyProperty ActiveColorProperty =
            DependencyProperty.Register(
                "ActiveColor", typeof(string), typeof(ThemeAnimatedBorder),
                new PropertyMetadata("SurfaceActive", OnStateChanged));

        /// <summary>Name of the <see cref="Theme"/> property to use when inactive.</summary>
        public static readonly DependencyProperty InactiveColorProperty =
            DependencyProperty.Register(
                "InactiveColor", typeof(string), typeof(ThemeAnimatedBorder),
                new PropertyMetadata("Surface", OnStateChanged));

        public bool IsActive
        {
            get { return (bool)GetValue(IsActiveProperty); }
            set { SetValue(IsActiveProperty, value); }
        }

        public string ActiveColor
        {
            get { return (string)GetValue(ActiveColorProperty); }
            set { SetValue(ActiveColorProperty, value); }
        }

        public string InactiveColor
        {
            get { return (string)GetValue(InactiveColorProperty); }
            set { SetValue(InactiveColorProperty, value); }
        }


        /// <summary>
        /// Long enough to read as a transition, short enough not to delay
        /// reading the new state.
        /// </summary>
        public static readonly Duration TransitionDuration = new Duration(TimeSpan.FromMilliseconds(180));
        #endregion


        #region public methods
        public ThemeAnimatedBorder()
        {
            // Its own unfrozen brush: a theme brush from the resource
            // dictionary is frozen and shared, so animating it is neither
            // allowed nor local to this element.
            brush = new SolidColorBrush(Target);
            Background = brush;

            Loaded += ThemeAnimatedBorder_Loaded;
        }


        /// <summary>
        /// Jumps to the colour the current state calls for, with no animation.
        /// Used at startup and after a theme change, where there is no state
        /// change to report.
        /// </summary>
        public void SnapToState()
        {
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = Target;
        }
        #endregion


        #region private methods
        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ThemeAnimatedBorder)d).Animate();
        }


        private void ThemeAnimatedBorder_Loaded(object sender, RoutedEventArgs e)
        {
            // A row that arrives already selected, or already running, should
            // start at its colour rather than animate into it.
            SnapToState();
        }


        private void Animate()
        {
            if (!IsLoaded)
            {
                SnapToState();
                return;
            }

            var animation = new ColorAnimation
            {
                To = Target,
                Duration = TransitionDuration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };

            // On completion the animation stops holding the value, so the
            // colour has to be written for keeps - and it is read from the
            // theme again here, which is what makes a theme change mid
            // transition land on the new palette.
            animation.Completed += (s, e) => brush.Color = Target;

            // Handing the same brush a new animation replaces whatever was
            // running on it; nothing queues.
            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }


        /// <summary>The colour the current state calls for, in the active theme.</summary>
        private Color Target
        {
            get { return Resolve(IsActive ? ActiveColor : InactiveColor); }
        }


        private static Color Resolve(string themeColorName)
        {
            System.Reflection.PropertyInfo property =
                typeof(Theme).GetProperty(themeColorName ?? "");

            if (property == null || property.PropertyType != typeof(System.Drawing.Color))
                return Colors.Transparent;

            return ThemeBrushes.ToMediaColor((System.Drawing.Color)property.GetValue(Theme.Current));
        }
        #endregion


        #region private members
        private readonly SolidColorBrush brush;
        #endregion
    }
}
