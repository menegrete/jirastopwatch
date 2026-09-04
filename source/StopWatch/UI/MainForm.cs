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

using StopWatch.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StopWatch
{
    public partial class MainForm : Form
    {
        #region public methods
        public MainForm()
        {
            settings = Settings.Instance;
            if (!settings.Load())
                MessageBox.Show(string.Format("An error occurred while loading settings for Jira StopWatch. Your configuration file has most likely become corrupted.{0}{0}And older configuration file has been loaded instead, so please verify your settings.", Environment.NewLine), "Jira StopWatch");

            // Before InitializeComponent, so every control is built with the
            // right palette already in place.
            Theme.Current = Theme.ForMode(settings.Theme);

            Logger.Instance.LogfilePath = Path.Combine(Application.UserAppDataPath, "jirastopwatch.log");
            Logger.Instance.Enabled = settings.LoggingEnabled;

            restRequestFactory = new RestRequestFactory();
            jiraApiRequestFactory = new JiraApiRequestFactory(restRequestFactory);

            restClientFactory = new RestClientFactory();
            restClientFactory.BaseUrl = this.settings.JiraBaseUrl;

            jiraApiRequester = new JiraApiRequester(restClientFactory, jiraApiRequestFactory);

            jiraClient = new JiraClient(jiraApiRequestFactory, jiraApiRequester);

            jiraService = new IssueJiraService(jiraClient, this.settings);

            filterProvider = new FilterProvider(jiraClient, this.settings);
            filterProvider.FiltersLoaded += filterProvider_FiltersLoaded;

            InitializeComponent();

            pMain.HorizontalScroll.Maximum = 0;
            pMain.AutoScroll = false;
            pMain.VerticalScroll.Visible = false;
            pMain.AutoScroll = true;

            Text = string.Format("{0} v. {1}", Application.ProductName, Application.ProductVersion);

            cbFilters.DropDownStyle = ComboBoxStyle.DropDownList;
            cbFilters.DisplayMember = "Name";

            ticker = new Timer();
            // First run should be almost immediately after start
            ticker.Interval = firstDelay;
            ticker.Tick += ticker_Tick;

            activeTimer = new ActiveTimerViewModel(() => this.issueControls.Cast<ITimerSource>());

            ApplyTheme();
        }


        /// <summary>
        /// Paints the main window with the active theme. Called at construction
        /// and again whenever the user changes the theme setting.
        /// </summary>
        public void ApplyTheme()
        {
            ThemeApplier.Apply(this);

            // The top strip is an accent band, not a plain surface, so it and its
            // caption are the two places the generic walk cannot get right.
            pTop.BackColor = Theme.Current.Accent;
            lblActiveFilter.BackColor = Color.Transparent;
            lblActiveFilter.ForeColor = Theme.Current.AccentText;

            pbSettings.BackgroundImage = ThemeIcons.Settings;
            pbMiniView.BackgroundImage = ThemeIcons.MiniView;

            foreach (var issue in issueControls)
                issue.ApplyTheme();

            // Not part of the Control tree, so ThemeApplier's walk cannot reach it.
            if (miniView != null)
                miniView.ApplyTheme();
        }

        public void HandleSessionLock()
        {
            if (settings.PauseOnSessionLock == PauseAndResumeSetting.NoPause)
                return;

            // Which rows are running is the active-timer model's job to know.
            // As before, only the first running row is paused and remembered.
            IssueControl issue = activeTimer.RunningSources.OfType<IssueControl>().FirstOrDefault();
            if (issue == null)
                return;

            lastRunningIssue = issue;
            // The last place InvokeIfRequired earns its keep: SessionSwitch
            // arrives on a thread with no synchronization context, so there is
            // no await that would bring this back to the UI thread by itself.
            issue.InvokeIfRequired(
                () => issue.Pause()
            );
            activeTimer.Refresh();
        }

        public void HandleSessionUnlock()
        {
            if (settings.PauseOnSessionLock != PauseAndResumeSetting.PauseAndResume)
                return;

            if (lastRunningIssue != null)
            {
                IssueControl resumed = lastRunningIssue;
                resumed.InvokeIfRequired(
                    () => resumed.Start()
                );
                lastRunningIssue = null;

                // Start() does not raise TimerStarted, so tell the model directly.
                activeTimer.NotifyTimerStarted(resumed);
            }
        }
        #endregion


        #region private eventhandlers
        void issue_TimerStarted(object sender, EventArgs e)
        {
            IssueControl senderCtrl = (IssueControl)sender;
            ChangeIssueState(senderCtrl.IssueKey);

            if (!settings.AllowMultipleTimers)
            {
                foreach (var issue in this.issueControls)
                    if (issue != senderCtrl)
                        issue.Pause();
            }

            activeTimer.NotifyTimerStarted(senderCtrl);
        }


        void Issue_TimerReset(object sender, EventArgs e)
        {
            UpdateTotalTime();
            activeTimer.Refresh();
        }


        void ticker_Tick(object sender, EventArgs e)
        {
            bool firstTick = ticker.Interval == firstDelay;

            ticker.Interval = defaultDelay;

            UpdateJiraRelatedData(firstTick);
            UpdateIssuesOutput(firstTick);

            SaveSettingsAndIssueStates();

            if (firstTick)
            {
                CheckForUpdates();
            }
        }


        private void pbMiniView_Click(object sender, EventArgs e)
        {
            EnterMiniView();
        }


        private void miniView_RestoreRequested(object sender, EventArgs e)
        {
            ExitMiniView();
        }


        private void pbSettings_Click(object sender, EventArgs e)
        {
            // No topmost juggling here any more: ModalDialog.ShowOver keeps the
            // dialog above this window, and InitializeIssueControls re-applies
            // TopMost from the setting once the dialog is accepted.
            EditSettings();
        }


        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Reached even while hidden behind the mini view - a session
            // shutdown, for instance - so the mini view has to come down with
            // it, and settings still get written exactly as before.
            if (miniView != null)
            {
                miniView.ClosingForShutdown = true;
                miniView.Close();
                miniView = null;
            }

            SaveSettingsAndIssueStates();
        }


        private void MainForm_Shown(object sender, EventArgs e)
        {
            if (this.settings.FirstRun)
            {
                this.settings.FirstRun = false;

                // Deferred rather than called inline: showing a modal dialog
                // from within the Shown event races the main window's own
                // activation, and the dialog can end up behind it.
                this.BeginInvoke(new Action(EditSettings));
            }
            else
            {
                if (IsJiraEnabled)
                    AuthenticateJira(this.settings.Username, this.settings.ApiToken);
            }

            InitializeIssueControls();

            // Restore what the last run left behind. The rows hand this to their
            // own model; the key still goes through the control, because that is
            // what triggers the summary lookup.
            int i = 0;
            foreach (var issueControl in this.issueControls)
            {
                if (i < settings.PersistedIssues.Count)
                {
                    var persistedIssue = settings.PersistedIssues[i];
                    issueControl.IssueKey = persistedIssue.Key;
                    issueControl.Model.Hydrate(persistedIssue, this.settings.SaveTimerState);
                }
                i++;
            }

            ticker.Start();
        }


        private void cbFilters_DropDown(object sender, EventArgs e)
        {
            LoadFilters();
        }


        private void cbFilters_SelectedIndexChanged(object sender, EventArgs e)
        {
            // The provider owns which filter is active, and saves it.
            filterProvider.Current = (FilterItem)cbFilters.SelectedItem;
        }


        /// <summary>
        /// Repaints the filter combo from the provider. The provider decides
        /// what the list is and which entry is selected; this only shows it.
        /// </summary>
        private void filterProvider_FiltersLoaded(object sender, EventArgs e)
        {
            FilterItem current = filterProvider.Current;

            cbFilters.Items.Clear();
            foreach (var filter in filterProvider.Filters)
                cbFilters.Items.Add(filter);

            if (current != null)
                cbFilters.SelectedItem = current;
        }


        private void MainForm_Resize(object sender, EventArgs e)
        {
            // Mono for MacOSX and Linux do not implement the notifyIcon
            // so ignore this feature if we are not running on Windows
            if (!CrossPlatformHelpers.IsWindowsEnvironment())
                return;

            // While the mini view is up this window is hidden on purpose. The
            // tray icon must not appear as well: two stand-ins for one hidden
            // window is one too many.
            if (inMiniView)
                return;

            if (!this.settings.MinimizeToTray)
                return;

            if (WindowState == FormWindowState.Minimized)
            {
                this.notifyIcon.Visible = true;
                this.Hide();
            }
            else if (WindowState == FormWindowState.Normal)
            {
                this.notifyIcon.Visible = false;
            }
        }

        private void notifyIcon_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void lblConnectionStatus_Click(object sender, EventArgs e)
        {
            if (jiraClient.SessionValid)
                return;

            string msg = string.Format("Jira StopWatch could not connect to your Jira server. Error returned:{0}{0}{1}", Environment.NewLine, jiraClient.ErrorMessage);
            MessageBox.Show(msg, "Connection error");
        }
        #endregion


        #region private methods
        /// <summary>
        /// Hides this window and puts the floating mini view up in its place.
        /// The window's geometry is remembered so that coming back lands
        /// exactly where the user left it.
        /// </summary>
        private void EnterMiniView()
        {
            if (inMiniView)
                return;

            restoreBounds = new Rectangle(Location, Size);
            restoreWindowState = WindowState;

            if (miniView == null)
            {
                // Created on first use: nobody should pay WPF's start-up cost
                // for a view they never open.
                miniView = new MiniTimerWindow(activeTimer, settings);
                miniView.RestoreRequested += miniView_RestoreRequested;
            }

            inMiniView = true;

            // The tray icon belongs to the minimize-to-tray feature, not here.
            notifyIcon.Visible = false;

            miniView.ShowAt();
            Hide();
        }


        /// <summary>Takes the mini view down and brings this window back as it was.</summary>
        private void ExitMiniView()
        {
            if (!inMiniView)
                return;

            inMiniView = false;

            if (miniView != null)
                miniView.Hide();

            Show();

            WindowState = restoreWindowState == FormWindowState.Minimized
                ? FormWindowState.Normal
                : restoreWindowState;

            if (!restoreBounds.IsEmpty)
            {
                Location = restoreBounds.Location;
                Size = restoreBounds.Size;
            }

            Activate();
        }


        private void AuthenticateJira(string username, string apiToken)
        {
            _ = AuthenticateJiraAsync(username, apiToken);
        }


        private async Task AuthenticateJiraAsync(string username, string apiToken)
        {
            lblConnectionStatus.Text = "Connecting...";
            lblConnectionStatus.ForeColor = Theme.Current.Text;

            // Everything after the await is back on the UI thread, so the
            // controls below are touched directly.
            bool authenticated = await Task.Run(() => jiraClient.Authenticate(username, apiToken));

            if (authenticated)
                UpdateIssuesOutput(true);

            UpdateJiraRelatedData(true);
        }

        private void issue_RemoveMeTriggered(object sender, EventArgs e)
        {
            if (this.settings.IssueCount > 1)
            {
                this.settings.IssueCount--;
            }
            this.InitializeIssueControls();
        }

        private void pbAddIssue_Clicked(object sender, EventArgs e)
        {
            IssueAdd();
        }

        private void IssueAdd()
        {
            if (this.settings.IssueCount < settings.MaxIssues || this.issueControls.Count() < settings.MaxIssues)
            {
                this.settings.IssueCount++;
                this.InitializeIssueControls();
                IssueControl AddedIssue = this.issueControls.Last();
                IssueSetCurrentByControl(AddedIssue);
                this.pMain.ScrollControlIntoView(AddedIssue);
            }
        }

        private void InitializeIssueControls()
        {
            this.SuspendLayout();

            if (this.settings.IssueCount >= settings.MaxIssues)
            {
                // Max reached.  Reset number in case it is larger
                this.settings.IssueCount = settings.MaxIssues;

                // Update tooltip to reflect the fact that you can't add anymore
                // We don't disable the button since then the tooltip doesn't show but
                // the click won't do anything if we have too many issues
                this.ttMain.SetToolTip(this.pbAddIssue, string.Format("You have reached the max limit of {0} issues and cannot add another", settings.MaxIssues.ToString()));
                this.pbAddIssue.Cursor = System.Windows.Forms.Cursors.No;
            }
            else
            {
                if (this.settings.IssueCount < 1)
                    this.settings.IssueCount = 1;

                // Reset status 
                this.ttMain.SetToolTip(this.pbAddIssue, "Add another issue row (CTRL-N)");
                this.pbAddIssue.Cursor = System.Windows.Forms.Cursors.Hand;
            }
            
            // Remove IssueControl where user has clicked the remove button
            foreach (IssueControl issue in this.issueControls)
            {
                if (issue.MarkedForRemoval)
                    this.pMain.Controls.Remove(issue);
            }


            // If we have too many issueControl controls, compared to this.IssueCount
            // remove the ones not needed
            while (this.issueControls.Count() > this.settings.IssueCount)
            {
                var issue = this.issueControls.Last();
                this.pMain.Controls.Remove(issue);
            }

            // Create issueControl controls needed
            while (this.issueControls.Count() < this.settings.IssueCount)
            {
                var issue = new IssueControl(this.settings, this.jiraService, this.filterProvider);
                issue.RemoveMeTriggered += new EventHandler(this.issue_RemoveMeTriggered);
                issue.TimerStarted += issue_TimerStarted;
                issue.TimerReset += Issue_TimerReset;
                issue.Selected += Issue_Selected;
                issue.TimeEdited += Issue_TimeEdited;
                issue.ApplyTheme();
                this.pMain.Controls.Add(issue);
            }

            // To make sure that pMain's scrollbar doesn't screw up, all IssueControls need to have
            // their position reset, before positioning them again
            foreach (IssueControl issue in this.issueControls)
            {
                issue.Left = 0;
                issue.Top = 0;
            }

            // Now position all issueControl controls
            int i = 0;
            bool EnableRemoveIssue = this.issueControls.Count() > 1;
            foreach (IssueControl issue in this.issueControls)
            {
                issue.ToggleRemoveIssueButton(EnableRemoveIssue);
                issue.Top = i * issue.Height;
                i++;
            }

            this.ClientSize = new Size(pBottom.Width, this.settings.IssueCount * issueControls.Last().Height + pMain.Top + pBottom.Height);

            var workingArea = Screen.FromControl(this).WorkingArea;
            if (this.Height > workingArea.Height)
                this.Height = workingArea.Height;

            if (this.Bottom > workingArea.Bottom)
                this.Top = workingArea.Bottom - this.Height;
            
            pMain.Height = ClientSize.Height - pTop.Height - pBottom.Height;
            pBottom.Top = ClientSize.Height - pBottom.Height;

            this.TopMost = this.settings.AlwaysOnTop;

            if (currentIssueIndex >= issueControls.Count())
                IssueSetCurrent(issueControls.Count() - 1);
            else
                IssueSetCurrent(currentIssueIndex);

            this.ResumeLayout(false);
            this.PerformLayout();
            UpdateIssuesOutput(true);

            // The set of rows just changed, so whatever the active-timer model
            // resolved to may no longer be on screen.
            activeTimer.Refresh();
        }

        private void Issue_TimeEdited(object sender, EventArgs e)
        {
            UpdateTotalTime();
        }

        private void Issue_Selected(object sender, EventArgs e)
        {
            IssueSetCurrentByControl((IssueControl)sender);
            activeTimer.Refresh();
        }

        private void IssueSetCurrentByControl(IssueControl control)
        {
            int i = 0;
            foreach (var issue in issueControls)
            {
                if (issue == control)
                {
                    IssueSetCurrent(i);
                    return;
                }
                i++;
            }
        }

        private void UpdateIssuesOutput(bool updateSummary = false)
        {
            foreach (var issue in this.issueControls)
                issue.UpdateOutput(updateSummary);
            UpdateTotalTime();
        }


        private void UpdateTotalTime()
        {
            TimeSpan totalTime = new TimeSpan();
            foreach (var issue in this.issueControls)
                totalTime += issue.WatchTimer.TimeElapsed;
            tbTotalTime.Text = JiraTimeHelpers.TimeSpanToJiraTime(totalTime);
        }


        private void UpdateJiraRelatedData(bool firstTick)
        {
            _ = UpdateJiraRelatedDataAsync(firstTick);
        }


        private async Task UpdateJiraRelatedDataAsync(bool firstTick)
        {
            if (!IsJiraEnabled)
            {
                SetConnectionStatus(false);
                return;
            }

            bool valid = jiraClient.SessionValid || await Task.Run(() => jiraClient.ValidateSession());

            SetConnectionStatus(valid);

            if (!valid)
                return;

            if (firstTick)
                LoadFilters();

            UpdateIssuesOutput(firstTick);
        }


        private void SetConnectionStatus(bool connected)
        {
            if (connected)
            {
                lblConnectionStatus.Text = "Connected";
                lblConnectionStatus.ForeColor = Theme.Current.Success;
                lblConnectionStatus.Font = new Font(lblConnectionStatus.Font, FontStyle.Regular);
                lblConnectionStatus.Cursor = Cursors.Default;
            }
            else
            {
                lblConnectionStatus.Text = "Not connected";
                lblConnectionStatus.ForeColor = Theme.Current.Danger;
                lblConnectionStatus.Font = new Font(lblConnectionStatus.Font, FontStyle.Regular | FontStyle.Underline);
                lblConnectionStatus.Cursor = Cursors.Hand;
            }
        }


        private void ChangeIssueState(string issueKey)
        {
            if (string.IsNullOrWhiteSpace(settings.StartTransitions))
                return;

            _ = ChangeIssueStateAsync(issueKey);
        }


        private async Task ChangeIssueStateAsync(string issueKey)
        {
            var startTransitions = this.settings.StartTransitions
                .Split(new string[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().ToLower()).ToArray();

            await Task.Run(() =>
            {
                var availableTransitions = jiraClient.GetAvailableTransitions(issueKey);
                if (availableTransitions == null || availableTransitions.Transitions.Count() == 0)
                    return;

                foreach (var t in availableTransitions.Transitions)
                {
                    if (startTransitions.Any(t.Name.ToLower().Contains))
                    {
                        jiraClient.DoTransition(issueKey, t.Id);
                        return;
                    }
                }
            });
        }


        private void EditSettings()
        {
            using (var form = new SettingsForm(this.settings))
            {
                if (ModalDialog.ShowOver(form, this) == System.Windows.Forms.DialogResult.OK)
                {
                    // Repaint before the issue rows are rebuilt below, so the new
                    // rows are created with the theme already switched.
                    if (Theme.Current.Mode != this.settings.Theme)
                    {
                        Theme.Current = Theme.ForMode(this.settings.Theme);
                        ApplyTheme();
                    }

                    restClientFactory.BaseUrl = this.settings.JiraBaseUrl;
                    Logging.Logger.Instance.Enabled = settings.LoggingEnabled;
                    if (IsJiraEnabled)
                        AuthenticateJira(this.settings.Username, this.settings.ApiToken);
                    InitializeIssueControls();
                }
            }
        }


        private void SaveSettingsAndIssueStates()
        {
            settings.PersistedIssues.Clear();

            foreach (var issueControl in this.issueControls)
                settings.PersistedIssues.Add(issueControl.Model.Persist());

            this.settings.Save();
        }


        private void LoadFilters()
        {
            // Fire and forget: the combo repaints itself from FiltersLoaded
            // once the answer arrives, exactly as before.
            _ = filterProvider.LoadAsync();
        }


        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_SHOWME)
                ShowOnTop();

            base.WndProc(ref m);
        }


        private void ShowOnTop()
        {
            // Launching the app again while the mini view is up means the user
            // wants the full window, not a second stand-in next to it.
            if (inMiniView)
            {
                ExitMiniView();
                return;
            }

            if (WindowState == FormWindowState.Minimized) {
                Show();
                WindowState = FormWindowState.Normal;
                notifyIcon.Visible = false;
            }

            // get our current "TopMost" value (ours will always be false though)
            // make our form jump to the top of everything
            // set it back to whatever it was
            bool top = TopMost;
            TopMost = true;
            TopMost = top;
        }


        private void CheckForUpdates()
        {
            if (!settings.CheckForUpdate)
                return;

            _ = CheckForUpdatesAsync();
        }


        private async Task CheckForUpdatesAsync()
        {
            GithubRelease latestRelease = await Task.Run(() => ReleaseHelper.GetLatestVersion());
            if (latestRelease == null)
                return;

            string currentVersion = Application.ProductVersion;
            if (string.Compare(latestRelease.TagName, currentVersion) <= 0)
                return;

            string msg = string.Format("There is a newer version available of Jira StopWatch.{0}{0}Latest release is {1}. You are running version {2}.{0}{0}Do you want to download latest release?",
                Environment.NewLine,
                latestRelease.TagName,
                currentVersion);
            if (MessageBox.Show(msg, "New version available", MessageBoxButtons.YesNo) == DialogResult.Yes)
                System.Diagnostics.Process.Start("https://github.com/jirastopwatch/jirastopwatch/releases/latest");





        }
        #endregion


        #region private members
        private bool IsJiraEnabled
        {
            get
            {
                return !(
                    string.IsNullOrWhiteSpace(settings.JiraBaseUrl) ||
                    string.IsNullOrWhiteSpace(settings.Username) ||
                    string.IsNullOrWhiteSpace(settings.ApiToken)
                );
            }
        }

        private IEnumerable<IssueControl> issueControls
        {
            get
            {
                return this.pMain.Controls.OfType<IssueControl>();
            }
        }

        private Timer ticker;

        private JiraApiRequestFactory jiraApiRequestFactory;
        private RestRequestFactory restRequestFactory;
        private JiraApiRequester jiraApiRequester;
        private RestClientFactory restClientFactory;
        private JiraClient jiraClient;

        private Settings settings;

        private IssueControl lastRunningIssue = null;

        private ActiveTimerViewModel activeTimer;

        private readonly IssueJiraService jiraService;

        private readonly FilterProvider filterProvider;

        private MiniTimerWindow miniView;
        private bool inMiniView;
        private Rectangle restoreBounds;
        private FormWindowState restoreWindowState = FormWindowState.Normal;
        #endregion


        #region private consts
        private const int firstDelay = 500;
        private const int defaultDelay = 30000;
        #endregion

        private int currentIssueIndex;


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Up))
            {
                IssueMoveUp();
                return true;
            }


            if (keyData == (Keys.Control | Keys.Down))
            {
                IssueMoveDown();
                return true;
            }

            if (keyData == (Keys.Control | Keys.P))
            {
                IssueTogglePlay();
                return true;
            }

            if (keyData == (Keys.Control | Keys.L))
            {
                IssuePostWorklog();
                return true;
            }

            if (keyData == (Keys.Control | Keys.E))
            {
                IssueEditTime();
                return true;
            }

            if (keyData == (Keys.Control | Keys.R))
            {
                IssueReset();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Delete))
            {
                IssueDelete();
                return true;
            }

            if (keyData == (Keys.Control | Keys.I))
            {
                IssueFocusKey();
                return true;
            }

            if (keyData == (Keys.Control | Keys.N))
            {
                IssueAdd();
                return true;
            }

            if (keyData == (Keys.Control | Keys.C))
            {
                IssueCopyToClipboard();
                return true;
            }

            if (keyData == (Keys.Control | Keys.V))
            {
                IssuePasteFromClipboard();
                return true;
            }

            if (keyData == (Keys.Control | Keys.O))
            {
                IssueOpenInBrowser();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Down))
            {
                IssueOpenCombo();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }



        private void IssueOpenInBrowser()
        {
            issueControls.ToList()[currentIssueIndex].OpenJira();
        }

        private void IssueCopyToClipboard()
        {
            issueControls.ToList()[currentIssueIndex].CopyKeyToClipboard();
        }

        private void IssuePasteFromClipboard()
        {
            issueControls.ToList()[currentIssueIndex].PasteKeyFromClipboard();
        }

        private void IssueEditTime()
        {
            issueControls.ToList()[currentIssueIndex].EditTime();
        }

        private void IssueDelete()
        {
            issueControls.ToList()[currentIssueIndex].Remove();
        }

        private void IssueFocusKey()
        {
            issueControls.ToList()[currentIssueIndex].FocusKey();
        }

        private void IssueReset()
        {
            issueControls.ToList()[currentIssueIndex].Reset();
        }

        private void IssuePostWorklog()
        {
            issueControls.ToList()[currentIssueIndex].PostAndReset();
        }

        private void IssueTogglePlay()
        {
            issueControls.ToList()[currentIssueIndex].StartStop();
        }

        private void IssueMoveDown()
        {
            if (currentIssueIndex == issueControls.Count() - 1)
                return;

            IssueSetCurrent(currentIssueIndex + 1);
        }

        private void IssueMoveUp()
        {
            if (currentIssueIndex == 0)
                return;

            IssueSetCurrent(currentIssueIndex - 1);
        }

        private void IssueSetCurrent(int index)
        {
            currentIssueIndex = index;
            int i = 0;
            foreach (var issue in issueControls)
            {
                issue.Current = i == currentIssueIndex;
                if (i == currentIssueIndex)
                {
                    pMain.ScrollControlIntoView(issue);
                    issue.Focus();
                }
                i++;
            }
        }

        private void IssueOpenCombo()
        {
            issueControls.ToList()[currentIssueIndex].OpenCombo();
        }

        private void pbHelp_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://jirastopwatch.com/doc");
        }
    }
}
