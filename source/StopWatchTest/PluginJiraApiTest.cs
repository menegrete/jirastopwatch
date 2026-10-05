namespace StopWatchTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Moq;
    using NUnit.Framework;
    using StopWatch;
    using StopWatch.Plugin;
    using StopWatch.Plugins;


    [TestFixture]
    public class PluginJiraApiTest
    {
        private Mock<IJiraOperations> jira;
        private PluginJiraApi api;
        private List<string> logged;

        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void Setup()
        {
            logged = new List<string>();
            jira = new Mock<IJiraOperations>();
            jira.SetupGet(j => j.SessionValid).Returns(true);

            api = new PluginJiraApi(new IssueJiraService(jira.Object, new Settings()), jira.Object, (message, ex) => logged.Add(message));
        }


        private static JiraIssueInfo Subtask(string key, string summary)
        {
            return new JiraIssueInfo { Key = key, Summary = summary, IsSubtask = true };
        }


        #region worklog with estimate
        [TestCase(PluginEstimateUpdate.Auto, EstimateUpdateMethods.Auto)]
        [TestCase(PluginEstimateUpdate.Leave, EstimateUpdateMethods.Leave)]
        [TestCase(PluginEstimateUpdate.SetTo, EstimateUpdateMethods.SetTo)]
        [TestCase(PluginEstimateUpdate.ManualDecrease, EstimateUpdateMethods.ManualDecrease)]
        public async Task AddWorklog_WithEstimate_SendsThatEstimateToJira(PluginEstimateUpdate asked, EstimateUpdateMethods expected)
        {
            jira.Setup(j => j.PostWorklog("TST-1", Start, TimeSpan.FromMinutes(5), "c", expected, "4h")).Returns(JiraResult<string>.Ok("1"));

            bool added = await api.AddWorklogAsync("TST-1", Start, TimeSpan.FromMinutes(5), "c", asked, "4h");

            Assert.That(added, Is.True);
            jira.VerifyAll();
        }


        [Test]
        public async Task AddWorklog_WithoutEstimate_UsesAuto()
        {
            jira.Setup(j => j.PostWorklog("TST-1", Start, TimeSpan.FromMinutes(5), "c", EstimateUpdateMethods.Auto, "")).Returns(JiraResult<string>.Ok("1"));

            Assert.That(await api.AddWorklogAsync("TST-1", Start, TimeSpan.FromMinutes(5), "c"), Is.True);
            jira.VerifyAll();
        }


        [Test]
        public async Task AddWorklog_JiraRefuses_ReturnsFalse()
        {
            jira.Setup(j => j.PostWorklog(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()))
                .Returns(JiraResult<string>.Fail(JiraFailureReason.Validation, "closed"));

            Assert.That(await api.AddWorklogAsync("TST-1", Start, TimeSpan.FromMinutes(5), "c", PluginEstimateUpdate.Auto, null), Is.False);
        }
        #endregion


        #region subtasks
        [Test]
        public async Task GetSubtasks_ReturnsPublicDescriptions()
        {
            jira.Setup(j => j.GetSubtasks("TST-1")).Returns(JiraResult<IReadOnlyList<JiraIssueInfo>>.Ok(new[] { Subtask("TST-2", "One"), Subtask("TST-3", "Two") }));

            IReadOnlyList<PluginSubtask> result = await api.GetSubtasksAsync("TST-1");

            Assert.That(result.Select(s => s.Key + ":" + s.Summary), Is.EqualTo(new[] { "TST-2:One", "TST-3:Two" }));
        }


        [Test]
        public async Task GetSubtasks_JiraRefuses_ReturnsNull()
        {
            jira.Setup(j => j.GetSubtasks("TST-1")).Returns(JiraResult<IReadOnlyList<JiraIssueInfo>>.Fail(JiraFailureReason.NotFound, "no"));

            Assert.That(await api.GetSubtasksAsync("TST-1"), Is.Null);
        }


        [Test]
        public async Task GetSubtasks_ThrowingClient_ReturnsNullAndLogs()
        {
            jira.Setup(j => j.GetSubtasks("TST-1")).Throws(new InvalidOperationException("boom"));

            Assert.That(await api.GetSubtasksAsync("TST-1"), Is.Null);
            Assert.That(logged, Has.Some.Contain("boom"));
        }


        [Test]
        public async Task CreateSubtask_UsesTheProjectsFirstSubtaskType()
        {
            jira.Setup(j => j.GetSubtaskTypes("TST")).Returns(JiraResult<IReadOnlyList<JiraIssueType>>.Ok(new[]
            {
                new JiraIssueType { Id = "10", Name = "Sub-task" },
                new JiraIssueType { Id = "11", Name = "Other" }
            }));
            jira.Setup(j => j.CreateSubtask("TST-1", "Part", "10")).Returns(JiraResult<string>.Ok("TST-9"));

            Assert.That(await api.CreateSubtaskAsync("TST-1", "Part"), Is.EqualTo("TST-9"));
        }


        [Test]
        public async Task CreateSubtask_ProjectOffersNoSubtaskType_ReturnsNullWithoutCreating()
        {
            jira.Setup(j => j.GetSubtaskTypes("TST")).Returns(JiraResult<IReadOnlyList<JiraIssueType>>.Ok(new JiraIssueType[0]));

            Assert.That(await api.CreateSubtaskAsync("TST-1", "Part"), Is.Null);
            jira.Verify(j => j.CreateSubtask(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }


        [Test]
        public async Task CreateSubtask_JiraRefuses_ReturnsNull()
        {
            jira.Setup(j => j.GetSubtaskTypes("TST")).Returns(JiraResult<IReadOnlyList<JiraIssueType>>.Ok(new[] { new JiraIssueType { Id = "10" } }));
            jira.Setup(j => j.CreateSubtask(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(JiraResult<string>.Fail(JiraFailureReason.Validation, "no"));

            Assert.That(await api.CreateSubtaskAsync("TST-1", "Part"), Is.Null);
        }


        [Test]
        public async Task CreateSubtask_ThrowingClient_ReturnsNullAndLogs()
        {
            jira.Setup(j => j.GetSubtaskTypes("TST")).Throws(new InvalidOperationException("boom"));

            Assert.That(await api.CreateSubtaskAsync("TST-1", "Part"), Is.Null);
            Assert.That(logged, Has.Some.Contain("boom"));
        }


        [TestCase(null)]
        [TestCase("")]
        [TestCase("nokey")]
        public async Task CreateSubtask_ParentWithoutAProjectKey_ReturnsNull(string parent)
        {
            Assert.That(await api.CreateSubtaskAsync(parent, "Part"), Is.Null);
        }
        #endregion
    }
}
