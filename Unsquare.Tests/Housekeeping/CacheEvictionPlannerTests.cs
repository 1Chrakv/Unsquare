using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unsquare.Core.Housekeeping;

namespace Unsquare.Tests.Housekeeping
{
    public class CacheEvictionPlannerTests
    {
        private static readonly DateTime Now = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
        private static readonly HashSet<string> NoneClaimed = new();

        private static CachedAsset Asset(string name, long bytes, int daysAgo) => new(name, bytes, Now.AddDays(-daysAgo));

        [Test]
        public void Plan_UnderCap_EvictsNothing()
        {
            var assets = new[] { Asset("a", 40, 10), Asset("b", 60, 1) };
            Assert.That(CacheEvictionPlanner.Plan(assets, NoneClaimed, 100).Evicted, Is.Empty);
        }

        [Test]
        public void Plan_OverCap_EvictsOldestFirstUntilItFits()
        {
            var assets = new[] { Asset("new", 50, 1), Asset("oldest", 30, 30), Asset("old", 30, 20), Asset("mid", 30, 5) };

            var plan = CacheEvictionPlanner.Plan(assets, NoneClaimed, 80).Evicted;

            Assert.That(plan, Is.EqualTo(new[] { "oldest", "old" }));
        }

        [Test]
        public void Plan_ClaimedAssets_NeverEvictedNorCounted()
        {
            var assets = new[] { Asset("legacy", 500, 100), Asset("a", 40, 10), Asset("b", 40, 1) };
            var claimed = new HashSet<string> { "legacy" };

            Assert.That(CacheEvictionPlanner.Plan(assets, claimed, 100).Evicted, Is.Empty);
            Assert.That(CacheEvictionPlanner.Plan(assets, claimed, 50).Evicted, Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void Plan_EqualTimes_TieBrokenByName()
        {
            var assets = new[] { Asset("c", 10, 3), Asset("a", 10, 3), Asset("b", 10, 3) };

            var plan = CacheEvictionPlanner.Plan(assets, NoneClaimed, 10).Evicted;

            Assert.That(plan, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void Plan_MixedAssets_ReportsCacheAndClaimedTotals()
        {
            var assets = new[] { Asset("legacy", 500, 100), Asset("a", 40, 10), Asset("b", 60, 1) };
            var claimed = new HashSet<string> { "legacy" };

            var plan = CacheEvictionPlanner.Plan(assets, claimed, 50);

            Assert.That(plan.CacheBytes, Is.EqualTo(100));
            Assert.That(plan.ClaimedBytes, Is.EqualTo(500));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Plan_NonPositiveCap_Throws(long cap)
        {
            Assert.That(() => CacheEvictionPlanner.Plan(new CachedAsset[0], NoneClaimed, cap), Throws.ArgumentException);
        }
    }
}
