using NUnit.Framework;
using Unsquare.Core.Diagnostics;

namespace Unsquare.Tests.Diagnostics
{
    [TestFixture]
    public sealed class PhaseTimerTests
    {
        private long _now;

        [SetUp]
        public void SetUp() => _now = 0;

        // 1000 ticks per second: one tick is one millisecond.
        private PhaseTimer CreateTimer() => new(() => _now, 1000);

        private void Spend(PhaseTimer timer, string phase, long milliseconds)
        {
            using (timer.Measure(phase))
                _now += milliseconds;
        }

        [Test]
        public void Measure_SamePhaseTwice_AddsUp()
        {
            var timer = CreateTimer();

            Spend(timer, "deform", 5);
            _now += 100; // untimed
            Spend(timer, "deform", 7);

            Assert.That(timer.Milliseconds("deform"), Is.EqualTo(12.0));
        }

        [Test]
        public void Measure_TwoPhases_KeepsInsertionOrder()
        {
            var timer = CreateTimer();

            Spend(timer, "write", 2);
            Spend(timer, "deform", 3);
            Spend(timer, "write", 1);

            Assert.That(timer.Summary(), Is.EqualTo("write 3.0, deform 3.0 ms"));
        }

        [Test]
        public void Milliseconds_UnknownPhase_IsZero()
        {
            Assert.That(CreateTimer().Milliseconds("deform"), Is.EqualTo(0.0));
        }

        [Test]
        public void Reset_AfterMeasure_ClearsPhasesAndCounters()
        {
            var timer = CreateTimer();
            Spend(timer, "deform", 5);
            timer.Count("vertices", 10);

            timer.Reset();

            Assert.That(timer.Milliseconds("deform"), Is.EqualTo(0.0));
            Assert.That(timer.Total("vertices"), Is.EqualTo(0));
            Assert.That(timer.Summary(), Is.EqualTo("nothing timed"));
        }

        [Test]
        public void Summary_WithCounters_ListsThemAfterTimes()
        {
            var timer = CreateTimer();
            Spend(timer, "deform", 38);
            timer.Count("vertices", 40000);
            timer.Count("meshes", 1);
            timer.Count("vertices", 8210);

            Assert.That(timer.Summary(), Is.EqualTo("deform 38.0 ms; 48210 vertices, 1 meshes"));
        }

        [Test]
        public void Constructor_NonPositiveFrequency_Throws()
        {
            Assert.That(() => new PhaseTimer(() => 0, 0), Throws.ArgumentException);
        }
    }
}
