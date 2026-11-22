using System.Collections.Generic;
using NUnit.Framework;
using Unsquare.Core.Caching;

namespace Unsquare.Tests.Caching
{
    public class LruBudgetTests
    {
        private static List<string> Collect(LruBudget<string> lru)
        {
            var evicted = new List<string>();
            lru.CollectOverBudget(evicted);
            return evicted;
        }

        [Test]
        public void CollectOverBudget_UnderBudget_EvictsNothing()
        {
            var lru = new LruBudget<string>(100);
            lru.Touch("a", 40);
            lru.Touch("b", 60);

            Assert.That(Collect(lru), Is.Empty);
            Assert.That(lru.Count, Is.EqualTo(2));
        }

        [Test]
        public void CollectOverBudget_OverBudget_EvictsOldestFirstUntilItFits()
        {
            var lru = new LruBudget<string>(70);
            lru.Touch("oldest", 50);
            lru.Touch("old", 30);
            lru.Touch("mid", 30);
            lru.Touch("new", 30);

            Assert.That(Collect(lru), Is.EqualTo(new[] { "oldest", "old" }));
            Assert.That(lru.TotalCost, Is.EqualTo(60));
            Assert.That(lru.Contains("oldest"), Is.False);
        }

        [Test]
        public void Touch_ExistingKey_MakesItMostRecent()
        {
            var lru = new LruBudget<string>(60);
            lru.Touch("a", 30);
            lru.Touch("b", 30);
            lru.Touch("a", 30);
            lru.Touch("c", 30);

            Assert.That(Collect(lru), Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void Touch_SameKeyTwice_CountsItOnce()
        {
            var lru = new LruBudget<string>(100);
            lru.Touch("a", 40);
            lru.Touch("a", 50);

            Assert.That(lru.Count, Is.EqualTo(1));
            Assert.That(lru.TotalCost, Is.EqualTo(50));
        }

        [Test]
        public void CollectOverBudget_MostRecentAloneOverBudget_KeepsIt()
        {
            var lru = new LruBudget<string>(100);
            lru.Touch("small", 10);
            lru.Touch("huge", 500);

            Assert.That(Collect(lru), Is.EqualTo(new[] { "small" }));
            Assert.That(lru.Contains("huge"), Is.True);
        }

        [Test]
        public void Budget_Lowered_EvictsAtNextCollect()
        {
            var lru = new LruBudget<string>(100);
            lru.Touch("a", 40);
            lru.Touch("b", 40);
            lru.Budget = 50;

            Assert.That(Collect(lru), Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void Remove_And_Clear_UpdateTotals()
        {
            var lru = new LruBudget<string>(100);
            lru.Touch("a", 40);
            lru.Touch("b", 40);

            Assert.That(lru.Remove("a"), Is.True);
            Assert.That(lru.Remove("a"), Is.False);
            Assert.That(lru.TotalCost, Is.EqualTo(40));
            lru.Clear();
            Assert.That(lru.Count, Is.Zero);
            Assert.That(lru.TotalCost, Is.Zero);
        }

        [Test]
        public void Constructor_And_Touch_InvalidArguments_Throw()
        {
            Assert.That(() => new LruBudget<string>(0), Throws.ArgumentException);
            var lru = new LruBudget<string>(10);
            Assert.That(() => lru.Budget = -1, Throws.ArgumentException);
            Assert.That(() => lru.Touch("a", -1), Throws.ArgumentException);
        }
    }
}
