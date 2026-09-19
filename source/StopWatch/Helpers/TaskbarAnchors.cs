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
 *
 * Adapted from mechanicwb2-hub's now-playing-taskbar-widget (MIT License,
 * Copyright (c) 2026 MechanicWB),
 * https://github.com/mechanicwb2-hub/now-playing-taskbar-widget
 */

using System;
using System.Windows.Automation;

namespace StopWatch
{
    /// <summary>
    /// Locates, via UI Automation, the taskbar elements that bound its free
    /// space: the widgets/date-time button and Start button (Win11 centered
    /// taskbars) and the rightmost open-app button (left-aligned taskbars).
    /// Values come back in physical screen pixels.
    /// </summary>
    internal static class TaskbarAnchors
    {
        /// <summary>
        /// One read of the taskbar's anchor points.
        ///
        /// <see cref="Ok"/> false means the READ itself failed (UI Automation
        /// threw) - the caller should keep whatever anchors it already had,
        /// not treat this as "the buttons are gone". <see cref="Ok"/> true
        /// with a null point means the element genuinely does not exist right
        /// now (e.g. widgets disabled). Conflating the two would turn a
        /// transient UI Automation hiccup into "the button disappeared",
        /// landing the widget on top of the clock.
        /// </summary>
        public struct Result
        {
            public bool Ok;
            public int? WidgetsRightPx;
            public int? StartLeftPx;
            public int? TaskButtonsRightPx;
        }


        public static Result Get(IntPtr tray)
        {
            int? widgetsRight = null;
            int? startLeft = null;
            int? taskButtonsRight = null;

            try
            {
                AutomationElement root = AutomationElement.FromHandle(tray);
                if (root == null)
                    return new Result { Ok = false };

                AutomationElement widgets = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "WidgetsButton"));
                if (widgets != null)
                {
                    System.Windows.Rect rect = widgets.Current.BoundingRectangle;
                    if (!rect.IsEmpty)
                        widgetsRight = (int)rect.Right;
                }

                AutomationElement start = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "StartButton"));
                if (start != null)
                {
                    System.Windows.Rect rect = start.Current.BoundingRectangle;
                    if (!rect.IsEmpty)
                        startLeft = (int)rect.Left;
                }

                // The rightmost edge of the open-app button row, so a
                // left-aligned/free-space-on-the-right widget never overlaps
                // an open app's button.
                AutomationElementCollection buttons = root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                foreach (AutomationElement button in buttons)
                {
                    // One button can die mid-enumeration (an app opening or
                    // closing right then) - without a per-button try/catch,
                    // that would discard the whole read, including anchors
                    // already found above.
                    try
                    {
                        string className = button.Current.ClassName ?? "";
                        if (!className.StartsWith("Taskbar.TaskListButton", StringComparison.Ordinal))
                            continue;

                        System.Windows.Rect rect = button.Current.BoundingRectangle;
                        if (rect.IsEmpty)
                            continue;

                        if (!taskButtonsRight.HasValue || rect.Right > taskButtonsRight.Value)
                            taskButtonsRight = (int)rect.Right;
                    }
                    catch
                    {
                        // Keep whatever this loop already found; move on to the next button.
                    }
                }
            }
            catch
            {
                return new Result { Ok = false };
            }

            return new Result
            {
                Ok = true,
                WidgetsRightPx = widgetsRight,
                StartLeftPx = startLeft,
                TaskButtonsRightPx = taskButtonsRight
            };
        }
    }
}
