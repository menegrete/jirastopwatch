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
using System.Windows;
using System.Windows.Threading;

namespace StopWatch
{
    /// <summary>
    /// The application's entry point, which used to be Program.Main.
    ///
    /// Everything that lived there is here: the single-instance mutex, the
    /// unhandled-exception handlers and the session-switch subscription. The
    /// message that a second instance broadcasts is still WM_SHOWME; what
    /// changed is where it is heard, which is now a hook on the main window's
    /// handle rather than a form's WndProc. See the openspec design, D8.
    /// </summary>
    internal partial class App : Application
    {
        #region protected methods
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (!ClaimSingleInstance())
            {
                // Somebody is already running. They have been asked to come to
                // the front; this process leaves without ever showing a window.
                Shutdown();
                return;
            }

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

            settings = Settings.Instance;
            settings.Load();

            Theme.Current = Theme.ForMode(settings.Theme);
            ThemeBrushes.ApplyToApplication();

            window = new MainWindow(settings);
            MainWindow = window;
            window.Show();
        }


        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;

            if (mutex != null)
            {
                // Releasing this is what lets the next instance become the
                // primary one.
                mutex.ReleaseMutex();
                mutex.Dispose();
                mutex = null;
            }

            base.OnExit(e);
        }
        #endregion


        #region private methods
        /// <summary>
        /// Becomes the one running instance, or tells the instance that already
        /// is to come to the front and returns false.
        /// </summary>
        private bool ClaimSingleInstance()
        {
            mutex = new Mutex(true, MutexName);

            if (mutex.WaitOne(TimeSpan.Zero, true))
                return true;

            mutex.Dispose();
            mutex = null;

            NativeMethods.PostMessage(
                (IntPtr)NativeMethods.HWND_BROADCAST,
                NativeMethods.WM_SHOWME,
                IntPtr.Zero,
                IntPtr.Zero);

            return false;
        }
        #endregion


        #region private eventhandlers
        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (window == null)
                return;

            // SessionSwitch arrives on a thread of its own, so the window is
            // reached through its dispatcher rather than directly.
            if (e.Reason == SessionSwitchReason.SessionLock)
                window.Dispatcher.Invoke(new Action(window.HandleSessionLock));
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
                window.Dispatcher.Invoke(new Action(window.HandleSessionUnlock));
        }


        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteLog("Unhandled Dispatcher Exception");
            WriteException(e.Exception);

            DisplayErrorHandled();

            // Handled so that one failed interaction does not take the whole
            // application down, which is how the WinForms thread-exception
            // handler behaved.
            e.Handled = true;
        }


        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteLog("Unhandled UI Exception");

            WriteException(e.ExceptionObject as Exception);

            DisplayErrorHandled();
        }
        #endregion


        #region private methods
        /// <summary>
        /// Reports a crash, once.
        ///
        /// The guard is not paranoia: a failure raised from a layout pass is
        /// raised again by the message loop this dialog runs, and without it
        /// the report becomes an endless stack of dialogs that ends in a stack
        /// overflow.
        /// </summary>
        private static void DisplayErrorHandled()
        {
            if (reportingError)
                return;

            reportingError = true;

            MessageBox.Show(
                string.Format("Jira StopWatch encountered an unhandled error. A logfile has been created. If the error continues to occur, please send the logfile content to carsten@sarum.dk.\n\nSee more details in the logfile:\n\n{0}", LogPath),
                "Unhandled error occurred");
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
        /// The log file, in the same place as before.
        ///
        /// Asked of WinForms rather than composed by hand: its
        /// UserAppDataPath has its own rules for company, product and version,
        /// and re-deriving them would risk moving an existing user's log
        /// somewhere they cannot find it. WinForms is referenced deliberately
        /// and permanently anyway.
        /// </summary>
        private static string LogPath
        {
            get
            {
                return Path.Combine(
                    System.Windows.Forms.Application.UserAppDataPath,
                    "jirastopwatch.log");
            }
        }
        #endregion


        #region private members
        private const string MutexName = "{D5597999-20FE-430F-8E5D-8893EBED2599}";

        private static bool reportingError;

        private Mutex mutex;
        private Settings settings;
        private MainWindow window;
        #endregion
    }
}
