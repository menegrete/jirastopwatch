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

using System.Windows.Input;

namespace StopWatch
{
    /// <summary>
    /// The main window's keyboard shortcuts, one command per shortcut with its
    /// gesture attached.
    ///
    /// These started as the thirteen shortcuts MainForm.ProcessCmdKey handled
    /// as a chain of if statements, listed one by one in the change's
    /// inventory.md before that method was deleted; each one here carries the
    /// same gesture and drives the same action as the entry it came from.
    /// Alt+Down (open the key completion list) was later removed along with
    /// the completion list itself, once the Filter feature that fed it was
    /// gone.
    /// </summary>
    internal static class StopWatchCommands
    {
        /// <summary>Ctrl+Up - select the row above.</summary>
        public static readonly RoutedUICommand SelectPrevious =
            Make("Select previous issue", "SelectPrevious", Key.Up, ModifierKeys.Control);

        /// <summary>Ctrl+Down - select the row below.</summary>
        public static readonly RoutedUICommand SelectNext =
            Make("Select next issue", "SelectNext", Key.Down, ModifierKeys.Control);

        /// <summary>Ctrl+P - start or pause the selected row's timer.</summary>
        public static readonly RoutedUICommand TogglePlay =
            Make("Start/stop timer", "TogglePlay", Key.P, ModifierKeys.Control);

        /// <summary>Ctrl+L - post the selected row's worklog.</summary>
        public static readonly RoutedUICommand PostWorklog =
            Make("Post worklog", "PostWorklog", Key.L, ModifierKeys.Control);

        /// <summary>Ctrl+E - edit the selected row's elapsed time.</summary>
        public static readonly RoutedUICommand EditTime =
            Make("Edit time", "EditTime", Key.E, ModifierKeys.Control);

        /// <summary>Ctrl+R - reset the selected row's timer.</summary>
        public static readonly RoutedUICommand ResetTimer =
            Make("Reset timer", "ResetTimer", Key.R, ModifierKeys.Control);

        /// <summary>Ctrl+Delete - remove the selected row.</summary>
        public static readonly RoutedUICommand RemoveIssue =
            Make("Remove issue row", "RemoveIssue", Key.Delete, ModifierKeys.Control);

        /// <summary>Ctrl+I - put the focus in the selected row's key field.</summary>
        public static readonly RoutedUICommand FocusKey =
            Make("Focus issue key", "FocusKey", Key.I, ModifierKeys.Control);

        /// <summary>Ctrl+N - add a row.</summary>
        public static readonly RoutedUICommand AddIssue =
            Make("Add issue row", "AddIssue", Key.N, ModifierKeys.Control);

        /// <summary>Ctrl+C - copy the selected row's key.</summary>
        public static readonly RoutedUICommand CopyKey =
            Make("Copy issue key", "CopyKey", Key.C, ModifierKeys.Control);

        /// <summary>Ctrl+V - paste a key into the selected row.</summary>
        public static readonly RoutedUICommand PasteKey =
            Make("Paste issue key", "PasteKey", Key.V, ModifierKeys.Control);

        /// <summary>Ctrl+O - open the selected issue in a browser.</summary>
        public static readonly RoutedUICommand OpenInBrowser =
            Make("Open issue in browser", "OpenInBrowser", Key.O, ModifierKeys.Control);


        private static RoutedUICommand Make(string text, string name, Key key, ModifierKeys modifiers)
        {
            return new RoutedUICommand(text, name, typeof(StopWatchCommands),
                new InputGestureCollection { new KeyGesture(key, modifiers) });
        }
    }
}
