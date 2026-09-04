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

using System.Windows.Forms;

namespace StopWatch
{
    internal static class ModalDialog
    {
        /// <summary>
        /// Shows a modal dialog that is actually visible above its owner.
        ///
        /// WinForms does not give an owned dialog the topmost style, so with
        /// "always on top" enabled the main window draws over its own modal
        /// dialogs: the dialog has focus and takes keystrokes, but the user
        /// cannot see it. Matching the owner's topmost state fixes that without
        /// the window flicker of turning the main window's topmost off and on
        /// around every dialog.
        /// </summary>
        public static DialogResult ShowOver(Form dialog, IWin32Window owner)
        {
            Form ownerForm = FindOwnerForm(owner);

            if (ownerForm != null && ownerForm.TopMost)
                dialog.TopMost = true;

            return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        }


        /// <summary>
        /// The top-level form behind an owner that may be a control - call
        /// sites pass the IssueControl they belong to, not the window.
        /// </summary>
        private static Form FindOwnerForm(IWin32Window owner)
        {
            Form form = owner as Form;
            if (form != null)
                return form;

            Control control = owner as Control;
            if (control != null)
                return control.FindForm();

            return Form.ActiveForm;
        }
    }
}
