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

using Avalonia.Controls;
using Avalonia.Input;

namespace StopWatch
{
    /// <summary>
    /// A main-window shortcut: a name, a text and its gesture. The gesture is
    /// declared here once; <see cref="Bind"/> attaches a handler to a window,
    /// which is what WPF's CommandBinding did.
    /// </summary>
    internal sealed class ShortcutCommand
    {
        public ShortcutCommand(string text, string name, Key key, KeyModifiers modifiers)
        {
            Text = text;
            Name = name;
            Gesture = new KeyGesture(key, modifiers);
        }


        public string Text { get; private set; }

        public string Name { get; private set; }

        public KeyGesture Gesture { get; private set; }


        /// <summary>
        /// Wires the shortcut into <paramref name="host"/>: the gesture runs
        /// <paramref name="execute"/> while <paramref name="canExecute"/> (if
        /// any) allows it. Returns the command, to give to a button's Command
        /// and to call RaiseCanExecuteChanged on.
        ///
        /// Key bindings are processed after the focused control has had its
        /// chance, so a TextBox still keeps Ctrl+C/Ctrl+V for its own text.
        /// </summary>
        public RelayCommand Bind(Control host, System.Action execute, System.Func<bool> canExecute = null)
        {
            var command = new RelayCommand(execute, canExecute);
            host.KeyBindings.Add(new KeyBinding { Gesture = Gesture, Command = command });
            return command;
        }
    }


    /// <summary>
    /// The main window's keyboard shortcuts, one entry per shortcut with its
    /// gesture attached.
    ///
    /// These started as the thirteen shortcuts MainForm.ProcessCmdKey handled
    /// as a chain of if statements; each one here carries the same gesture and
    /// drives the same action as the entry it came from. Alt+Down (open the
    /// key completion list) was later removed along with the completion list
    /// itself. MoveUp/MoveDown were added later still, for reordering rows.
    /// </summary>
    internal static class StopWatchCommands
    {
        /// <summary>Ctrl+Up - select the row above.</summary>
        public static readonly ShortcutCommand SelectPrevious =
            new ShortcutCommand("Select previous issue", "SelectPrevious", Key.Up, KeyModifiers.Control);

        /// <summary>Ctrl+Down - select the row below.</summary>
        public static readonly ShortcutCommand SelectNext =
            new ShortcutCommand("Select next issue", "SelectNext", Key.Down, KeyModifiers.Control);

        /// <summary>Ctrl+Shift+Up - move the selected row up one position.</summary>
        public static readonly ShortcutCommand MoveUp =
            new ShortcutCommand("Move issue up", "MoveUp", Key.Up, KeyModifiers.Control | KeyModifiers.Shift);

        /// <summary>Ctrl+Shift+Down - move the selected row down one position.</summary>
        public static readonly ShortcutCommand MoveDown =
            new ShortcutCommand("Move issue down", "MoveDown", Key.Down, KeyModifiers.Control | KeyModifiers.Shift);

        /// <summary>Ctrl+P - start or pause the selected row's timer.</summary>
        public static readonly ShortcutCommand TogglePlay =
            new ShortcutCommand("Start/stop timer", "TogglePlay", Key.P, KeyModifiers.Control);

        /// <summary>Ctrl+L - post the selected row's worklog.</summary>
        public static readonly ShortcutCommand PostWorklog =
            new ShortcutCommand("Post worklog", "PostWorklog", Key.L, KeyModifiers.Control);

        /// <summary>Ctrl+E - edit the selected row's elapsed time.</summary>
        public static readonly ShortcutCommand EditTime =
            new ShortcutCommand("Edit time", "EditTime", Key.E, KeyModifiers.Control);

        /// <summary>Ctrl+R - reset the selected row's timer.</summary>
        public static readonly ShortcutCommand ResetTimer =
            new ShortcutCommand("Reset timer", "ResetTimer", Key.R, KeyModifiers.Control);

        /// <summary>Ctrl+Delete - remove the selected row.</summary>
        public static readonly ShortcutCommand RemoveIssue =
            new ShortcutCommand("Remove issue row", "RemoveIssue", Key.Delete, KeyModifiers.Control);

        /// <summary>Ctrl+I - put the focus in the selected row's key field.</summary>
        public static readonly ShortcutCommand FocusKey =
            new ShortcutCommand("Focus issue key", "FocusKey", Key.I, KeyModifiers.Control);

        /// <summary>Ctrl+N - add a row.</summary>
        public static readonly ShortcutCommand AddIssue =
            new ShortcutCommand("Add issue row", "AddIssue", Key.N, KeyModifiers.Control);

        /// <summary>Ctrl+C - copy the selected row's key.</summary>
        public static readonly ShortcutCommand CopyKey =
            new ShortcutCommand("Copy issue key", "CopyKey", Key.C, KeyModifiers.Control);

        /// <summary>Ctrl+V - paste a key into the selected row.</summary>
        public static readonly ShortcutCommand PasteKey =
            new ShortcutCommand("Paste issue key", "PasteKey", Key.V, KeyModifiers.Control);

        /// <summary>Ctrl+O - open the selected issue in a browser.</summary>
        public static readonly ShortcutCommand OpenInBrowser =
            new ShortcutCommand("Open issue in browser", "OpenInBrowser", Key.O, KeyModifiers.Control);
    }
}
