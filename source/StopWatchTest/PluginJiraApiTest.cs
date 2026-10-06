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
        public async Task GetSubtasks_CarriesTheIssueTypeName()
        {
            var typed = Subtask("TST-2", "One");
            typed.IssueTypeName = "Task A";
            jira.Setup(j => j.GetSubtasks("TST-1")).Returns(JiraResult<IReadOnlyList<JiraIssueInfo>>.Ok(new[] { typed, Subtask("TST-3", "Two") }));

            IReadOnlyList<PluginSubtask> result = await api.GetSubtasksAsync("TST-1");

            Assert.That(result.Select(s => s.IssueType), Is.EqualTo(new[] { "Task A", "" }));
        }


        [Test]
        public void PluginSubtask_WithoutAType_HasAnEmptyIssueType()
        {
            Assert.That(new PluginSubtask("TST-2", "One").IssueType, Is.EqualTo(""));
            Assert.That(new PluginSubtask("TST-2", "One", null).IssueType, Is.EqualTo(""));
            Assert.That(new PluginSubtask("TST-2", "One", "Task A").IssueType, Is.EqualTo("Task A"));
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


        private void ProjectOffersTypes()
        {
            jira.Setup(j => j.GetSubtaskTypes("TST")).Returns(JiraResult<IReadOnlyList<JiraIssueType>>.Ok(new[]
            {
                new JiraIssueType { Id = "10", Name = "Sub-task" },
                new JiraIssueType { Id = "11", Name = "Task A" }
            }));
        }


        [Test]
        public async Task CreateSubtask_KnownType_CreatesItWithThatType()
        {
            ProjectOffersTypes();
            jira.Setup(j => j.CreateSubtask("TST-1", "Part", "11")).Returns(JiraResult<string>.Ok("TST-9"));

            Assert.That(await api.CreateSubtaskAsync("TST-1", "Part", "Task A"), Is.EqualTo("TST-9"));
        }


        [TestCase("task a")]
        [TestCase("TASK A")]
        [TestCase("  Task A ")]
        public async Task CreateSubtask_TypeNameIgnoresCaseAndSurroundingWhitespace(string name)
        {
            ProjectOffersTypes();
            jira.Setup(j => j.CreateSubtask("TST-1", "Part", "11")).Returns(JiraResult<string>.Ok("TST-9"));

            Assert.That(await api.CreateSubtaskAsync("TST-1", "Part", name), Is.EqualTo("TST-9"));
        }


        [Test]
        public async Task CreateSubtask_UnknownType_ReturnsNullCreatesNothingAndLogsWhy()
        {
            ProjectOffersTypes();

            Assert.That(await api.CreateSubtaskAsync("TST-1", "Part", "Nope"), Is.Null);

            jira.Verify(j => j.CreateSubtask(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            Assert.That(logged, Has.Some.Contain("Nope").And.Contain("Sub-task").And.Contain("Task A"));
        }


        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public async Task CreateSubtask_NoTypeName_BehavesLikeTheTwoArgumentOverload(string name)
        {
            ProjectOffersTypes();
            jira.Setup(j => j.CreateSubtask("TST-1", "Part", "10")).Returns(JiraResult<string>.Ok("TST-9"));

            Assert.That(await api.CreateSubtaskAsync("TST-1", "Part", name), Is.EqualTo("TST-9"));
            Assert.That(await api.CreateSubtaskAsync("TST-1", "Part"), Is.EqualTo("TST-9"));
            jira.Verify(j => j.CreateSubtask("TST-1", "Part", "10"), Times.Exactly(2));
        }


        [Test]
        public async Task CreateSubtask_NamedTypeAndNoSubtaskTypes_ReturnsNull()
        {
            jira.Setup(j => j.GetSubtaskTypes("TST")).Returns(JiraResult<IReadOnlyList<JiraIssueType>>.Ok(new JiraIssueType[0]));

            Assert.That(await api.CreateSubtaskAsync("TST-1", "Part", "Task A"), Is.Null);
            jira.Verify(j => j.CreateSubtask(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
            Assert.That(await api.CreateSubtaskAsync(parent, "Part", "Task A"), Is.Null);
        }
        #endregion


        #region subtask types
        [Test]
        public async Task GetSubtaskTypes_ReturnsTheNames()
        {
            ProjectOffersTypes();

            Assert.That(await api.GetSubtaskTypesAsync("TST"), Is.EqualTo(new[] { "Sub-task", "Task A" }));
        }


        [Test]
        public async Task GetSubtaskTypes_ProjectWithoutThem_ReturnsAnEmptyList()
        {
            jira.Setup(j => j.GetSubtaskTypes("TST")).Returns(JiraResult<IReadOnlyList<JiraIssueType>>.Ok(new JiraIssueType[0]));

            IReadOnlyList<string> result = await api.GetSubtaskTypesAsync("TST");

            Assert.That(result, Is.Not.Null.And.Empty);
        }


        [Test]
        public async Task GetSubtaskTypes_JiraRefuses_ReturnsNull()
        {
            jira.Setup(j => j.GetSubtaskTypes("NOPE")).Returns(JiraResult<IReadOnlyList<JiraIssueType>>.Fail(JiraFailureReason.NotFound, "no"));

            Assert.That(await api.GetSubtaskTypesAsync("NOPE"), Is.Null);
        }


        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public async Task GetSubtaskTypes_BlankProjectKey_ReturnsNullWithoutAsking(string project)
        {
            Assert.That(await api.GetSubtaskTypesAsync(project), Is.Null);
            jira.Verify(j => j.GetSubtaskTypes(It.IsAny<string>()), Times.Never);
        }


        [Test]
        public async Task GetSubtaskTypes_NoSession_ReturnsNull()
        {
            jira.SetupGet(j => j.SessionValid).Returns(false);

            Assert.That(await api.GetSubtaskTypesAsync("TST"), Is.Null);
        }


        [Test]
        public async Task GetSubtaskTypes_ThrowingClient_ReturnsNullAndLogs()
        {
            jira.Setup(j => j.GetSubtaskTypes("TST")).Throws(new InvalidOperationException("boom"));

            Assert.That(await api.GetSubtaskTypesAsync("TST"), Is.Null);
            Assert.That(logged, Has.Some.Contain("boom"));
        }
        #endregion
    }
}
