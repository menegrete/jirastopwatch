namespace StopWatchTest
{
    using System;
    using System.Collections.Generic;
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
    /// The SplitTime sample run through the host's real pipeline and the rule
    /// for what the timer holds, with a fake Jira: every way the contract can
    /// end, seen from the host's side.
    /// </summary>
    [TestFixture]
    public class SplitTimeTest
    {
        private Mock<IJiraApi> jira;
        private Mock<IWorklogPoster> original;
        private TimeLoadRegistry registry;
        private TimeLoadPipeline pipeline;
        private FakePrompt prompt;
        private List<string> noticed;
        private List<string> worklogs;
        private int created;
        private int worklogCalls;
        private int failWorklogAt;
        private List<TimeSpan> originalAskedFor;

        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan Total = TimeSpan.FromMinutes(7);

        private class FakePrompt : ISplitPrompt
        {
            public int Asked;
            public int LastTotal;
            public Func<int, SplitPlan> Plan;

            public SplitPlan Ask(string issueKey, IReadOnlyList<PluginSubtask> subtasks, int totalMinutes)
            {
                Asked++;
                LastTotal = totalMinutes;
                return Plan?.Invoke(totalMinutes);
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
            noticed = new List<string>();
            worklogs = new List<string>();
            originalAskedFor = new List<TimeSpan>();
            created = 0;
            worklogCalls = 0;
            failWorklogAt = 0;

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
                    worklogCalls++;
                    if (failWorklogAt == worklogCalls)
                        return Task.FromResult(false);
                    worklogs.Add($"{key}@{(start - Start).TotalMinutes}+{span.TotalMinutes}:{comment}:{est}:{value}");
                    return Task.FromResult(true);
                });
            jira.Setup(j => j.CreateSubtaskAsync("TST-1", It.IsAny<string>()))
                .Returns(() => Task.FromResult("TST-" + (50 + ++created)));

            original = new Mock<IWorklogPoster>();
            original.Setup(o => o.PostWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()))
                .Callback<string, DateTimeOffset, TimeSpan, string, EstimateUpdateMethods, string>((k, s, t, c, e, v) => originalAskedFor.Add(t))
                .ReturnsAsync(new PostWorklogResult { Success = true });

            prompt = new FakePrompt { Plan = EvenPlan };

            var handler = new SplitTimeHandler(prompt, m => { });
            registry = new TimeLoadRegistry();
            registry.RegisterInsteadOf("SplitTime", handler.HandleAsync);

            pipeline = new TimeLoadPipeline(original.Object, registry, jira.Object, (m, e) => { });
            pipeline.Notifier = new Notices(noticed);
        }


        private static SplitPlan EvenPlan(int total)
        {
            int[] shares = SplitMath.Distribute(total, 3);
            return new SplitPlan
            {
                Parts =
                {
                    new SplitPart { ExistingKey = "TST-2", Minutes = shares[0] },
                    new SplitPart { ExistingKey = "TST-3", Minutes = shares[1] },
                    new SplitPart { ExistingKey = "TST-4", Minutes = shares[2] }
                }
            };
        }

        private static TimeLoadInput Input(TimeSpan total, TimeLoadSource source = null)
        {
            return new TimeLoadInput
            {
                IssueKey = "TST-1",
                StartTime = Start,
                TimeElapsed = total,
                Comment = "work",
                EstimateUpdateMethod = EstimateUpdateMethods.Leave,
                EstimateUpdateValue = "",
                Source = source ?? TimeLoadSource.User
            };
        }

        private Task<TimeLoadPipelineResult> Load(TimeLoadSource source = null)
        {
            return pipeline.LoadAsync(Input(Total, source));
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
            prompt.Plan = total => new SplitPlan
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
            prompt.Plan = total => null;

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
        public async Task TheFirstWriteFailing_FallsBackToTheStandardLoad()
        {
            failWorklogAt = 1;

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(worklogs, Is.Empty);
            VerifyStandardLoad(Times.Once());
        }


        [Test]
        public async Task ASplitThatDoesNotAddUp_FailsBeforeWriting()
        {
            prompt.Plan = total => new SplitPlan { Parts = { new SplitPart { ExistingKey = "TST-2", Minutes = 3 } } };

            TimeLoadPipelineResult result = await Load();

            Assert.That(worklogs, Is.Empty);
            VerifyStandardLoad(Times.Once());
            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
        }
        #endregion


        #region failing halfway, and trying again
        [Test]
        public async Task AFailureAfterWriting_DoesNotFallBackAndTheHostMeasuresWhatReachedJira()
        {
            failWorklogAt = 2;

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            Assert.That(result.WritesMade, Is.EqualTo(1));
            Assert.That(result.TimeLoaded, Is.EqualTo(TimeSpan.FromMinutes(3)));
            Assert.That(worklogs, Has.Count.EqualTo(1));
            VerifyStandardLoad(Times.Never());
            Assert.That(noticed, Has.Count.EqualTo(1));
            Assert.That(noticed[0], Does.StartWith("warning:").And.Contain("3 min loaded").And.Contain("keeps the 4 min"));
        }


        /// <summary>Posts from a timer the way the main window does, and applies the timer rule to the result.</summary>
        private async Task<TimeLoadPipelineResult> LoadFrom(WatchTimer timer)
        {
            var input = Input(timer.TimeElapsedNearestMinute);
            input.CurrentElapsed = () => timer.TimeElapsedNearestMinute;

            TimeLoadPipelineResult result = await pipeline.LoadAsync(input);
            TimerAfterLoad.Apply(result, () => timer.TimeElapsed, elapsed => timer.TimeElapsed = elapsed, timer.Reset);
            return result;
        }


        private static WatchTimer TimerHolding(int minutes)
        {
            var timer = new WatchTimer();
            timer.SetState(new TimerState { Running = false, TotalTime = TimeSpan.FromMinutes(minutes), InitialStartTime = Start });
            return timer;
        }


        [Test]
        public async Task ARetryAfterAPartialLoad_SplitsOnlyWhatRemains()
        {
            WatchTimer timer = TimerHolding(7);

            failWorklogAt = 2;
            await LoadFrom(timer);
            Assert.That(timer.TimeElapsed, Is.EqualTo(TimeSpan.FromMinutes(4)), "3 of 7 minutes reached Jira");

            // The user posts again: the dialog is offered the 4 that remain.
            TimeLoadPipelineResult retry = await LoadFrom(timer);

            Assert.That(retry.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(prompt.LastTotal, Is.EqualTo(4));
            Assert.That(timer.TimeElapsed, Is.EqualTo(TimeSpan.Zero), "the timer is reset once the load succeeds");

            double loaded = worklogs.Sum(w => double.Parse(w.Split('+')[1].Split(':')[0]));
            Assert.That(loaded, Is.EqualTo(7), "the first attempt's 3 plus the retry's 4, nothing twice");
            VerifyStandardLoad(Times.Never());
        }


        [Test]
        public async Task ARetryThatFailsBeforeWriting_FallsBackAndLoadsOnlyTheRemainder()
        {
            WatchTimer timer = TimerHolding(7);

            failWorklogAt = 2;
            await LoadFrom(timer);          // 3 written, timer holds 4

            // The retry cannot even read the subtasks.
            jira.Setup(j => j.GetSubtasksAsync("TST-1")).ReturnsAsync((IReadOnlyList<PluginSubtask>)null);
            TimeLoadPipelineResult retry = await LoadFrom(timer);

            Assert.That(retry.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(originalAskedFor, Is.EqualTo(new[] { TimeSpan.FromMinutes(4) }), "the standard load covers the remainder, not the 7");
            Assert.That(timer.TimeElapsed, Is.EqualTo(TimeSpan.Zero));
        }
        #endregion
    }
}
