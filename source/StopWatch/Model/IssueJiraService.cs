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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StopWatch
{
    /// <summary>What a posted worklog did, so that the caller knows whether to reset.</summary>
    internal class PostWorklogResult
    {
        /// <summary>
        /// Whether everything that had to be posted was posted. Only then may
        /// the row's timer be reset: a reset after a failed post loses the time.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>Whether a comment was posted in the comment track.</summary>
        public bool CommentPosted { get; set; }

        /// <summary>Whether the worklog itself was posted.</summary>
        public bool WorklogPosted { get; set; }
    }


    /// <summary>The remaining estimate of an issue, as Jira reports it.</summary>
    internal class RemainingEstimate
    {
        /// <summary>Jira's own notation, or null when the issue has no estimate.</summary>
        public string Text { get; set; }

        /// <summary>The same value in seconds, or -1 when there is none.</summary>
        public int Seconds { get; set; }

        public static RemainingEstimate None
        {
            get { return new RemainingEstimate { Text = "", Seconds = -1 }; }
        }
    }


    /// <summary>
    /// The Jira calls an issue row makes, with their input passed in and their
    /// result returned.
    ///
    /// This used to live inside IssueControl, which meant the worklog rules
    /// read their own input back out of controls on the UI thread and could not
    /// be tested at all. Nothing here knows about controls or windows.
    /// </summary>
    internal class IssueJiraService
    {
        #region public methods
        public IssueJiraService(IJiraOperations jira, Settings settings)
        {
            if (jira == null)
                throw new ArgumentNullException("jira");
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.jira = jira;
            this.settings = settings;
        }


        /// <summary>
        /// Posts the comment track and the worklog for one issue, in that order.
        ///
        /// The order matters and so does stopping on failure: the comment goes
        /// first because <see cref="WorklogCommentSetting.CommentOnly"/> means
        /// the worklog must not carry it, and a failed comment has to abort the
        /// whole thing so that the caller does not reset a timer whose time was
        /// never recorded anywhere.
        /// </summary>
        public async Task<PostWorklogResult> PostWorklogAsync(string key, DateTimeOffset startTime, TimeSpan timeElapsed, string comment, EstimateUpdateMethods estimateUpdateMethod, string estimateUpdateValue)
        {
            var result = new PostWorklogResult();

            await Task.Run(() =>
            {
                bool postSuccesful = true;

                // First post the comment in the comment track - and clear the
                // comment string, if it should only be posted there.
                // Only actually post in the comment track if text is not empty.
                if (settings.PostWorklogComment != WorklogCommentSetting.WorklogOnly && !string.IsNullOrEmpty(comment))
                {
                    postSuccesful = jira.PostComment(key, comment);
                    result.CommentPosted = postSuccesful;

                    if (postSuccesful && settings.PostWorklogComment == WorklogCommentSetting.CommentOnly)
                        comment = "";
                }

                // Now post the worklog with timeElapsed - and the comment
                // unless it was reset.
                if (postSuccesful)
                {
                    postSuccesful = jira.PostWorklog(key, startTime, timeElapsed, comment, estimateUpdateMethod, estimateUpdateValue);
                    result.WorklogPosted = postSuccesful;
                }

                result.Success = postSuccesful;
            });

            return result;
        }


        /// <summary>
        /// Resolves an issue's summary, already carrying whatever the client
        /// composes - project prefix and parent summary included.
        ///
        /// Returns an empty summary when there is no key or no session, and
        /// null when Jira refused the request, which the caller reads as "leave
        /// the summary you already have".
        /// </summary>
        public async Task<string> GetSummaryAsync(string key)
        {
            if (string.IsNullOrEmpty(key) || !jira.SessionValid)
                return "";

            bool addProjectName = settings.IncludeProjectName;

            return await Task.Run(() =>
            {
                try
                {
                    return jira.GetIssueSummary(key, addProjectName);
                }
                catch (RequestDeniedException)
                {
                    // The caller keeps the summary it already had.
                    return null;
                }
            });
        }


        /// <summary>
        /// Reads an issue's remaining estimate. Returns
        /// <see cref="RemainingEstimate.None"/> when there is no key, no
        /// session, or Jira reports no time tracking for the issue.
        /// </summary>
        public async Task<RemainingEstimate> GetRemainingEstimateAsync(string key)
        {
            if (string.IsNullOrEmpty(key) || !jira.SessionValid)
                return RemainingEstimate.None;

            return await Task.Run(() =>
            {
                TimetrackingFields timetracking = jira.GetIssueTimetracking(key);
                if (timetracking == null)
                    return RemainingEstimate.None;

                return new RemainingEstimate
                {
                    Text = timetracking.RemainingEstimate,
                    Seconds = timetracking.RemainingEstimateSeconds
                };
            });
        }


        /// <summary>
        /// The issues offered as completions for an issue key, for the JQL of
        /// the filter the user has active. An empty JQL means no filter is
        /// selected, and there is nothing to offer.
        /// </summary>
        public async Task<List<Issue>> GetIssuesAsync(string jql)
        {
            if (string.IsNullOrEmpty(jql) || !jira.SessionValid)
                return new List<Issue>();

            return await Task.Run(() =>
            {
                SearchResult result = jira.GetIssuesByJQL(jql);
                if (result == null || result.Issues == null)
                    return new List<Issue>();

                return result.Issues;
            });
        }
        #endregion


        #region private members
        private readonly IJiraOperations jira;
        private readonly Settings settings;
        #endregion
    }
}
