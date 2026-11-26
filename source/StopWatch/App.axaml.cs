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

using Microsoft.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using StopWatch.Plugins;

namespace StopWatch
{
    /// <summary>
    /// The application: startup, shutdown and the failure handlers. What used
    /// to be Program.Main lives here and in Program.
    ///
    /// Startup is OnFrameworkInitializationCompleted (WPF's OnStartup): the
    /// unhandled-exception handler, the session-switch subscription, settings,
    /// theme, composition, plugins, then the main window. The single-instance
    /// mutex is claimed earlier, in Program.Main. The message that a second
    /// instance broadcasts is still WM_SHOWME; it is heard by a hook on the
    /// main window's handle (Windows only). See the openspec design, D8.
    /// </summary>
    internal partial class App : Application
    {
        #region public methods
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // Both palettes, once. Which one is shown is the theme variant,
            // set from settings below.
            ThemeBrushes.Install(this);
        }


        public override void OnFrameworkInitializationCompleted()
        {
            var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

            if (desktop != null)
            {
                // As before: the app keeps running while the main window is
                // hidden in the tray or in the mini view, and ends when the
                // last window is closed. Hide() is not Close().
                desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
                desktop.Exit += (s, e) => Shutdown();

                Dispatcher.UIThread.UnhandledException += Dispatcher_UnhandledException;

                if (OperatingSystem.IsWindows())
                    SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

                settings = Settings.Instance;
                settings.Load();

                Theme.Current = Theme.ForMode(settings.Theme);
                ThemeBrushes.ApplyToApplication();

                Logging.Logger.Instance.LogfilePath = LogPath;
                Logging.Logger.Instance.Enabled = settings.LoggingEnabled;

                var composition = new AppComposition(settings);

                window = new MainWindow(settings, composition);
                desktop.MainWindow = window;

                // Loaded before the window is shown so that the tray menu and the
                // Plugins entry are complete from the start. Never throws.
                window.SetPlugins(PluginManager.Load(PluginManager.DefaultPluginsFolders, PluginManager.DefaultDataFolder, window, composition));
            }

            base.OnFrameworkInitializationCompleted();
        }
        #endregion


        #region internal methods
        /// <summary>
        /// Becomes the one running instance, or tells the instance that already
        /// is to come to the front and returns false. Called from Program.Main.
        /// </summary>
        internal static bool ClaimSingleInstance()
        {
            mutex = new Mutex(true, MutexName);

            if (mutex.WaitOne(TimeSpan.Zero, true))
                return true;

            mutex.Dispose();
            mutex = null;

            if (OperatingSystem.IsWindows())
            {
                NativeMethods.PostMessage(
                    (IntPtr)NativeMethods.HWND_BROADCAST,
                    NativeMethods.WM_SHOWME,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }

            return false;
        }


        /// <summary>
        /// The one place any caught-but-otherwise-unhandled exception is
        /// reported from, whatever caught it: the dispatcher, the app domain,
        /// or <see cref="FireAndForgetExtensions.FireAndForget"/> observing a
        /// task nobody else was going to. Safe to call from any thread.
        /// </summary>
        internal static void ReportException(string context, Exception exception)
        {
            WriteLog(context);
            WriteException(exception);

            if (Dispatcher.UIThread.CheckAccess())
                DisplayErrorHandled();
            else
                Dispatcher.UIThread.Post(DisplayErrorHandled);
        }
        #endregion


        #region private methods
        private void Shutdown()
        {
            if (OperatingSystem.IsWindows())
                SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;

            if (mutex != null)
            {
                // Releasing this is what lets the next instance become the
                // primary one.
                mutex.ReleaseMutex();
                mutex.Dispose();
                mutex = null;
            }
        }


        /// <summary>
        /// Reports a crash, guarding only against showing a second dialog while
        /// the first is still open.
        ///
        /// The guard is not paranoia: a failure raised from a layout pass is
        /// raised again while the dialog is up - without a guard the report
        /// becomes an endless stack of dialogs. The guard is released once
        /// this dialog closes, so a later, unrelated crash still gets its own
        /// report instead of being silently swallowed for the rest of the
        /// process's life.
        /// </summary>
        private static void DisplayErrorHandled()
        {
            if (reportingError)
                return;

            reportingError = true;
            try
            {
                Task closed = MessageDialog.Show(
                    null,
                    string.Format("Jira StopWatch encountered an unhandled error. A logfile has been created. If the error continues to occur, please send the logfile content to carsten@sarum.dk.\n\nSee more details in the logfile:\n\n{0}", LogPath),
                    "Unhandled error occurred");

                closed.ContinueWith(t => reportingError = false, TaskScheduler.Default);
            }
            catch (Exception)
            {
                // Reporting a crash must not itself crash.
                reportingError = false;
            }
        }


        /// <summary>
        /// Writes an exception and every one it wraps. The outermost message of
        /// a XAML or reflection failure says almost nothing; the cause is
        /// always further in.
        /// </summary>
        private static void WriteException(Exception exception)
        {
            while (exception != null)
            {
                WriteLog(string.Format("{0}: {1}", exception.GetType().Name, exception.Message));
                WriteLog(exception.StackTrace);

                exception = exception.InnerException;
            }
        }


        private static void WriteLog(string message)
        {
            try
            {
                File.AppendAllText(LogPath, string.Format("{0}: {1}\n", DateTime.Now, message));
            }
            catch (IOException)
            {
                // Reporting a crash must not itself crash.
            }
        }


        /// <summary>
        /// The log file, in the same place as before: AppInfo.UserDataPath
        /// reproduces System.Windows.Forms.Application.UserAppDataPath, whose
        /// rules for company, product and version would otherwise risk moving
        /// an existing user's log somewhere they cannot find it.
        /// </summary>
        private static string LogPath
        {
            get
            {
                return Path.Combine(AppInfo.UserDataPath, "jirastopwatch.log");
            }
        }
        #endregion


        #region private eventhandlers
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (window == null)
                return;

            // SessionSwitch arrives on a thread of its own, so the window is
            // reached through the UI dispatcher rather than directly.
            if (e.Reason == SessionSwitchReason.SessionLock)
                Dispatcher.UIThread.Invoke(window.HandleSessionLock);
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
                Dispatcher.UIThread.Invoke(window.HandleSessionUnlock);
        }


        private void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ReportException("Unhandled Dispatcher Exception", e.Exception);

            // Handled so that one failed interaction does not take the whole
            // application down, which is how the WinForms thread-exception
            // handler behaved.
            e.Handled = true;
        }
        #endregion


        #region private members
        private const string MutexName = "{D5597999-20FE-430F-8E5D-8893EBED2599}";

        private static bool reportingError;

        private static Mutex mutex;
        private Settings settings;
        private MainWindow window;
        #endregion
    }
}
