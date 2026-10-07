using System.Collections.Generic;
using NUnit.Framework;
using Unsquare.Core.Scheduling;

namespace Unsquare.Tests.Scheduling
{
    public class DebouncerTests
    {
        private Debouncer<int, string> _debouncer;
        private List<KeyValuePair<int, string>> _due;

        [SetUp]
        public void SetUp()
        {
            _debouncer = new Debouncer<int, string>(0.4);
            _due = new List<KeyValuePair<int, string>>();
        }

        [Test]
        public void CollectDue_BeforeDelay_ReturnsNothing()
        {
            _debouncer.Request(1, "a", now: 10.0);
            Assert.That(_debouncer.CollectDue(10.3, _due), Is.EqualTo(0));
            Assert.That(_debouncer.Count, Is.EqualTo(1));
        }

        [Test]
        public void CollectDue_AfterDelay_ReturnsLatestValueAndRemovesIt()
        {
            _debouncer.Request(1, "a", 10.0);
            _debouncer.Request(1, "b", 10.1);
            Assert.That(_debouncer.CollectDue(10.5, _due), Is.EqualTo(1));
            Assert.That(_due[0], Is.EqualTo(new KeyValuePair<int, string>(1, "b")));
            Assert.That(_debouncer.Count, Is.EqualTo(0));
        }

        [Test]
        public void Request_RestartsDelay()
        {
            _debouncer.Request(1, "a", 10.0);
            _debouncer.Request(1, "b", 10.3);
            Assert.That(_debouncer.CollectDue(10.5, _due), Is.EqualTo(0));
            Assert.That(_debouncer.CollectDue(10.8, _due), Is.EqualTo(1));
        }

        [Test]
        public void TryPeek_ShowsPendingValue()
        {
            _debouncer.Request(2, "x", 0);
            Assert.That(_debouncer.TryPeek(2, out var value), Is.True);
            Assert.That(value, Is.EqualTo("x"));
            Assert.That(_debouncer.TryPeek(3, out _), Is.False);
        }

        [Test]
        public void Cancel_DropsRequest()
        {
            _debouncer.Request(1, "a", 0);
            Assert.That(_debouncer.Cancel(1), Is.True);
            Assert.That(_debouncer.CollectDue(100, _due), Is.EqualTo(0));
        }

        [Test]
        public void CollectDue_KeysAreIndependent()
        {
            _debouncer.Request(1, "a", 0.0);
            _debouncer.Request(2, "b", 0.3);
            Assert.That(_debouncer.CollectDue(0.5, _due), Is.EqualTo(1));
            Assert.That(_due[0].Key, Is.EqualTo(1));
        }
    }
}
