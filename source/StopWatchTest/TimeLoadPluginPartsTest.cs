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
    public class TimeLoadContractTypesTest
    {
        [Test]
        public void Source_User_HasNoPluginId()
        {
            Assert.That(TimeLoadSource.User.IsUser, Is.True);
            Assert.That(TimeLoadSource.User.PluginId, Is.Null);
            Assert.That(TimeLoadSource.User.ToString(), Is.EqualTo("user"));
        }


        [Test]
        public void Source_FromPlugin_CarriesTheId()
        {
            TimeLoadSource source = TimeLoadSource.FromPlugin("Alpha");

            Assert.That(source.IsUser, Is.False);
            Assert.That(source.PluginId, Is.EqualTo("Alpha"));
            Assert.That(source.ToString(), Is.EqualTo("plugin:Alpha"));
        }


        [TestCase(null)]
        [TestCase("")]
        public void Source_FromPlugin_RequiresAnId(string id)
        {
            Assert.Throws<ArgumentException>(() => TimeLoadSource.FromPlugin(id));
        }


        [Test]
        public void Declined_Cancelled_CarryNothing()
        {
            Assert.That(InsteadOfResult.Declined().Kind, Is.EqualTo(InsteadOfKind.Declined));
            Assert.That(InsteadOfResult.Cancelled().Kind, Is.EqualTo(InsteadOfKind.Cancelled));
            Assert.That(InsteadOfResult.Declined().TimeLoaded, Is.EqualTo(TimeSpan.Zero));
        }


        [Test]
        public void Handled_CarriesTheTimeLoaded()
        {
            InsteadOfResult result = InsteadOfResult.Handled(TimeSpan.FromMinutes(7));

            Assert.That(result.Kind, Is.EqualTo(InsteadOfKind.Handled));
            Assert.That(result.TimeLoaded, Is.EqualTo(TimeSpan.FromMinutes(7)));
        }


        [Test]
        public void Failed_CarriesAnOptionalReason()
        {
            Assert.That(InsteadOfResult.Failed("why").Reason, Is.EqualTo("why"));
            Assert.That(InsteadOfResult.Failed().Reason, Is.Null);
            Assert.That(InsteadOfResult.Failed().Kind, Is.EqualTo(InsteadOfKind.Failed));
        }
    }


    [TestFixture]
    public class TimeLoadRegistryTest
    {
        private static Task<InsteadOfResult> Declines(TimeLoadRequest request)
        {
            return Task.FromResult(InsteadOfResult.Declined());
        }


        [Test]
        public void Handlers_AreOrderedByIdOrdinalIgnoringCase()
        {
            var registry = new TimeLoadRegistry();
            registry.RegisterInsteadOf("beta", Declines);
            registry.RegisterInsteadOf("Gamma", Declines);
            registry.RegisterInsteadOf("Alpha", Declines);

            Assert.That(registry.Handlers.Select(h => h.PluginId), Is.EqualTo(new[] { "Alpha", "beta", "Gamma" }));
        }


        [Test]
        public void ASecondHandlerFromTheSamePlugin_Throws()
        {
            var registry = new TimeLoadRegistry();
            registry.RegisterInsteadOf("Alpha", Declines);

            Assert.Throws<InvalidOperationException>(() => registry.RegisterInsteadOf("ALPHA", Declines));
        }


        [Test]
        public void Observers_AreKeptInSubscriptionOrderAndCanBeRemoved()
        {
            var registry = new TimeLoadRegistry();
            EventHandler<TimeLoadedEventArgs> first = (s, e) => { };
            EventHandler<TimeLoadedEventArgs> second = (s, e) => { };
            registry.AddObserver(first);
            registry.AddObserver(second);

            Assert.That(registry.Observers, Is.EqualTo(new[] { first, second }));

            registry.RemoveObserver(first);
            Assert.That(registry.Observers, Is.EqualTo(new[] { second }));
        }
    }


    [TestFixture]
    public class CountingJiraApiTest
    {
        private Mock<IJiraApi> inner;
        private CountingJiraApi counting;

        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void Setup()
        {
            inner = new Mock<IJiraApi>();
            counting = new CountingJiraApi(inner.Object);
        }


        [Test]
        public async Task AcceptedWrites_AreCounted()
        {
            inner.Setup(j => j.AddWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>())).ReturnsAsync(true);
            inner.Setup(j => j.AddWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<PluginEstimateUpdate>(), It.IsAny<string>())).ReturnsAsync(true);
            inner.Setup(j => j.CreateSubtaskAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("TST-2");

            await counting.AddWorklogAsync("TST-1", Start, TimeSpan.FromMinutes(1), "");
            await counting.AddWorklogAsync("TST-1", Start, TimeSpan.FromMinutes(1), "", PluginEstimateUpdate.Leave, null);
            await counting.CreateSubtaskAsync("TST-1", "Part");

            Assert.That(counting.WritesMade, Is.EqualTo(3));
        }


        [Test]
        public async Task RejectedWrites_AreNotCounted()
        {
            inner.Setup(j => j.AddWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>())).ReturnsAsync(false);
            inner.Setup(j => j.CreateSubtaskAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((string)null);

            Assert.That(await counting.AddWorklogAsync("TST-1", Start, TimeSpan.FromMinutes(1), ""), Is.False);
            Assert.That(await counting.CreateSubtaskAsync("TST-1", "Part"), Is.Null);

            Assert.That(counting.WritesMade, Is.EqualTo(0));
        }


        [Test]
        public async Task ReadsAndComments_AreForwardedAndNotCounted()
        {
            inner.Setup(j => j.GetSummaryAsync("TST-1")).ReturnsAsync("Summary");
            inner.Setup(j => j.AddCommentAsync("TST-1", "c")).ReturnsAsync(true);

            Assert.That(await counting.GetSummaryAsync("TST-1"), Is.EqualTo("Summary"));
            Assert.That(await counting.AddCommentAsync("TST-1", "c"), Is.True);

            Assert.That(counting.WritesMade, Is.EqualTo(0));
        }


        [Test]
        public async Task TwoInstances_DoNotShareACount()
        {
            inner.Setup(j => j.AddWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>())).ReturnsAsync(true);
            var other = new CountingJiraApi(inner.Object);

            await Task.WhenAll(
                counting.AddWorklogAsync("TST-1", Start, TimeSpan.FromMinutes(1), ""),
                counting.AddWorklogAsync("TST-1", Start, TimeSpan.FromMinutes(1), ""),
                other.AddWorklogAsync("TST-1", Start, TimeSpan.FromMinutes(1), ""));

            Assert.That(counting.WritesMade, Is.EqualTo(2));
            Assert.That(other.WritesMade, Is.EqualTo(1));
        }
    }


    [TestFixture]
    public class PluginTimeLoadTest
    {
        private TimeLoadRegistry registry;
        private Mock<IWorklogPoster> original;
        private TimeLoadPipeline pipeline;

        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void Setup()
        {
            registry = new TimeLoadRegistry();
            original = new Mock<IWorklogPoster>();
            original.Setup(o => o.PostWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()))
                .ReturnsAsync(new PostWorklogResult { Success = true });
            pipeline = new TimeLoadPipeline(original.Object, registry, new Mock<IJiraApi>().Object, null);
        }


        [Test]
        public void RegisterInsteadOf_RegistersTheHandlerUnderThePluginsId()
        {
            new PluginTimeLoad("Alpha", registry).RegisterInsteadOf(request => Task.FromResult(InsteadOfResult.Declined()));

            Assert.That(registry.Handlers.Single().PluginId, Is.EqualTo("Alpha"));
        }


        [Test]
        public void RegisteringTwice_Throws()
        {
            var timeLoad = new PluginTimeLoad("Alpha", registry);
            timeLoad.RegisterInsteadOf(request => Task.FromResult(InsteadOfResult.Declined()));

            Assert.Throws<InvalidOperationException>(() => timeLoad.RegisterInsteadOf(request => Task.FromResult(InsteadOfResult.Declined())));
        }


        [Test]
        public async Task After_SubscribersAreNotified()
        {
            TimeLoadedEventArgs seen = null;
            new PluginTimeLoad("Alpha", registry).After += (s, e) => seen = e;

            await pipeline.LoadAsync(new TimeLoadInput { IssueKey = "TST-1", StartTime = Start, TimeElapsed = TimeSpan.FromMinutes(1) });

            Assert.That(seen, Is.Not.Null);
        }


        [Test]
        public async Task LoaderLoad_UsesThePluginAsItsSource_AndSkipsItsOwnHandler()
        {
            TimeLoadSource seen = null;
            bool alphaAsked = false;
            registry.RegisterInsteadOf("Alpha", request => { alphaAsked = true; return Task.FromResult(InsteadOfResult.Declined()); });
            registry.RegisterInsteadOf("Beta", request => { seen = request.Source; return Task.FromResult(InsteadOfResult.Declined()); });

            TimeLoadResult result = await new PluginTimeLoader("Alpha", pipeline)
                .LoadAsync("TST-1", Start, TimeSpan.FromMinutes(7), "c", PluginEstimateUpdate.Leave, null);

            Assert.That(result.Success, Is.True);
            Assert.That(alphaAsked, Is.False);
            Assert.That(seen.PluginId, Is.EqualTo("Alpha"));
            original.Verify(o => o.PostWorklogAsync("TST-1", Start, TimeSpan.FromMinutes(7), "c", EstimateUpdateMethods.Leave, ""), Times.Once());
        }


        [Test]
        public async Task LoaderLoad_Failure_IsReportedWithoutThrowing()
        {
            original.Setup(o => o.PostWorklogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<EstimateUpdateMethods>(), It.IsAny<string>()))
                .ReturnsAsync(new PostWorklogResult { Success = false });

            TimeLoadResult result = await new PluginTimeLoader("Alpha", pipeline)
                .LoadAsync("TST-1", Start, TimeSpan.FromMinutes(7), "c", PluginEstimateUpdate.Auto, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.Not.Empty);
        }
    }
}
