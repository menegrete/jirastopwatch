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

using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace StopWatch
{
    /// <summary>
    /// Avalonia has no MessageBox: this is the small replacement the ported
    /// windows use for "show a message and an OK button". Built in code so
    /// that it needs no XAML and works before any window exists (the crash
    /// report in App uses it); the app's Window style gives it the theme.
    /// </summary>
    internal static class MessageDialog
    {
        /// <summary>
        /// Shows <paramref name="text"/> and completes when the dialog closes.
        /// Modal over <paramref name="owner"/> when there is one, a free window
        /// otherwise. Must be called on the UI thread.
        /// </summary>
        public static Task Show(Window owner, string text, string title)
        {
            var completion = new TaskCompletionSource<bool>();

            var ok = new Button
            {
                Content = "OK",
                HorizontalAlignment = HorizontalAlignment.Right,
                IsDefault = true
            };

            var window = new Window
            {
                Title = title,
                CanResize = false,
                ShowInTaskbar = owner == null,
                SizeToContent = SizeToContent.WidthAndHeight,
                MaxWidth = 560,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 },
                        ok
                    }
                }
            };

            ok.Click += (s, e) => window.Close();
            window.Closed += (s, e) => completion.TrySetResult(true);

            if (owner != null)
                _ = window.ShowDialog(owner);
            else
                window.Show();

            return completion.Task;
        }
    }
}
