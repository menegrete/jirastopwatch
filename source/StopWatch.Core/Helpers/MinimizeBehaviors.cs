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


using System.Collections.Generic;

namespace StopWatch
{
    /// <summary>
    /// Which minimize behaviors a platform offers, and what a stored value
    /// does there. The platform is a parameter so both sides can be tested on
    /// any machine; callers pass <c>OperatingSystem.IsWindows()</c>.
    /// See the minimize-behavior and taskbar-widget-view specs.
    /// </summary>
    public static class MinimizeBehaviors
    {
        /// <summary>
        /// What minimizing really does. The taskbar widget exists only on
        /// Windows; a setting inherited from there (the settings file can
        /// roam) behaves as the mini view anywhere else.
        /// </summary>
        public static MinimizeBehavior Effective(MinimizeBehavior stored, bool isWindows)
        {
            if (stored == MinimizeBehavior.TaskbarWidget && !isWindows)
                return MinimizeBehavior.MiniView;

            return stored;
        }


        /// <summary>The options the Settings window offers, in display order.</summary>
        public static IReadOnlyList<MinimizeBehavior> Available(bool isWindows)
        {
            if (isWindows)
                return new[] { MinimizeBehavior.MiniView, MinimizeBehavior.Tray, MinimizeBehavior.TaskbarWidget };

            return new[] { MinimizeBehavior.MiniView, MinimizeBehavior.Tray };
        }
    }
}
