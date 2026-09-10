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

namespace StopWatchTest
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Moq;
    using NUnit.Framework;
    using StopWatch;


    [TestFixture]
    public class IssueJiraServiceTest
    {
        private Mock<IJiraOperations> jira;
        private Settings settings;
        private IssueJiraService service;

        /// <summary>What the fake Jira was asked to do, in the order it was asked.</summary>
        private List<string> calls;

        private static readonly DateTimeOffset StartTime = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan Elapsed = TimeSpan.FromMinutes(30);

        [SetUp]
        public void Setup()
        {
            calls = new List<string>();

            jira = new Mock<IJiraOperations>();
            jira.SetupGet(j => j.SessionValid).Returns(true);
            jira.Setup(j => j.PostComment(It.IsAny<string>(), It.IsAny<string>()))
                .Callback(() => calls.Add("comment"))
                .Returns(true);
            jira.Setup(j => j.PostWorklog(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()))
                .Callback(() => calls.Add("worklog"))
                .Returns(true);

            settings = new Settings();
            service = new IssueJiraService(jira.Object, settings);
        }


        private Task<PostWorklogResult> Post(string comment)
        {
            return service.PostWorklogAsync("TST-1", StartTime, Elapsed, comment, EstimateUpdateMethods.Auto, null);
        }


        #region WorklogCommentSetting rules
        [Test]
        public async Task WorklogOnly_NeverPostsToTheCommentTrack()
        {
            settings.PostWorklogComment = WorklogCommentSetting.WorklogOnly;

            PostWorklogResult result = await Post("some note");

            jira.Verify(j => j.PostComment(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            Assert.That(result.CommentPosted, Is.False);
            Assert.That(result.Success, Is.True);
        }


        [Test]
        public async Task WorklogOnly_CarriesTheCommentOnTheWorklog()
        {
            settings.PostWorklogComment = WorklogCommentSetting.WorklogOnly;

            await Post("some note");

            jira.Verify(j => j.PostWorklog("TST-1", StartTime, Elapsed, "some note", EstimateUpdateMethods.Auto, null), Times.Once);
        }


        [Test]
        public async Task CommentOnly_PostsTheCommentAndClearsItFromTheWorklog()
        {
            settings.PostWorklogComment = WorklogCommentSetting.CommentOnly;

            PostWorklogResult result = await Post("some note");

            jira.Verify(j => j.PostComment("TST-1", "some note"), Times.Once);
            jira.Verify(j => j.PostWorklog("TST-1", StartTime, Elapsed, "", EstimateUpdateMethods.Auto, null), Times.Once);
            Assert.That(result.CommentPosted, Is.True);
            Assert.That(result.WorklogPosted, Is.True);
        }


        [Test]
        public async Task WorklogAndComment_PostsTheCommentInBothPlaces()
        {
            settings.PostWorklogComment = WorklogCommentSetting.WorklogAndComment;

            await Post("some note");

            jira.Verify(j => j.PostComment("TST-1", "some note"), Times.Once);
            jira.Verify(j => j.PostWorklog("TST-1", StartTime, Elapsed, "some note", EstimateUpdateMethods.Auto, null), Times.Once);
        }


        [Test]
        public async Task AnEmptyCommentIsNotPostedToTheCommentTrack()
        {
            settings.PostWorklogComment = WorklogCommentSetting.CommentOnly;

            await Post("");

            jira.Verify(j => j.PostComment(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            jira.Verify(j => j.PostWorklog("TST-1", StartTime, Elapsed, "", EstimateUpdateMethods.Auto, null), Times.Once);
        }


        [Test]
        public async Task ANullCommentIsNotPostedToTheCommentTrack()
        {
            settings.PostWorklogComment = WorklogCommentSetting.WorklogAndComment;

            await Post(null);

            jira.Verify(j => j.PostComment(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }


        [Test]
        public async Task TheEstimateIsPassedThroughToTheWorklog()
        {
            settings.PostWorklogComment = WorklogCommentSetting.WorklogOnly;

            await service.PostWorklogAsync("TST-1", StartTime, Elapsed, null, EstimateUpdateMethods.SetTo, "4h");

            jira.Verify(j => j.PostWorklog("TST-1", StartTime, Elapsed, null, EstimateUpdateMethods.SetTo, "4h"), Times.Once);
        }
        #endregion


        #region order, and failure aborting the rest
        [Test]
        public async Task TheCommentIsPostedBeforeTheWorklog()
        {
            settings.PostWorklogComment = WorklogCommentSetting.WorklogAndComment;

            await Post("some note");

            Assert.That(calls, Is.EqualTo(new List<string> { "comment", "worklog" }));
        }


        [Test]
        public async Task AFailedCommentStopsTheWorklog()
        {
            settings.PostWorklogComment = WorklogCommentSetting.WorklogAndComment;
            jira.Setup(j => j.PostComment(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            PostWorklogResult result = await Post("some note");

            jira.Verify(j => j.PostWorklog(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()), Times.Never);
            Assert.That(result.CommentPosted, Is.False);
            Assert.That(result.WorklogPosted, Is.False);
        }


        [Test]
        public async Task AFailedCommentStopsTheReset()
        {
            settings.PostWorklogComment = WorklogCommentSetting.WorklogAndComment;
            jira.Setup(j => j.PostComment(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            PostWorklogResult result = await Post("some note");

            Assert.That(result.Success, Is.False, "a reset here would lose time that was never recorded");
        }


        [Test]
        public async Task AFailedWorklogStopsTheReset()
        {
            settings.PostWorklogComment = WorklogCommentSetting.WorklogOnly;
            jira.Setup(j => j.PostWorklog(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>())).Returns(false);

            PostWorklogResult result = await Post("some note");

            Assert.That(result.Success, Is.False);
        }


        [Test]
        public async Task EverythingPostedAllowsTheReset()
        {
            settings.PostWorklogComment = WorklogCommentSetting.WorklogAndComment;

            PostWorklogResult result = await Post("some note");

            Assert.That(result.Success, Is.True);
        }
        #endregion


        #region summary lookup
        [Test]
        public async Task GetSummary_ReturnsWhatJiraSays()
        {
            jira.Setup(j => j.GetIssueSummary("TST-1", false)).Returns("Do the thing");

            string summary = await service.GetSummaryAsync("TST-1");

            Assert.That(summary, Is.EqualTo("Do the thing"));
        }


        [Test]
        public async Task GetSummary_PassesTheProjectNameSetting()
        {
            settings.IncludeProjectName = true;
            jira.Setup(j => j.GetIssueSummary("TST-1", true)).Returns("Project / Do the thing");

            string summary = await service.GetSummaryAsync("TST-1");

            Assert.That(summary, Is.EqualTo("Project / Do the thing"));
        }


        [Test]
        public async Task GetSummary_ReturnsNullWhenJiraRefusesTheRequest()
        {
            jira.Setup(j => j.GetIssueSummary(It.IsAny<string>(), It.IsAny<bool>())).Throws(new RequestDeniedException());

            string summary = await service.GetSummaryAsync("TST-1");

            Assert.That(summary, Is.Null, "null tells the caller to keep the summary it has");
        }


        [Test]
        public async Task GetSummary_ReturnsEmptyOnAnInvalidSession()
        {
            jira.SetupGet(j => j.SessionValid).Returns(false);

            string summary = await service.GetSummaryAsync("TST-1");

            Assert.That(summary, Is.EqualTo(""));
            jira.Verify(j => j.GetIssueSummary(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }


        [Test]
        public async Task GetSummary_ReturnsEmptyOnAnEmptyKey()
        {
            Assert.That(await service.GetSummaryAsync(""), Is.EqualTo(""));
            Assert.That(await service.GetSummaryAsync(null), Is.EqualTo(""));

            jira.Verify(j => j.GetIssueSummary(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }
        #endregion


        #region remaining estimate lookup
        [Test]
        public async Task GetRemainingEstimate_ReturnsWhatJiraSays()
        {
            jira.Setup(j => j.GetIssueTimetracking("TST-1")).Returns(new TimetrackingFields
            {
                RemainingEstimate = "2h 30m",
                RemainingEstimateSeconds = 9000
            });

            RemainingEstimate estimate = await service.GetRemainingEstimateAsync("TST-1");

            Assert.That(estimate.Text, Is.EqualTo("2h 30m"));
            Assert.That(estimate.Seconds, Is.EqualTo(9000));
        }


        [Test]
        public async Task GetRemainingEstimate_ReturnsNoneWhenTheIssueHasNoTimeTracking()
        {
            jira.Setup(j => j.GetIssueTimetracking("TST-1")).Returns((TimetrackingFields)null);

            RemainingEstimate estimate = await service.GetRemainingEstimateAsync("TST-1");

            Assert.That(estimate.Seconds, Is.EqualTo(-1));
            Assert.That(estimate.Text, Is.EqualTo(""));
        }


        [Test]
        public async Task GetRemainingEstimate_ReturnsNoneOnAnInvalidSession()
        {
            jira.SetupGet(j => j.SessionValid).Returns(false);

            RemainingEstimate estimate = await service.GetRemainingEstimateAsync("TST-1");

            Assert.That(estimate.Seconds, Is.EqualTo(-1));
            jira.Verify(j => j.GetIssueTimetracking(It.IsAny<string>()), Times.Never);
        }


        [Test]
        public async Task GetRemainingEstimate_ReturnsNoneOnAnEmptyKey()
        {
            Assert.That((await service.GetRemainingEstimateAsync("")).Seconds, Is.EqualTo(-1));
            Assert.That((await service.GetRemainingEstimateAsync(null)).Seconds, Is.EqualTo(-1));

            jira.Verify(j => j.GetIssueTimetracking(It.IsAny<string>()), Times.Never);
        }
        #endregion


        #region construction
        [Test]
        public void ItRefusesToBeBuiltWithoutItsCollaborators()
        {
            Assert.Throws<ArgumentNullException>(() => new IssueJiraService(null, settings));
            Assert.Throws<ArgumentNullException>(() => new IssueJiraService(jira.Object, null));
        }
        #endregion
    }
}
