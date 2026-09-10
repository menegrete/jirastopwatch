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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StopWatch
{
    /// <summary>
    /// The three ways out of the worklog dialog. The form returned OK, Yes and
    /// Cancel; naming them says which is which.
    /// </summary>
    internal enum WorklogResult
    {
        /// <summary>Cancelled; nothing is posted and nothing is kept.</summary>
        Cancel,

        /// <summary>Post the worklog now and reset the timer.</summary>
        Post,

        /// <summary>Keep the comment and the estimate on the row, post nothing.</summary>
        SaveForLater
    }


    /// <summary>
    /// Replaces WorklogForm: the comment, the start time and what to do with
    /// the remaining estimate.
    /// </summary>
    internal partial class WorklogWindow : Window
    {
        #region public members
        /// <summary>Which of the three buttons the user chose.</summary>
        public WorklogResult Result { get; private set; }

        public string Comment
        {
            get { return tbComment.Text; }
        }


        public EstimateUpdateMethods EstimateUpdateMethod
        {
            get { return estimateUpdateMethod; }
        }


        public string EstimateValue
        {
            get
            {
                switch (estimateUpdateMethod)
                {
                    case EstimateUpdateMethods.SetTo:
                        return tbSetTo.Text;
                    case EstimateUpdateMethods.ManualDecrease:
                        return tbReduceBy.Text;
                    default:
                        return null;
                }
            }
        }


        public DateTimeOffset InitialStartTime
        {
            get { return StartDate + StartTimeOfDay; }
        }
        #endregion


        #region public methods
        public WorklogWindow(DateTimeOffset startTime, TimeSpan timeElapsed, string comment, EstimateUpdateMethods estimateUpdateMethod, string estimateUpdateValue)
        {
            this.timeElapsed = timeElapsed;

            InitializeComponent();

            SourceInitialized += (s, e) => NativeMethods.SetTitleBarDarkMode(this, Theme.Current.Mode == StopWatch.ThemeMode.Dark);

            Result = WorklogResult.Cancel;

            if (!string.IsNullOrEmpty(comment))
            {
                // Two blank lines above whatever was saved earlier, with the
                // caret at the top, so the new note goes first.
                tbComment.Text = string.Format("{0}{0}{1}", Environment.NewLine, comment);
                tbComment.SelectionStart = 0;
            }

            // Read LocalDateTime once and fill both fields from the same value:
            // reading it repeatedly off the DateTimeOffset was observed to
            // shift the time zone.
            DateTime local = startTime.LocalDateTime;
            dpStartDate.SelectedDate = local.Date;
            tpStartTime.SelectedTime = local;

            switch (estimateUpdateMethod)
            {
                case EstimateUpdateMethods.Leave:
                    rdEstimateAdjustLeave.IsChecked = true;
                    break;
                case EstimateUpdateMethods.SetTo:
                    rdEstimateAdjustSetTo.IsChecked = true;
                    tbSetTo.Text = estimateUpdateValue;
                    break;
                case EstimateUpdateMethods.ManualDecrease:
                    rdEstimateAdjustManualDecrease.IsChecked = true;
                    tbReduceBy.Text = estimateUpdateValue;
                    break;
                default:
                    rdEstimateAdjustAuto.IsChecked = true;
                    break;
            }

            RemainingEstimateUpdated();

            Loaded += (s, e) => tbComment.Focus();
        }


        /// <summary>
        /// Fills in what Jira says the remaining estimate is. Arrives after the
        /// dialog is already up, because the lookup is a round trip; until then
        /// the two options read without their numbers.
        /// </summary>
        public void SetRemainingEstimate(string text, int seconds)
        {
            remainingEstimate = text;
            remainingEstimateSeconds = seconds;
            RemainingEstimateUpdated();
        }
        #endregion


        #region private eventhandlers
        private void tbComment_KeyDown(object sender, KeyEventArgs e)
        {
            // The comment field takes plain Enter as a newline, so submitting
            // from it needs the modifier.
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Submit();
                e.Handled = true;
            }
        }


        private void EstimateValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            Submit();
            e.Handled = true;
        }


        private void EstimateValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            // ClearValue, not Style = null: assigning null pins the field to
            // "no style" rather than reverting to the implicit dark TextBox
            // style, so typing would clear the complaint but leave the field
            // looking unstyled.
            ((TextBox)sender).ClearValue(StyleProperty);
        }


        private void EstimateValue_LostFocus(object sender, RoutedEventArgs e)
        {
            // Validated on the way out so that the field says what is wrong,
            // but leaving is never blocked: being unable to leave an invalid
            // field would make it impossible to pick a different option.
            Validate((TextBox)sender, false);
        }


        private void estimateUpdateMethod_Changed(object sender, RoutedEventArgs e)
        {
            if (rdEstimateAdjustSetTo.IsChecked == true)
                estimateUpdateMethod = EstimateUpdateMethods.SetTo;
            else if (rdEstimateAdjustManualDecrease.IsChecked == true)
                estimateUpdateMethod = EstimateUpdateMethods.ManualDecrease;
            else if (rdEstimateAdjustLeave.IsChecked == true)
                estimateUpdateMethod = EstimateUpdateMethods.Leave;
            else
                estimateUpdateMethod = EstimateUpdateMethods.Auto;

            // Only the field belonging to the chosen option takes input, and a
            // field that is out of play stops complaining.
            tbSetTo.IsEnabled = estimateUpdateMethod == EstimateUpdateMethods.SetTo;
            tbReduceBy.IsEnabled = estimateUpdateMethod == EstimateUpdateMethods.ManualDecrease;

            if (!tbSetTo.IsEnabled)
                tbSetTo.ClearValue(StyleProperty);
            if (!tbReduceBy.IsEnabled)
                tbReduceBy.ClearValue(StyleProperty);
        }


        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            Submit();
        }


        private void btnSaveForLater_Click(object sender, RoutedEventArgs e)
        {
            // Nothing is posted, so nothing needs to validate: whatever is in
            // the estimate fields is kept as typed, as it was before.
            Result = WorklogResult.SaveForLater;
            DialogResult = true;
        }
        #endregion


        #region private methods
        private void Submit()
        {
            if (!ValidateAll())
                return;

            Result = WorklogResult.Post;
            DialogResult = true;
        }


        private DateTime StartDate
        {
            get { return dpStartDate.SelectedDate ?? DateTime.Now.Date; }
        }


        private TimeSpan StartTimeOfDay
        {
            get { return tpStartTime.SelectedTime?.TimeOfDay ?? DateTime.Now.TimeOfDay; }
        }


        /// <summary>
        /// Recomposes the two option labels around what Jira reported. Both
        /// read without a number until the lookup answers.
        /// </summary>
        private void RemainingEstimateUpdated()
        {
            rdEstimateAdjustLeave.Content = string.IsNullOrWhiteSpace(remainingEstimate)
                ? "_Leave Unchanged"
                : string.Format("_Leave As {0}", remainingEstimate);

            rdEstimateAdjustAuto.Content = remainingEstimateSeconds > 0
                ? string.Format("Adjust _Automatically (to {0})", AdjustedRemainingEstimate)
                : "Adjust _Automatically";
        }


        private string AdjustedRemainingEstimate
        {
            get
            {
                int seconds = remainingEstimateSeconds - (int)Math.Floor(timeElapsed.TotalSeconds);

                return seconds > 0
                    ? JiraTimeHelpers.TimeSpanToJiraTime(new TimeSpan(0, 0, seconds))
                    : "0m";
            }
        }


        /// <summary>
        /// Only the field belonging to the chosen option has to be valid; the
        /// other one is not going to be read.
        /// </summary>
        private bool ValidateAll()
        {
            switch (estimateUpdateMethod)
            {
                case EstimateUpdateMethods.SetTo:
                    return Validate(tbSetTo, true);
                case EstimateUpdateMethods.ManualDecrease:
                    return Validate(tbReduceBy, true);
                default:
                    return true;
            }
        }


        /// <summary>
        /// Marks the field when its content is empty or does not parse as Jira
        /// time. A disabled field is never wrong.
        /// </summary>
        private bool Validate(TextBox field, bool focusIfInvalid)
        {
            if (!field.IsEnabled)
                return true;

            bool valid = !string.IsNullOrWhiteSpace(field.Text)
                && JiraTimeHelpers.JiraTimeToTimeSpan(field.Text) != null;

            if (valid)
                field.ClearValue(StyleProperty);
            else
                field.Style = (Style)FindResource("InvalidInput");

            if (!valid && focusIfInvalid)
            {
                field.Focus();
                field.SelectAll();
            }

            return valid;
        }
        #endregion


        #region private members
        private readonly TimeSpan timeElapsed;

        private EstimateUpdateMethods estimateUpdateMethod = EstimateUpdateMethods.Auto;

        private string remainingEstimate;
        private int remainingEstimateSeconds = -1;
        #endregion
    }
}
