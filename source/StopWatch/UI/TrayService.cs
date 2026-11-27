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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace StopWatch
{
    /// <summary>One line of the tray menu: an action, a bold-ish header, or a separator.</summary>
    internal sealed class TrayMenuEntry
    {
        private TrayMenuEntry() { }

        public string Title { get; private set; }

        /// <summary>Null for headers and separators.</summary>
        public Action Action { get; private set; }

        public bool IsSeparator { get; private set; }

        /// <summary>A non-clickable group title (the WinForms menu used a bold label).</summary>
        public bool IsHeader { get; private set; }

        public static TrayMenuEntry Item(string title, Action action)
        {
            return new TrayMenuEntry { Title = title, Action = action };
        }

        public static TrayMenuEntry Header(string title)
        {
            return new TrayMenuEntry { Title = title, IsHeader = true };
        }

        public static TrayMenuEntry Separator()
        {
            return new TrayMenuEntry { IsSeparator = true };
        }
    }


    /// <summary>
    /// The notification-area icon, replacing WinForms' NotifyIcon. One instance
    /// per application, created by whoever decides when the icon is needed
    /// (the main window, for the Tray minimize behavior).
    ///
    /// Wraps Avalonia's <see cref="TrayIcon"/>, which is registered on the
    /// application so that it lives as long as this service does. The icon is
    /// the application icon, static: it is not drawn from theme colours.
    ///
    /// Behavior carried over from the WinForms tray: a left click raises
    /// <see cref="Clicked"/> (the owner restores its window); a right click
    /// opens the menu if there is one, and does nothing if the menu is empty -
    /// so with no plugin commands the right click behaves as before plugins
    /// existed.
    ///
    /// Must be used on the UI thread. Shown as soon as it is created; dispose
    /// it to remove the icon.
    /// </summary>
    internal sealed class TrayService : IDisposable
    {
        public const string DefaultToolTip = "Jira StopWatch";

        private const string DefaultIconUri = "avares://StopWatch/stopwatchicon.ico";

        private readonly TrayIcon trayIcon;
        private bool disposed;


        public TrayService(string toolTip = DefaultToolTip)
        {
            // Visible from creation, as in the validated spike: starting hidden
            // and showing it later did not make the icon appear.
            trayIcon = new TrayIcon
            {
                ToolTipText = toolTip,
                Icon = LoadDefaultIcon()
            };
            trayIcon.Clicked += (s, e) => Clicked?.Invoke(this, EventArgs.Empty);

            // An icon that is not registered on the application is never
            // created by the platform.
            TrayIcon.SetIcons(Application.Current, new TrayIcons { trayIcon });
        }


        /// <summary>Raised on a left click of the icon.</summary>
        public event EventHandler Clicked;


        /// <summary>Shows or hides the icon (NotifyIcon.Visible).</summary>
        public bool Visible
        {
            get { return trayIcon.IsVisible; }
            set { trayIcon.IsVisible = value; }
        }


        public string ToolTip
        {
            get { return trayIcon.ToolTipText; }
            set { trayIcon.ToolTipText = value; }
        }


        public void SetIcon(WindowIcon icon)
        {
            trayIcon.Icon = icon;
        }


        /// <summary>
        /// Replaces the menu. Safe to call whether the icon is visible or not
        /// (plugins arrive after the window is built). An empty list removes
        /// the menu altogether.
        /// </summary>
        public void SetMenu(IEnumerable<TrayMenuEntry> entries)
        {
            var menu = new NativeMenu();

            foreach (TrayMenuEntry entry in entries ?? new TrayMenuEntry[0])
            {
                if (entry.IsSeparator)
                {
                    menu.Items.Add(new NativeMenuItemSeparator());
                    continue;
                }

                var item = new NativeMenuItem(entry.Title) { IsEnabled = !entry.IsHeader };

                if (entry.Action != null)
                {
                    Action action = entry.Action;
                    item.Click += (s, e) => action();
                }

                menu.Items.Add(item);
            }

            trayIcon.Menu = menu.Items.Count == 0 ? null : menu;
        }


        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            trayIcon.IsVisible = false;
            trayIcon.Dispose();
        }


        /// <summary>The application icon, from the avares:// resource.</summary>
        private static WindowIcon LoadDefaultIcon()
        {
            using (var stream = AssetLoader.Open(new Uri(DefaultIconUri)))
                return new WindowIcon(stream);
        }
    }
}
