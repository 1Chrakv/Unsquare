using System;
using NUnit.Framework;
using Unsquare.Core.Housekeeping;
using Unsquare.Core.Persistence;
using Unsquare.Tests.Persistence;

namespace Unsquare.Tests.Housekeeping
{
    public class PrunePlannerTests
    {
        private static readonly DateTime Now = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);

        private static string Asset(int i) => "UQ_" + i.ToString("x16");

        [Test]
        public void PlanPrune_OnlyExpiredEntriesWithoutCities()
        {
            var manifest = new VariantManifest();
            manifest.Register(VariantManifestTests.Entry("Old", Asset(1)), null, Now.AddDays(-30));
            manifest.Register(VariantManifestTests.Entry("Recent", Asset(2)), null, Now.AddDays(-2));
            manifest.Register(VariantManifestTests.Entry("OldButUsed", Asset(3)), "City", Now.AddDays(-30));

            var plan = PrunePlanner.PlanPrune(manifest, Now, MaxAge);

            Assert.That(plan.EntryKeys, Is.EqualTo(new[] { "BuildingPrefab/Old" }));
            Assert.That(plan.AssetNames, Is.EqualTo(new[] { Asset(1) }));
        }

        [Test]
        public void PlanPrune_KeepsAssetsSharedWithRemainingEntries()
        {
            var manifest = new VariantManifest();
            manifest.Register(VariantManifestTests.Entry("Old", Asset(1), Asset(2)), null, Now.AddDays(-30));
            manifest.Register(VariantManifestTests.Entry("Kept", Asset(2)), "City", Now);

            var plan = PrunePlanner.PlanPrune(manifest, Now, MaxAge);

            Assert.That(plan.AssetNames, Is.EqualTo(new[] { Asset(1) }));
        }
    }
}
