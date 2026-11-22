using System;
using NUnit.Framework;
using Unsquare.Core.Persistence;

namespace Unsquare.Tests.Persistence
{
    public class LegacySaveCountsTests
    {
        [Test]
        public void Any_AllZero_IsFalse() => Assert.That(new LegacySaveCounts(0, 0, 0).Any, Is.False);

        [TestCase(3, 0, 0)]
        [TestCase(0, 2, 0)]
        [TestCase(0, 0, 5)]
        public void Any_OnePositive_IsTrue(int convertible, int archived, int remaining) =>
            Assert.That(new LegacySaveCounts(convertible, archived, remaining).Any, Is.True);

        [TestCase(-1, 0, 0)]
        [TestCase(0, 0, -1)]
        public void Constructor_NegativeCount_Throws(int convertible, int archived, int remaining) =>
            Assert.Throws<ArgumentException>(() => new LegacySaveCounts(convertible, archived, remaining));
    }
}
