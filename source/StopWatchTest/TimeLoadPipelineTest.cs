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


    [TestFixture]
    public class TimeLoadPipelineTest
    {
        private Mock<IWorklogPoster> original;
        private Mock<IJiraApi> rawJira;
        private TimeLoadRegistry registry;
        private TimeLoadPipeline pipeline;
        private FakeNotifier notifier;
        private List<string> logged;

        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan Total = TimeSpan.FromMinutes(7);

        private class FakeNotifier : ITimeLoadNotifier
        {
            public List<string> Infos = new List<string>();
            public List<string> Warnings = new List<string>();
            public void Info(string message) { Infos.Add(message); }
            public void Warning(string message) { Warnings.Add(message); }
        }


        [SetUp]
        public void Setup()
        {
            logged = new List<string>();
            notifier = new FakeNotifier();

            original = new Mock<IWorklogPoster>();
            original.Setup(o => o.PostWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()))
                .ReturnsAsync(new PostWorklogResult { Success = true });

            rawJira = new Mock<IJiraApi>();
            rawJira.Setup(j => j.AddWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>())).ReturnsAsync(true);
            rawJira.Setup(j => j.AddWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<PluginEstimateUpdate>(), It.IsAny<string>())).ReturnsAsync(true);
            rawJira.Setup(j => j.CreateSubtaskAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("TST-99");

            registry = new TimeLoadRegistry();
            pipeline = new TimeLoadPipeline(original.Object, registry, rawJira.Object, (message, ex) => logged.Add(message));
            pipeline.Notifier = notifier;
        }


        private TimeLoadInput Input(TimeLoadSource source = null)
        {
            return new TimeLoadInput
            {
                IssueKey = "TST-1",
                StartTime = Start,
                TimeElapsed = Total,
                Comment = "work",
                EstimateUpdateMethod = EstimateUpdateMethods.SetTo,
                EstimateUpdateValue = "4h",
                Source = source ?? TimeLoadSource.User
            };
        }

        private Task<TimeLoadPipelineResult> Load(TimeLoadSource source = null, Action<bool> busy = null)
        {
            return pipeline.LoadAsync(Input(source), busy);
        }

        private void Register(string id, Func<TimeLoadRequest, Task<InsteadOfResult>> handler)
        {
            registry.RegisterInsteadOf(id, handler);
        }

        private void VerifyOriginalRan(Times times)
        {
            original.Verify(o => o.PostWorklogAsync("TST-1", Start, Total, "work", EstimateUpdateMethods.SetTo, "4h"), times);
        }

        private static Func<TimeLoadRequest, Task<InsteadOfResult>> Answer(InsteadOfResult result, int writesFirst = 0)
        {
            return async request =>
            {
                for (int i = 0; i < writesFirst; i++)
                    await request.Jira.AddWorklogAsync(request.IssueKey, request.StartTime, TimeSpan.FromMinutes(1), "");
                return result;
            };
        }


        #region no plugin takes part
        [Test]
        public async Task NoHandlers_RunsTheOriginalAndSucceeds()
        {
            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(result.HandledBy, Is.Null);
            VerifyOriginalRan(Times.Once());
        }


        [Test]
        public async Task NoHandlers_OriginalFails_Fails()
        {
            original.Setup(o => o.PostWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()))
                .ReturnsAsync(new PostWorklogResult { Success = false });

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            Assert.That(notifier.Warnings, Is.Empty);
        }
        #endregion


        #region the outcome table
        [Test]
        public async Task Declined_NoWrites_RunsTheOriginal()
        {
            Register("Alpha", Answer(InsteadOfResult.Declined()));

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            VerifyOriginalRan(Times.Once());
        }


        [Test]
        public async Task Declined_WithWrites_IsAFailureWithWrites()
        {
            Register("Alpha", Answer(InsteadOfResult.Declined(), writesFirst: 1));

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            Assert.That(result.WritesMade, Is.EqualTo(1));
            Assert.That(result.TimeLoaded, Is.EqualTo(TimeSpan.FromMinutes(1)));
            VerifyOriginalRan(Times.Never());
            Assert.That(notifier.Warnings, Has.Count.EqualTo(1));
        }


        [Test]
        public async Task Handled_TimeMatches_SucceedsWithoutRunningTheOriginal()
        {
            Register("Alpha", Answer(InsteadOfResult.Handled(Total), writesFirst: 3));

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(result.HandledBy, Is.EqualTo("Alpha"));
            Assert.That(result.WritesMade, Is.EqualTo(3));
            VerifyOriginalRan(Times.Never());
            Assert.That(notifier.Warnings, Is.Empty);
        }


        [Test]
        public async Task Handled_TimeDiffers_DoesNotSucceedAndWarns()
        {
            Register("Alpha", Answer(InsteadOfResult.Handled(TimeSpan.FromMinutes(5)), writesFirst: 2));

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            VerifyOriginalRan(Times.Never());
            Assert.That(notifier.Warnings, Has.Count.EqualTo(1));

            // It says what the plugin declared (5), what was confirmed (7), what
            // reached Jira (2) and what the timer keeps (5).
            Assert.That(notifier.Warnings[0], Does.Contain("loaded 5 min").And.Contain("7 min was confirmed").And.Contain("2 min reached Jira").And.Contain("keeps the 5 min"));
        }


        [Test]
        public async Task Handled_TimeDiffers_TheTimeToSubtractIsWhatTheHostMeasured_NotWhatThePluginDeclared()
        {
            Register("Alpha", Answer(InsteadOfResult.Handled(TimeSpan.FromMinutes(5)), writesFirst: 2));

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.TimeLoaded, Is.EqualTo(TimeSpan.FromMinutes(2)));
        }


        [Test]
        public async Task ThePartialNotice_CountsTheRemainderFromWhatTheTimerHoldsNow()
        {
            Register("Alpha", Answer(InsteadOfResult.Failed("halfway"), writesFirst: 2));
            TimeLoadInput input = Input();

            // The timer kept running while the handler waited.
            input.CurrentElapsed = () => TimeSpan.FromMinutes(9);

            await pipeline.LoadAsync(input);

            Assert.That(notifier.Warnings[0], Does.Contain("keeps the 7 min"));
        }


        [Test]
        public async Task Cancelled_NoWrites_DoesNothingAndSaysNothing()
        {
            Register("Alpha", Answer(InsteadOfResult.Cancelled()));

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Cancelled));
            VerifyOriginalRan(Times.Never());
            Assert.That(notifier.Warnings, Is.Empty);
            Assert.That(notifier.Infos, Is.Empty);
        }


        [Test]
        public async Task Cancelled_WithWrites_IsAFailureWithWrites()
        {
            Register("Alpha", Answer(InsteadOfResult.Cancelled(), writesFirst: 1));

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            Assert.That(result.TimeLoaded, Is.EqualTo(TimeSpan.FromMinutes(1)));
            VerifyOriginalRan(Times.Never());
            Assert.That(notifier.Warnings, Has.Count.EqualTo(1));
        }


        [Test]
        public async Task Failed_NoWrites_FallsBackToTheOriginalWithANonModalNotice()
        {
            Register("Alpha", Answer(InsteadOfResult.Failed("no luck")));

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(result.HandledBy, Is.Null);
            VerifyOriginalRan(Times.Once());
            Assert.That(notifier.Infos, Has.Count.EqualTo(1));
            Assert.That(notifier.Infos[0], Does.Contain("Alpha").And.Contain("no luck"));
            Assert.That(notifier.Warnings, Is.Empty);
            Assert.That(logged, Has.Some.Contain("Alpha"));
        }


        [Test]
        public async Task Failed_WithWrites_DoesNotFallBackAndWarnsWithTheWriteCount()
        {
            Register("Alpha", Answer(InsteadOfResult.Failed("halfway"), writesFirst: 2));

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            Assert.That(result.WritesMade, Is.EqualTo(2));
            Assert.That(result.TimeLoaded, Is.EqualTo(TimeSpan.FromMinutes(2)));
            VerifyOriginalRan(Times.Never());
            Assert.That(notifier.Warnings, Has.Count.EqualTo(1));
            Assert.That(notifier.Warnings[0], Does.Contain("2 write").And.Contain("2 min loaded").And.Contain("keeps the 5 min"));
        }
        #endregion


        #region a handler that misbehaves
        [Test]
        public async Task HandlerThrowsBeforeItsFirstAwait_FallsBack()
        {
            Register("Alpha", request => throw new InvalidOperationException("sync boom"));

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            VerifyOriginalRan(Times.Once());
            Assert.That(notifier.Infos, Has.Count.EqualTo(1));
        }


        [Test]
        public async Task HandlerThrowsAfterAwaiting_FallsBackWhenItWroteNothing()
        {
            Register("Alpha", async request =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async boom");
            });

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            VerifyOriginalRan(Times.Once());
        }


        [Test]
        public async Task HandlerThrowsAfterWriting_DoesNotFallBack()
        {
            Register("Alpha", async request =>
            {
                await request.Jira.AddWorklogAsync(request.IssueKey, request.StartTime, TimeSpan.FromMinutes(3), "");
                throw new InvalidOperationException("after writing");
            });

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            Assert.That(result.WritesMade, Is.EqualTo(1));
            VerifyOriginalRan(Times.Never());
            Assert.That(notifier.Warnings, Has.Count.EqualTo(1));
        }


        [Test]
        public async Task HandlerReturnsNoTask_IsAFailure()
        {
            Register("Alpha", request => null);

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            VerifyOriginalRan(Times.Once());
            Assert.That(notifier.Infos, Has.Count.EqualTo(1));
        }


        [Test]
        public async Task HandlerReturnsNoResult_IsAFailure()
        {
            Register("Alpha", request => Task.FromResult<InsteadOfResult>(null));

            TimeLoadPipelineResult result = await Load();

            VerifyOriginalRan(Times.Once());
            Assert.That(notifier.Infos, Has.Count.EqualTo(1));
            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
        }
        #endregion


        #region what the handler sees
        [Test]
        public async Task Handler_ReceivesTheConfirmedValuesAndTheUserSource()
        {
            TimeLoadRequest seen = null;
            Register("Alpha", request =>
            {
                seen = request;
                return Task.FromResult(InsteadOfResult.Declined());
            });

            await Load();

            Assert.That(seen.IssueKey, Is.EqualTo("TST-1"));
            Assert.That(seen.StartTime, Is.EqualTo(Start));
            Assert.That(seen.TimeElapsed, Is.EqualTo(Total));
            Assert.That(seen.Comment, Is.EqualTo("work"));
            Assert.That(seen.EstimateUpdate, Is.EqualTo(PluginEstimateUpdate.SetTo));
            Assert.That(seen.EstimateValue, Is.EqualTo("4h"));
            Assert.That(seen.Source.IsUser, Is.True);
            Assert.That(seen.Jira, Is.Not.Null);
        }


        [Test]
        public async Task EachInvocationGetsItsOwnCounter()
        {
            Register("Alpha", Answer(InsteadOfResult.Declined(), writesFirst: 1));
            Register("Beta", Answer(InsteadOfResult.Handled(Total), writesFirst: 2));

            TimeLoadPipelineResult result = await Load();

            // Alpha declined after writing, so the chain stops there with its one write.
            Assert.That(result.HandledBy, Is.EqualTo("Alpha"));
            Assert.That(result.WritesMade, Is.EqualTo(1));
        }
        #endregion


        #region several handlers
        [Test]
        public async Task Handlers_AreConsultedByIdIgnoringCaseAndTheFirstThatDoesNotDeclineWins()
        {
            var order = new List<string>();
            Func<string, InsteadOfResult, Func<TimeLoadRequest, Task<InsteadOfResult>>> note =
                (id, answer) => request => { order.Add(id); return Task.FromResult(answer); };

            Register("beta", note("beta", InsteadOfResult.Handled(Total)));
            Register("Alpha", note("Alpha", InsteadOfResult.Declined()));
            Register("Gamma", note("Gamma", InsteadOfResult.Handled(Total)));

            TimeLoadPipelineResult result = await Load();

            Assert.That(order, Is.EqualTo(new[] { "Alpha", "beta" }));
            Assert.That(result.HandledBy, Is.EqualTo("beta"));
        }


        [Test]
        public async Task AFailureFromTheFirstHandler_IsNotPassedOnToTheNext()
        {
            bool betaAsked = false;
            Register("Alpha", Answer(InsteadOfResult.Failed("no")));
            Register("Beta", request => { betaAsked = true; return Task.FromResult(InsteadOfResult.Handled(Total)); });

            TimeLoadPipelineResult result = await Load();

            Assert.That(betaAsked, Is.False);
            Assert.That(result.HandledBy, Is.Null);
            VerifyOriginalRan(Times.Once());
        }


        [Test]
        public async Task APlugin_IsNotAskedAboutItsOwnLoads_ButTheOthersAre()
        {
            var asked = new List<string>();
            Register("Alpha", request => { asked.Add("Alpha"); return Task.FromResult(InsteadOfResult.Declined()); });
            Register("Beta", request => { asked.Add("Beta"); return Task.FromResult(InsteadOfResult.Declined()); });

            await Load(TimeLoadSource.FromPlugin("alpha"));

            Assert.That(asked, Is.EqualTo(new[] { "Beta" }));
            VerifyOriginalRan(Times.Once());
        }
        #endregion


        #region a load started inside a handler
        [Test]
        public async Task ALoadStartedInsideAHandler_ConsultsNoHandlers()
        {
            var asked = new List<string>();
            TimeLoadPipelineResult nested = null;

            Register("Alpha", async request =>
            {
                asked.Add("Alpha");
                nested = await pipeline.LoadAsync(Input(TimeLoadSource.FromPlugin("Alpha")));
                return InsteadOfResult.Cancelled();
            });
            Register("Beta", request => { asked.Add("Beta"); return Task.FromResult(InsteadOfResult.Declined()); });

            await Load();

            Assert.That(asked, Is.EqualTo(new[] { "Alpha" }));
            Assert.That(nested.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            VerifyOriginalRan(Times.Once());
        }


        [Test]
        public async Task AfterTheHandlerReturns_LaterLoadsConsultHandlersAgain()
        {
            int asked = 0;
            Register("Alpha", request => { asked++; return Task.FromResult(InsteadOfResult.Declined()); });

            await Load();
            await Load();

            Assert.That(asked, Is.EqualTo(2));
        }
        #endregion


        #region the busy callback
        [Test]
        public async Task Busy_IsRaisedOnlyAroundTheOriginalLoad()
        {
            var events = new List<string>();
            Register("Alpha", request => { events.Add("handler"); return Task.FromResult(InsteadOfResult.Declined()); });
            original.Setup(o => o.PostWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()))
                .Callback(() => events.Add("original"))
                .ReturnsAsync(new PostWorklogResult { Success = true });

            await Load(busy: busy => events.Add(busy ? "busy" : "idle"));

            Assert.That(events, Is.EqualTo(new[] { "handler", "busy", "original", "idle" }));
        }


        [Test]
        public async Task Busy_IsNotRaisedWhenAHandlerTakesTheLoad()
        {
            var events = new List<string>();
            Register("Alpha", Answer(InsteadOfResult.Handled(Total)));

            await Load(busy: busy => events.Add(busy ? "busy" : "idle"));

            Assert.That(events, Is.Empty);
        }
        #endregion


        #region notices
        [Test]
        public async Task ForALoadAPluginStarted_NothingIsShownButItIsLogged()
        {
            Register("Alpha", Answer(InsteadOfResult.Failed("halfway"), writesFirst: 1));

            TimeLoadPipelineResult result = await Load(TimeLoadSource.FromPlugin("Other"));

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            Assert.That(notifier.Warnings, Is.Empty);
            Assert.That(notifier.Infos, Is.Empty);
            Assert.That(logged, Has.Some.Contain("Alpha"));
        }


        [Test]
        public async Task ForALoadAPluginStarted_AFallbackIsNotShownEither()
        {
            Register("Alpha", Answer(InsteadOfResult.Failed("no")));

            await Load(TimeLoadSource.FromPlugin("Other"));

            Assert.That(notifier.Infos, Is.Empty);
            VerifyOriginalRan(Times.Once());
        }
        #endregion


        #region observers
        [Test]
        public async Task Observers_AreToldTheHostHandledALoad()
        {
            TimeLoadedEventArgs seen = null;
            registry.AddObserver((s, e) => seen = e);

            await Load();

            Assert.That(seen.HandledBy, Is.Null);
            Assert.That(seen.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(seen.TimeLoaded, Is.EqualTo(Total));
            Assert.That(seen.IssueKey, Is.EqualTo("TST-1"));
            Assert.That(seen.Source.IsUser, Is.True);
        }


        [Test]
        public async Task Observers_AreToldWhichPluginHandledALoad()
        {
            TimeLoadedEventArgs seen = null;
            registry.AddObserver((s, e) => seen = e);
            Register("Alpha", Answer(InsteadOfResult.Handled(Total), writesFirst: 2));

            await Load();

            Assert.That(seen.HandledBy, Is.EqualTo("Alpha"));
            Assert.That(seen.WritesMade, Is.EqualTo(2));
            Assert.That(seen.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
        }


        [Test]
        public async Task Observers_AreToldAboutACancelledLoad()
        {
            TimeLoadedEventArgs seen = null;
            registry.AddObserver((s, e) => seen = e);
            Register("Alpha", Answer(InsteadOfResult.Cancelled()));

            await Load();

            Assert.That(seen.Outcome, Is.EqualTo(TimeLoadOutcome.Cancelled));
            Assert.That(seen.TimeLoaded, Is.EqualTo(TimeSpan.Zero));
        }


        [Test]
        public async Task Observers_AreToldAboutAFailureAndItsReason()
        {
            TimeLoadedEventArgs seen = null;
            registry.AddObserver((s, e) => seen = e);
            Register("Alpha", Answer(InsteadOfResult.Failed("halfway"), writesFirst: 1));

            await Load();

            Assert.That(seen.Outcome, Is.EqualTo(TimeLoadOutcome.Failed));
            Assert.That(seen.Reason, Is.EqualTo("halfway"));
            Assert.That(seen.WritesMade, Is.EqualTo(1));
            Assert.That(seen.TimeLoaded, Is.EqualTo(TimeSpan.FromMinutes(1)));
        }


        [Test]
        public async Task AnObserverThatThrows_DoesNotAffectTheLoadOrTheOtherObservers()
        {
            bool second = false;
            registry.AddObserver((s, e) => throw new InvalidOperationException("observer boom"));
            registry.AddObserver((s, e) => second = true);

            TimeLoadPipelineResult result = await Load();

            Assert.That(result.Outcome, Is.EqualTo(TimeLoadOutcome.Succeeded));
            Assert.That(second, Is.True);
            Assert.That(logged, Has.Some.Contain("observer boom"));
        }
        #endregion
    }
}
