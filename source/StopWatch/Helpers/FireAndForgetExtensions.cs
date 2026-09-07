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
using System.Threading.Tasks;

namespace StopWatch
{
    /// <summary>
    /// Marks an async call as deliberately not awaited by its caller, while
    /// still making sure a failure inside it is reported.
    ///
    /// An `async Task` method invoked as `_ = SomeAsync();` looks the same as
    /// one that is properly fire-and-forget, but it is not: any exception the
    /// method raises - before or after its first await - is captured into the
    /// Task that nobody is holding a reference to, and is never observed.
    /// It does not reach DispatcherUnhandledException or
    /// AppDomain.UnhandledException; it simply vanishes, with no log entry and
    /// no dialog, while the UI is left in whatever half-finished state the
    /// call left it in.
    ///
    /// `FireAndForget()` is the one place that gap is closed: it awaits the
    /// task itself and routes anything it throws through
    /// <see cref="App.ReportException"/>, the same path a genuinely unhandled
    /// exception takes. Using `async void` here is deliberate and safe - this
    /// method is the terminal observer of the task, not one more link that
    /// could itself be fire-and-forgotten.
    /// </summary>
    internal static class FireAndForgetExtensions
    {
        public static async void FireAndForget(this Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                App.ReportException("Unobserved exception from a fire-and-forget call", ex);
            }
        }
    }
}
