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
using System.Drawing;
using System.Windows.Forms;

namespace StopWatch
{
    /// <summary>
    /// Walks a control tree and paints it with the active theme.
    ///
    /// Runs after InitializeComponent, so whatever colours the Visual Studio
    /// designer wrote into the .Designer.cs files are irrelevant at runtime.
    /// Re-applying is safe: every event subscription here detaches before it
    /// attaches.
    /// </summary>
    public static class ThemeApplier
    {
        // Marks a textbox whose system border we removed, so that re-applying a
        // theme does not mistake it for a deliberately borderless one.
        private const string BorderedMarker = "sw-themed-border";

        // Hand-drawn checkbox / radio glyph metrics.
        private const int GlyphSize = 13;
        private const int GlyphTextGap = 4;
        private const int GlyphTextPadding = 4;


        #region public methods
        public static void Apply(Control root)
        {
            Apply(root, Theme.Current);
        }


        public static void Apply(Control root, Theme theme)
        {
            if (root == null)
                return;

            ApplyToControl(root, theme);

            foreach (Control child in root.Controls)
                Apply(child, theme);
        }


        /// <summary>
        /// Themes a tooltip component. Tooltips are not in the control tree, so
        /// they have to be handed over separately.
        /// </summary>
        public static void ApplyToToolTip(ToolTip toolTip, Theme theme)
        {
            if (toolTip == null)
                return;

            toolTip.OwnerDraw = true;
            toolTip.BackColor = theme.Surface;
            toolTip.ForeColor = theme.Text;
            toolTip.Draw -= ToolTip_Draw;
            toolTip.Draw += ToolTip_Draw;
        }
        #endregion


        #region private methods
        private static void ApplyToControl(Control control, Theme theme)
        {
            // Order matters: the specific types come before the generic ones,
            // because several of them derive from one another.
            if (control is Form)
                ApplyToForm((Form)control, theme);
            else if (control is LinkLabel)
                ApplyToLinkLabel((LinkLabel)control, theme);
            else if (control is Label)
                ApplyToLabel((Label)control, theme);
            else if (control is CheckBox)
                ApplyToCheckBox((CheckBox)control, theme);
            else if (control is RadioButton)
                ApplyToRadioButton((RadioButton)control, theme);
            else if (control is Button)
                ApplyToButton((Button)control, theme);
            else if (control is ComboBox)
                ApplyToComboBox((ComboBox)control, theme);
            else if (control is TextBox)
                ApplyToTextBox((TextBox)control, theme);
            else if (control is GroupBox)
                ApplyToGroupBox((GroupBox)control, theme);
            else if (control is DateTimePicker)
                ApplyToDateTimePicker((DateTimePicker)control, theme);
            else if (control is PictureBox)
                control.BackColor = Color.Transparent;
            else if (control is Panel)
                ApplyToPanel((Panel)control, theme);
            else
            {
                control.BackColor = theme.Background;
                control.ForeColor = theme.Text;
            }
        }


        private static void ApplyToForm(Form form, Theme theme)
        {
            form.BackColor = theme.Background;
            form.ForeColor = theme.Text;

            WhenHandleReady(form, Form_ApplyDarkTitleBar);
        }


        private static void ApplyToPanel(Panel panel, Theme theme)
        {
            panel.BackColor = theme.Background;
            panel.ForeColor = theme.Text;

            if (panel.AutoScroll || panel.VerticalScroll.Visible || panel.HorizontalScroll.Visible)
                WhenHandleReady(panel, Control_ApplyDarkScrollBars);
        }


        /// <summary>
        /// Walks up to the nearest ancestor with an opaque background.
        ///
        /// Needed because Graphics.Clear(Color.Transparent) paints black rather
        /// than nothing: any hand-painted control has to resolve transparency to a
        /// real colour first. Labels can stay Transparent because WinForms paints
        /// those against the parent itself.
        /// </summary>
        private static Color EffectiveBackColor(Control control, Theme theme)
        {
            for (Control parent = control.Parent; parent != null; parent = parent.Parent)
                if (parent.BackColor.A != 0)
                    return parent.BackColor;

            return theme.Background;
        }


        /// <summary>
        /// Runs HWND-level work without forcing the handle into existence.
        ///
        /// This matters: the applier runs from form constructors, and touching
        /// Control.Handle there creates the window early, outside the Show path.
        /// A modal dialog created that way never picks up its owner, so it ends up
        /// behind the main window while still blocking input - the app looks hung
        /// and has to be killed.
        /// </summary>
        private static void WhenHandleReady(Control control, EventHandler action)
        {
            if (control.IsHandleCreated)
            {
                action(control, EventArgs.Empty);
                return;
            }

            control.HandleCreated -= action;
            control.HandleCreated += action;
        }


        private static void ApplyToLabel(Label label, Theme theme)
        {
            // A 2px-high Fixed3D label is a horizontal rule, not a label. The
            // system draws Fixed3D with its own colours and ignores ours, so the
            // border has to go and the colour becomes the fill.
            if (label.BorderStyle == BorderStyle.Fixed3D && label.Height <= 3)
            {
                label.BorderStyle = BorderStyle.None;
                label.BackColor = theme.Border;
                return;
            }

            label.BackColor = Color.Transparent;
            label.ForeColor = theme.Text;
        }


        private static void ApplyToLinkLabel(LinkLabel link, Theme theme)
        {
            link.BackColor = Color.Transparent;
            link.ForeColor = theme.Text;
            link.LinkColor = theme.Link;
            link.ActiveLinkColor = theme.Link;
            link.VisitedLinkColor = theme.Link;
            link.DisabledLinkColor = theme.TextMuted;
        }


        private static void ApplyToTextBox(TextBox textBox, Theme theme)
        {
            bool bordered = BorderedMarker.Equals(textBox.Tag);

            // Both Fixed3D and FixedSingle are drawn by the system in its own
            // colours - measured at #646464 regardless of theme - so the border
            // comes off and the parent draws it in the theme's colour instead.
            // A textbox that was already borderless is being used as a label;
            // it keeps no border and sits flush on its parent.
            if (!bordered && textBox.BorderStyle != BorderStyle.None)
            {
                textBox.BorderStyle = BorderStyle.None;
                textBox.Tag = BorderedMarker;
                bordered = true;
            }

            textBox.ForeColor = theme.Text;

            if (bordered)
            {
                textBox.BackColor = textBox.Enabled && !textBox.ReadOnly
                    ? theme.Surface
                    : theme.SurfaceDisabled;

                if (textBox.Parent != null)
                    EnsureBorderPainter(textBox.Parent);
            }
            else
            {
                textBox.BackColor = EffectiveBackColor(textBox, theme);
            }

            if (textBox.Multiline && textBox.ScrollBars != ScrollBars.None)
                WhenHandleReady(textBox, Control_ApplyDarkScrollBars);
        }


        private static void EnsureBorderPainter(Control parent)
        {
            parent.Paint -= Parent_PaintTextBoxBorders;
            parent.Paint += Parent_PaintTextBoxBorders;
        }


        private static void ApplyToButton(Button button, Theme theme)
        {
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.ForeColor = theme.Text;

            bool isIconOnly = button.Image != null && String.IsNullOrEmpty(button.Text);

            // Icon-only buttons sit inside a row and should read as part of it,
            // the way UseVisualStyleBackColor made them look before.
            button.BackColor = isIconOnly && button.Parent != null
                ? button.Parent.BackColor
                : theme.Surface;

            button.FlatAppearance.BorderSize = isIconOnly ? 0 : 1;
            button.FlatAppearance.BorderColor = theme.Border;
            button.FlatAppearance.MouseOverBackColor = theme.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = theme.ButtonPressed;
        }


        // Neither FlatStyle.Flat nor FlatAppearance reaches the glyph: WinForms
        // fills the box interior with SystemColors.Window, which is white. The
        // glyph has to be drawn by hand.
        private static void ApplyToCheckBox(CheckBox checkBox, Theme theme)
        {
            PrepareForGlyphPaint(checkBox, theme);
            checkBox.Paint -= CheckBox_Paint;
            checkBox.Paint += CheckBox_Paint;
        }


        private static void ApplyToRadioButton(RadioButton radioButton, Theme theme)
        {
            PrepareForGlyphPaint(radioButton, theme);
            radioButton.Paint -= RadioButton_Paint;
            radioButton.Paint += RadioButton_Paint;
        }


        private static void PrepareForGlyphPaint(ButtonBase control, Theme theme)
        {
            control.FlatStyle = FlatStyle.Flat;
            control.ForeColor = theme.Text;
            control.BackColor = EffectiveBackColor(control, theme);

            // The designer sized these controls for the native glyph layout, which
            // is slightly tighter than ours. Widen where needed so no caption gets
            // clipped, and never shrink.
            int wanted = GlyphSize + GlyphTextGap
                + TextRenderer.MeasureText(control.Text, control.Font).Width
                + GlyphTextPadding;

            if (control.Width < wanted)
                control.Width = wanted;

            // The hand-drawn glyph carries the hover state, so it needs a repaint
            // when the pointer crosses the control.
            control.MouseEnter -= Glyph_InvalidateOnHover;
            control.MouseEnter += Glyph_InvalidateOnHover;
            control.MouseLeave -= Glyph_InvalidateOnHover;
            control.MouseLeave += Glyph_InvalidateOnHover;
        }


        private static void PaintGlyphControl(ButtonBase control, Graphics graphics, bool checked_, bool round)
        {
            Theme theme = Theme.Current;

            graphics.Clear(EffectiveBackColor(control, theme));

            bool hovered = control.Enabled
                && control.ClientRectangle.Contains(control.PointToClient(Control.MousePosition));

            Rectangle box = new Rectangle(0, (control.Height - GlyphSize) / 2, GlyphSize, GlyphSize);

            Color fill;
            if (!control.Enabled)
                fill = theme.SurfaceDisabled;
            else if (checked_)
                fill = theme.Accent;
            else
                fill = hovered ? theme.ButtonHover : theme.Surface;

            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(control.Enabled ? theme.Border : theme.SurfaceDisabled))
            {
                if (round)
                {
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    graphics.FillEllipse(brush, box);
                    graphics.DrawEllipse(pen, box);
                }
                else
                {
                    graphics.FillRectangle(brush, box);
                    graphics.DrawRectangle(pen, box);
                }
            }

            if (checked_)
            {
                Color mark = control.Enabled ? theme.AccentText : theme.TextMuted;

                if (round)
                {
                    using (SolidBrush brush = new SolidBrush(mark))
                        graphics.FillEllipse(brush, Rectangle.Inflate(box, -4, -4));
                }
                else
                {
                    // A two-stroke tick inside the box.
                    using (Pen pen = new Pen(mark, 2f))
                    {
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        graphics.DrawLines(pen, new[]
                        {
                            new Point(box.Left + 3, box.Top + 6),
                            new Point(box.Left + 5, box.Top + 9),
                            new Point(box.Left + 10, box.Top + 3)
                        });
                    }
                }
            }

            TextRenderer.DrawText(
                graphics,
                control.Text,
                control.Font,
                new Rectangle(GlyphSize + GlyphTextGap, 0,
                              control.Width - GlyphSize - GlyphTextGap, control.Height),
                control.Enabled ? theme.Text : theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                    | (NativeMethods.KeyboardCuesVisible()
                        ? TextFormatFlags.Default
                        : TextFormatFlags.HidePrefix)
            );
        }


        private static void ApplyToComboBox(ComboBox comboBox, Theme theme)
        {
            comboBox.FlatStyle = FlatStyle.System;
            comboBox.BackColor = theme.Surface;
            comboBox.ForeColor = theme.Text;

            // FlatStyle.Flat gets the body but not the drop-down button, which the
            // system keeps drawing light (measured #F0F0F0). FlatStyle.System hands
            // the drawing back to the native control, which is the only thing
            // DarkMode_CFD can reach.
            WhenHandleReady(comboBox, ComboBox_ApplyDarkChrome);

            // Leave an existing owner-draw alone - cbJira draws two columns of
            // its own and knows what it is doing. Everything else gets a plain
            // themed item renderer so the dropdown list is not white.
            if (comboBox.DrawMode == DrawMode.Normal)
            {
                comboBox.DrawMode = DrawMode.OwnerDrawFixed;
                comboBox.DrawItem -= ComboBox_DrawItem;
                comboBox.DrawItem += ComboBox_DrawItem;
            }
        }


        private static void ApplyToGroupBox(GroupBox groupBox, Theme theme)
        {
            groupBox.BackColor = Color.Transparent;
            groupBox.ForeColor = theme.Text;

            // The visual-styled GroupBox border ignores ForeColor, so it gets
            // drawn by hand.
            groupBox.Paint -= GroupBox_Paint;
            groupBox.Paint += GroupBox_Paint;
        }


        private static void ApplyToDateTimePicker(DateTimePicker picker, Theme theme)
        {
            // SysDateTimePick32 ignores BackColor entirely. ShowUpDown is set on
            // both pickers in this app, so there is no calendar popup to theme -
            // only the parts the OS lets us touch.
            picker.CalendarMonthBackground = theme.Surface;
            picker.CalendarForeColor = theme.Text;
            picker.CalendarTitleBackColor = theme.Accent;
            picker.CalendarTitleForeColor = theme.AccentText;
            picker.CalendarTrailingForeColor = theme.TextMuted;
        }
        #endregion


        #region private eventhandlers
        private static void GroupBox_Paint(object sender, PaintEventArgs e)
        {
            GroupBox groupBox = (GroupBox)sender;
            Theme theme = Theme.Current;

            Size textSize = TextRenderer.MeasureText(groupBox.Text, groupBox.Font);
            int textTop = textSize.Height / 2;

            e.Graphics.Clear(EffectiveBackColor(groupBox, theme));

            using (Pen pen = new Pen(theme.Border))
            {
                // Left, right, bottom, and the two stubs of the top edge either
                // side of the caption.
                e.Graphics.DrawLine(pen, 0, textTop, 0, groupBox.Height - 1);
                e.Graphics.DrawLine(pen, groupBox.Width - 1, textTop, groupBox.Width - 1, groupBox.Height - 1);
                e.Graphics.DrawLine(pen, 0, groupBox.Height - 1, groupBox.Width - 1, groupBox.Height - 1);
                e.Graphics.DrawLine(pen, 0, textTop, 6, textTop);
                e.Graphics.DrawLine(pen, 6 + textSize.Width, textTop, groupBox.Width - 1, textTop);
            }

            TextRenderer.DrawText(e.Graphics, groupBox.Text, groupBox.Font, new Point(6, 0), theme.Text);
        }


        private static void Form_ApplyDarkTitleBar(object sender, EventArgs e)
        {
            Control form = (Control)sender;
            NativeMethods.UseImmersiveDarkMode(form.Handle, Theme.Current.Mode == ThemeMode.Dark);
        }


        private static void Control_ApplyDarkScrollBars(object sender, EventArgs e)
        {
            Control control = (Control)sender;
            NativeMethods.UseDarkScrollBars(control.Handle, Theme.Current.Mode == ThemeMode.Dark);
        }


        private static void ComboBox_ApplyDarkChrome(object sender, EventArgs e)
        {
            Control comboBox = (Control)sender;
            NativeMethods.UseDarkComboBox(comboBox.Handle, Theme.Current.Mode == ThemeMode.Dark);
        }


        private static void Glyph_InvalidateOnHover(object sender, EventArgs e)
        {
            ((Control)sender).Invalidate();
        }


        private static void CheckBox_Paint(object sender, PaintEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            PaintGlyphControl(checkBox, e.Graphics, checkBox.Checked, false);
        }


        private static void RadioButton_Paint(object sender, PaintEventArgs e)
        {
            RadioButton radioButton = (RadioButton)sender;
            PaintGlyphControl(radioButton, e.Graphics, radioButton.Checked, true);
        }


        private static void Parent_PaintTextBoxBorders(object sender, PaintEventArgs e)
        {
            Control parent = (Control)sender;

            using (Pen pen = new Pen(Theme.Current.Border))
            {
                foreach (Control child in parent.Controls)
                {
                    if (!(child is TextBox) || !BorderedMarker.Equals(child.Tag) || !child.Visible)
                        continue;

                    e.Graphics.DrawRectangle(
                        pen,
                        child.Left - 1,
                        child.Top - 1,
                        child.Width + 1,
                        child.Height + 1
                    );
                }
            }
        }


        private static void ComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            Theme theme = Theme.Current;

            if (e.Index < 0)
                return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (SolidBrush background = new SolidBrush(selected ? theme.SurfaceActive : theme.Surface))
                e.Graphics.FillRectangle(background, e.Bounds);

            TextRenderer.DrawText(
                e.Graphics,
                comboBox.GetItemText(comboBox.Items[e.Index]),
                e.Font,
                e.Bounds,
                theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            );
        }


        private static void ToolTip_Draw(object sender, DrawToolTipEventArgs e)
        {
            Theme theme = Theme.Current;

            using (SolidBrush background = new SolidBrush(theme.Surface))
                e.Graphics.FillRectangle(background, e.Bounds);

            using (Pen pen = new Pen(theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);

            TextRenderer.DrawText(
                e.Graphics,
                e.ToolTipText,
                e.Font,
                e.Bounds,
                theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            );
        }
        #endregion
    }
}
