namespace StopWatchTest
{
    using System;
    using NUnit.Framework;
    using StopWatch;
    using StopWatch.Plugin;


    [TestFixture]
    public class TimerAfterLoadTest
    {
        private WatchTimer timer;
        private bool wasReset;

        private static readonly DateTimeOffset RecordedStart = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void Setup()
        {
            timer = new WatchTimer();
            timer.SetState(new TimerState { Running = false, TotalTime = TimeSpan.FromMinutes(10), InitialStartTime = RecordedStart });
            wasReset = false;
        }


        private void Apply(TimeLoadOutcome outcome, TimeSpan loaded)
        {
            TimerAfterLoad.Apply(
                new TimeLoadPipelineResult { Outcome = outcome, TimeLoaded = loaded },
                () => timer.TimeElapsed,
                elapsed => timer.TimeElapsed = elapsed,
                () => { wasReset = true; timer.Reset(); });
        }


        [Test]
        public void ASuccessfulLoad_ResetsTheTimer()
        {
            Apply(TimeLoadOutcome.Succeeded, TimeSpan.FromMinutes(10));

            Assert.That(wasReset, Is.True);
            Assert.That(timer.TimeElapsed, Is.EqualTo(TimeSpan.Zero));
        }


        [Test]
        public void APartialLoad_ReducesTheTimerByWhatReachedJira()
        {
            Apply(TimeLoadOutcome.Failed, TimeSpan.FromMinutes(4));

            Assert.That(wasReset, Is.False);
            Assert.That(timer.TimeElapsed, Is.EqualTo(TimeSpan.FromMinutes(6)));
        }


        [Test]
        public void APartialLoad_KeepsTheRecordedStartTime()
        {
            Apply(TimeLoadOutcome.Failed, TimeSpan.FromMinutes(4));

            Assert.That(timer.GetState().InitialStartTime, Is.EqualTo(RecordedStart));
        }


        [Test]
        public void APartialLoad_NeverGoesBelowZero()
        {
            Apply(TimeLoadOutcome.Failed, TimeSpan.FromMinutes(25));

            Assert.That(timer.TimeElapsed, Is.EqualTo(TimeSpan.Zero));
            Assert.That(wasReset, Is.False);
        }


        [Test]
        public void APartialLoad_OfARunningTimer_ReducesFromItsValueNowAndKeepsItRunning()
        {
            // 10 minutes already counted, and the session in progress adds to them.
            timer.SetState(new TimerState
            {
                Running = true,
                TotalTime = TimeSpan.FromMinutes(10),
                SessionStartTime = DateTime.Now.AddMinutes(-5),
                InitialStartTime = RecordedStart
            });

            Apply(TimeLoadOutcome.Failed, TimeSpan.FromMinutes(4));

            Assert.That(timer.Running, Is.True);
            Assert.That(timer.TimeElapsed.TotalMinutes, Is.EqualTo(11).Within(0.1));
            Assert.That(timer.GetState().InitialStartTime, Is.EqualTo(RecordedStart));
        }


        [TestCase(TimeLoadOutcome.Cancelled)]
        [TestCase(TimeLoadOutcome.Failed)]
        public void NothingLoaded_LeavesTheTimerAlone(TimeLoadOutcome outcome)
        {
            Apply(outcome, TimeSpan.Zero);

            Assert.That(wasReset, Is.False);
            Assert.That(timer.TimeElapsed, Is.EqualTo(TimeSpan.FromMinutes(10)));
        }


        [Test]
        public void Remaining_IsTheDifferenceAndNeverNegative()
        {
            Assert.That(TimerAfterLoad.Remaining(TimeSpan.FromMinutes(7), TimeSpan.FromMinutes(4)), Is.EqualTo(TimeSpan.FromMinutes(3)));
            Assert.That(TimerAfterLoad.Remaining(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(4)), Is.EqualTo(TimeSpan.Zero));
        }
    }
}
