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
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StopWatch
{
    /// <summary>
    /// Locates, via UI Automation, the taskbar elements that bound its free
    /// space: the widgets/date-time button and Start button (Win11 centered
    /// taskbars) and the rightmost open-app button (left-aligned taskbars).
    /// Values come back in physical screen pixels.
    ///
    /// Talks to the COM UI Automation API (<c>IUIAutomation</c>) through the
    /// minimal interop declared at the bottom of this file. The managed
    /// <c>System.Windows.Automation</c> this used to call belongs to WPF,
    /// which the application no longer references.
    /// </summary>
    [SupportedOSPlatform("windows")]
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

            IUIAutomation automation = null;
            IUIAutomationElement root = null;

            try
            {
                automation = (IUIAutomation)new CUIAutomation();

                root = automation.ElementFromHandle(tray);
                if (root == null)
                    return new Result { Ok = false };

                IUIAutomationElement widgets = FindByAutomationId(automation, root, "WidgetsButton");
                if (widgets != null)
                {
                    UiaRect rect = widgets.CurrentBoundingRectangle;
                    if (!IsEmpty(rect))
                        widgetsRight = rect.Right;
                    Marshal.ReleaseComObject(widgets);
                }

                IUIAutomationElement start = FindByAutomationId(automation, root, "StartButton");
                if (start != null)
                {
                    UiaRect rect = start.CurrentBoundingRectangle;
                    if (!IsEmpty(rect))
                        startLeft = rect.Left;
                    Marshal.ReleaseComObject(start);
                }

                // The rightmost edge of the open-app button row, so a
                // left-aligned/free-space-on-the-right widget never overlaps
                // an open app's button.
                IUIAutomationCondition isButton = automation.CreatePropertyCondition(UIA_ControlTypePropertyId, UIA_ButtonControlTypeId);
                IUIAutomationElementArray buttons = root.FindAll(TreeScope_Descendants, isButton);
                int count = buttons == null ? 0 : buttons.Length;

                for (int i = 0; i < count; i++)
                {
                    // One button can die mid-enumeration (an app opening or
                    // closing right then) - without a per-button try/catch,
                    // that would discard the whole read, including anchors
                    // already found above.
                    try
                    {
                        IUIAutomationElement button = buttons.GetElement(i);

                        string className = button.CurrentClassName ?? "";
                        UiaRect rect = button.CurrentBoundingRectangle;
                        Marshal.ReleaseComObject(button);

                        if (!className.StartsWith("Taskbar.TaskListButton", StringComparison.Ordinal))
                            continue;

                        if (IsEmpty(rect))
                            continue;

                        if (!taskButtonsRight.HasValue || rect.Right > taskButtonsRight.Value)
                            taskButtonsRight = rect.Right;
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
            finally
            {
                if (root != null)
                    Marshal.ReleaseComObject(root);
                if (automation != null)
                    Marshal.ReleaseComObject(automation);
            }

            return new Result
            {
                Ok = true,
                WidgetsRightPx = widgetsRight,
                StartLeftPx = startLeft,
                TaskButtonsRightPx = taskButtonsRight
            };
        }


        /// <summary>The first descendant with this AutomationId, or null if there is none.</summary>
        private static IUIAutomationElement FindByAutomationId(IUIAutomation automation, IUIAutomationElement root, string automationId)
        {
            IUIAutomationCondition condition = automation.CreatePropertyCondition(UIA_AutomationIdPropertyId, automationId);
            return root.FindFirst(TreeScope_Descendants, condition);
        }


        /// <summary>An element with no on-screen area has no usable edge (the managed API reported these as an empty rectangle).</summary>
        private static bool IsEmpty(UiaRect rect)
        {
            return rect.Right <= rect.Left || rect.Bottom <= rect.Top;
        }


        #region UI Automation interop
        // Only what Get() calls. A COM interface is called through its vtable,
        // so every method before the last one used has to be declared, in the
        // order of the type library (UIAutomationClient.idl): the members that
        // are never called are placeholders that keep the slots in line.
        // IUIAutomationCondition is only ever handed back to the API.

        private const int UIA_ControlTypePropertyId = 30003;
        private const int UIA_AutomationIdPropertyId = 30011;
        private const int UIA_ButtonControlTypeId = 50000;
        private const int TreeScope_Descendants = 4;

        [StructLayout(LayoutKind.Sequential)]
        private struct UiaRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [ComImport]
        [Guid("ff48dba4-60ef-4201-aa87-54103eef594e")]
        [ClassInterface(ClassInterfaceType.None)]
        private class CUIAutomation
        {
        }

        [ComImport]
        [Guid("352ffba8-0973-437c-a61f-f64cafd81df9")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationCondition
        {
        }

        [ComImport]
        [Guid("14314595-b4bc-4055-95f2-58f2e42c9855")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationElementArray
        {
            int Length { get; }

            IUIAutomationElement GetElement(int index);
        }

        [ComImport]
        [Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomation
        {
            void CompareElements();
            void CompareRuntimeIds();
            void GetRootElement();

            IUIAutomationElement ElementFromHandle(IntPtr hwnd);

            void ElementFromPoint();
            void GetFocusedElement();
            void GetRootElementBuildCache();
            void ElementFromHandleBuildCache();
            void ElementFromPointBuildCache();
            void GetFocusedElementBuildCache();
            void CreateTreeWalker();
            void ControlViewWalker();
            void ContentViewWalker();
            void RawViewWalker();
            void RawViewCondition();
            void ControlViewCondition();
            void ContentViewCondition();
            void CreateCacheRequest();
            void CreateTrueCondition();
            void CreateFalseCondition();

            IUIAutomationCondition CreatePropertyCondition(int propertyId, [MarshalAs(UnmanagedType.Struct)] object value);
        }

        [ComImport]
        [Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationElement
        {
            void SetFocus();
            void GetRuntimeId();

            IUIAutomationElement FindFirst(int scope, IUIAutomationCondition condition);

            IUIAutomationElementArray FindAll(int scope, IUIAutomationCondition condition);

            void FindFirstBuildCache();
            void FindAllBuildCache();
            void BuildUpdatedCache();
            void GetCurrentPropertyValue();
            void GetCurrentPropertyValueEx();
            void GetCachedPropertyValue();
            void GetCachedPropertyValueEx();
            void GetCurrentPatternAs();
            void GetCachedPatternAs();
            void GetCurrentPattern();
            void GetCachedPattern();
            void GetCachedParent();
            void GetCachedChildren();
            void CurrentProcessId();
            void CurrentControlType();
            void CurrentLocalizedControlType();
            void CurrentName();
            void CurrentAcceleratorKey();
            void CurrentAccessKey();
            void CurrentHasKeyboardFocus();
            void CurrentIsKeyboardFocusable();
            void CurrentIsEnabled();
            void CurrentAutomationId();

            string CurrentClassName { [return: MarshalAs(UnmanagedType.BStr)] get; }

            void CurrentHelpText();
            void CurrentCulture();
            void CurrentIsControlElement();
            void CurrentIsContentElement();
            void CurrentIsPassword();
            void CurrentNativeWindowHandle();
            void CurrentItemType();
            void CurrentIsOffscreen();
            void CurrentOrientation();
            void CurrentFrameworkId();
            void CurrentIsRequiredForForm();
            void CurrentItemStatus();

            UiaRect CurrentBoundingRectangle { get; }
        }
        #endregion
    }
}
