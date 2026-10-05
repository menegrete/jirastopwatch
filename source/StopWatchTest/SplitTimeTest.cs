namespace StopWatchTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Moq;
    using NUnit.Framework;
    using SplitTime;
    using StopWatch;
    using StopWatch.Plugin;


    [TestFixture]
    public class SplitMathTest
    {
        [TestCase(7, 3, new[] { 3, 2, 2 })]
        [TestCase(6, 3, new[] { 2, 2, 2 })]
        [TestCase(1, 3, new[] { 1, 0, 0 })]
        [TestCase(5, 1, new[] { 5 })]
        [TestCase(0, 3, new[] { 0, 0, 0 })]
        [TestCase(10, 4, new[] { 3, 3, 2, 2 })]
        public void Distribute_ConservesTheSumAndGivesTheRemainderToTheFirstShares(int total, int parts, int[] expected)
        {
            int[] shares = SplitMath.Distribute(total, parts);

            Assert.That(shares, Is.EqualTo(expected));
            Assert.That(shares.Sum(), Is.EqualTo(total));
        }


        [Test]
        public void Distribute_RejectsNoPartsOrANegativeTotal()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SplitMath.Distribute(5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SplitMath.Distribute(-1, 2));
        }
    }


    /// <summary>
    /// The SplitTime sample run through the host's real pipeline, with a fake
    /// Jira: every way the contract can end, seen from the host's side.
    /// </summary>
    [TestFixture]
    public class SplitTimeTest
    {
        private string directory;
        private Mock<IJiraApi> jira;
        private Mock<IWorklogPoster> original;
        private TimeLoadRegistry registry;
        private TimeLoadPipeline pipeline;
        private FakePrompt prompt;
        private List<string> told;
        private List<string> noticed;
        private List<string> worklogs;
        private int created;
        private bool failNextWorklog;
        private bool failCreation;

        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan Total = TimeSpan.FromMinutes(7);

        private class FakePrompt : ISplitPrompt
        {
            public int Asked;
            public SplitPlan Plan;

            public SplitPlan Ask(string issueKey, IReadOnlyList<PluginSubtask> subtasks, int totalMinutes)
            {
                Asked++;
                return Plan;
            }
        }

        private class Notices : ITimeLoadNotifier
        {
            private readonly List<string> into;
            public Notices(List<string> into) { this.into = into; }
            public void Info(string message) { into.Add("info: " + message); }
            public void Warning(string message) { into.Add("warning: " + message); }
        }


        [SetUp]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "SplitTimeTest-" + Guid.NewGuid().ToString("N"));
            told = new List<string>();
            noticed = new List<string>();
            worklogs = new List<string>();
            created = 0;
            failNextWorklog = false;
            failCreation = false;

            jira = new Mock<IJiraApi>();
            jira.Setup(j => j.GetSubtasksAsync("TST-1")).ReturnsAsync(new[]
            {
                new PluginSubtask("TST-2", "One"),
                new PluginSubtask("TST-3", "Two"),
                new PluginSubtask("TST-4", "Three")
            });
            jira.Setup(j => j.AddWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<PluginEstimateUpdate>(), It.IsAny<string>()))
                .Returns((string key, DateTimeOffset start, TimeSpan span, string comment, PluginEstimateUpdate est, string value) =>
                {
                    if (failNextWorklog)
                    {
                        failNextWorklog = false;
                        return Task.FromResult(false);
                    }
                    worklogs.Add($"{key}@{(start - Start).TotalMinutes}+{span.TotalMinutes}:{comment}:{est}:{value}");
                    return Task.FromResult(true);
                });
            jira.Setup(j => j.CreateSubtaskAsync("TST-1", It.IsAny<string>()))
                .Returns(() => Task.FromResult(failCreation ? null : "TST-" + (50 + ++created)));

            original = new Mock<IWorklogPoster>();
            original.Setup(o => o.PostWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()))
                .ReturnsAsync(new PostWorklogResult { Success = true });

            prompt = new FakePrompt { Plan = EvenPlan() };

            var handler = new SplitTimeHandler(prompt, new SplitLedger(directory), m => { }, told.Add);
            registry = new TimeLoadRegistry();
            registry.RegisterInsteadOf("SplitTime", handler.HandleAsync);

            pipeline = new TimeLoadPipeline(original.Object, registry, jira.Object, (m, e) => { });
            pipeline.Notifier = new Notices(noticed);
        }


        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }


        private static SplitPlan EvenPlan()
        {
            return new SplitPlan
            {
                Parts =
                {
                    new SplitPart { ExistingKey = "TST-2", Minutes = 3 },
                    new SplitPart { ExistingKey = "TST-3", Minutes = 2 },
                    new SplitPart { ExistingKey = "TST-4", Minutes = 2 }
                }
            };
        }

        private static TimeLoadInput Input(TimeLoadSource source = null)
        {
            return new TimeLoadInput
            {
                IssueKey = "TST-1",
                StartTime = Start,
                TimeElapsed = Total,
                Comment = "work",
                EstimateUpdateMethod = EstimateUpdateMethods.Leave,
                EstimateUpdateValue = "",
                Source = source ?? TimeLoadSource.User
            };
        }

        private Task<TimeLoadPipelineResult> Load(TimeLoadSource source = null)
        {
            return pipeline.LoadAsync(Input(source));
        }

        private void VerifyStandardLoad(Times times)
        {
            original.Verify(o => o.PostWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()), times);
        }


        #region the split
        [Test]
        public async Task ASuccessfulSplit_WritesEachShareInSequenceAndTheHostResetsTheTimer()
        {
            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(result.HandledBy, Is.EqualTo("SplitTime"));
            Assert.That(result.WritesMade, Is.EqualTo(3));
            Assert.That(worklogs, Is.EqualTo(new[]
            {
                "TST-2@0+3:work:Leave:",
                "TST-3@3+2:work:Leave:",
                "TST-4@5+2:work:Leave:"
            }));
            VerifyStandardLoad(Times.Never());
            Assert.That(noticed, Is.Empty);
        }


        [Test]
        public async Task ASplitCanCreateANewSubtaskForPartOfTheTime()
        {
            prompt.Plan = new SplitPlan
            {
                Parts =
                {
                    new SplitPart { ExistingKey = "TST-2", Minutes = 4 },
                    new SplitPart { NewSummary = "Review", Minutes = 3 }
                }
            };

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(result.WritesMade, Is.EqualTo(3));
            Assert.That(worklogs, Is.EqualTo(new[] { "TST-2@0+4:work:Leave:", "TST-51@4+3:work:Leave:" }));
        }


        [Test]
        public async Task AnIssueWithoutSubtasks_IsDeclinedAndTheHostLoadsIt()
        {
            jira.Setup(j => j.GetSubtasksAsync("TST-1")).ReturnsAsync(new PluginSubtask[0]);

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(result.HandledBy, Is.Null);
            Assert.That(prompt.Asked, Is.EqualTo(0));
            VerifyStandardLoad(Times.Once());
        }


        [Test]
        public async Task ALoadFromAnotherPlugin_IsDeclined()
        {
            TimeLoadPipelineResult result = await Load(TimeLoadSource.FromPlugin("Importer"));

            Assert.That(prompt.Asked, Is.EqualTo(0));
            VerifyStandardLoad(Times.Once());
            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
        }
        #endregion


        #region cancelling
        [Test]
        public async Task ClosingTheDialog_CancelsWithoutLoadingAnything()
        {
            prompt.Plan = null;

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Cancelled));
            Assert.That(worklogs, Is.Empty);
            VerifyStandardLoad(Times.Never());
            Assert.That(noticed, Is.Empty);
        }
        #endregion


        #region failing before writing
        [Test]
        public async Task NotBeingAbleToReadTheSubtasks_FallsBackToTheStandardLoadWithANotice()
        {
            jira.Setup(j => j.GetSubtasksAsync("TST-1")).ReturnsAsync((IReadOnlyList<PluginSubtask>)null);

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(result.HandledBy, Is.Null);
            VerifyStandardLoad(Times.Once());
            Assert.That(noticed, Has.Count.EqualTo(1));
            Assert.That(noticed[0], Does.StartWith("info:"));
        }


        [Test]
        public async Task TheFirstWriteFailing_FallsBackAndLeavesNothingToResume()
        {
            failNextWorklog = true;

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            VerifyStandardLoad(Times.Once());
            Assert.That(worklogs, Is.Empty);

            // The plan is gone, so a later load of the same time asks again.
            await Load();
            Assert.That(prompt.Asked, Is.EqualTo(2));
        }


        [Test]
        public async Task ASplitThatDoesNotAddUp_FailsBeforeWriting()
        {
            prompt.Plan = new SplitPlan { Parts = { new SplitPart { ExistingKey = "TST-2", Minutes = 3 } } };

            TimeLoadPipelineResult result = await Load();

            Assert.That(worklogs, Is.Empty);
            VerifyStandardLoad(Times.Once());
            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
        }
        #endregion


        #region failing halfway, and trying again
        [Test]
        public async Task AFailureAfterWriting_DoesNotFallBackAndTheHostWarns()
        {
            int calls = 0;
            jira.Setup(j => j.AddWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<PluginEstimateUpdate>(), It.IsAny<string>()))
                .Returns((string key, DateTimeOffset start, TimeSpan span, string comment, PluginEstimateUpdate est, string value) =>
                {
                    calls++;
                    if (calls == 2)
                        return Task.FromResult(false);
                    worklogs.Add(key);
                    return Task.FromResult(true);
                });

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            Assert.That(result.WritesMade, Is.EqualTo(1));
            Assert.That(worklogs, Is.EqualTo(new[] { "TST-2" }));
            VerifyStandardLoad(Times.Never());
            Assert.That(noticed, Has.Count.EqualTo(1));
            Assert.That(noticed[0], Does.StartWith("warning:"));
        }


        [Test]
        public async Task ARetryResumesTheSameSplit_WritingOnlyWhatIsMissingAndNeverAskingAgain()
        {
            prompt.Plan = new SplitPlan
            {
                Parts =
                {
                    new SplitPart { ExistingKey = "TST-2", Minutes = 4 },
                    new SplitPart { NewSummary = "Review", Minutes = 3 }
                }
            };

            // First attempt: the subtask is created, its worklog is refused.
            int posts = 0;
            jira.Setup(j => j.AddWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<PluginEstimateUpdate>(), It.IsAny<string>()))
                .Returns((string key, DateTimeOffset start, TimeSpan span, string comment, PluginEstimateUpdate est, string value) =>
                {
                    posts++;
                    if (posts == 2)
                        return Task.FromResult(false);
                    worklogs.Add(key);
                    return Task.FromResult(true);
                });

            TimeLoadPipelineResult first = await Load();
            Assert.That(first.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            Assert.That(created, Is.EqualTo(1));

            TimeLoadPipelineResult second = await Load();

            Assert.That(second.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(prompt.Asked, Is.EqualTo(1));
            Assert.That(created, Is.EqualTo(1), "the subtask must be reused, not created twice");
            Assert.That(worklogs, Is.EqualTo(new[] { "TST-2", "TST-51" }));
            VerifyStandardLoad(Times.Never());
        }


        [Test]
        public async Task ARetryThatWritesNothingNew_CancelsInsteadOfLettingTheHostLoadTheWholeTimeAgain()
        {
            prompt.Plan = new SplitPlan
            {
                Parts =
                {
                    new SplitPart { ExistingKey = "TST-2", Minutes = 4 },
                    new SplitPart { ExistingKey = "TST-3", Minutes = 3 }
                }
            };

            int posts = 0;
            jira.Setup(j => j.AddWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<PluginEstimateUpdate>(), It.IsAny<string>()))
                .Returns((string key, DateTimeOffset start, TimeSpan span, string comment, PluginEstimateUpdate est, string value) =>
                {
                    posts++;
                    return Task.FromResult(posts == 1);
                });

            await Load();                          // TST-2 written, TST-3 refused
            TimeLoadPipelineResult retry = await Load();   // TST-3 refused again, nothing new written

            Assert.That(retry.Outcome, Is.EqualTo(TimeLoadOutcome.Cancelled));
            VerifyStandardLoad(Times.Never());
            Assert.That(told, Has.Count.EqualTo(1));
            Assert.That(told[0], Does.Contain("already loaded"));
        }
        #endregion
    }
}
