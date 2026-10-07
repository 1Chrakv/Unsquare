using System;
using System.Collections.Generic;
using System.Linq;
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

        [Test]
        public void PlanOrphanSweep_DeletesOnlyUnclaimedOwnAssets()
        {
            var manifest = new VariantManifest();
            manifest.Register(VariantManifestTests.Entry("V", Asset(1)), "City", Now);
            var existing = new[] { Asset(1), Asset(2), "Foreign_Asset", Asset(3), Asset(4) };

            var plan = PrunePlanner.PlanOrphanSweep(manifest, existing);

            Assert.That(plan.IsRefused, Is.False);
            Assert.That(plan.AssetNames, Is.EqualTo(new[] { Asset(2), Asset(3), Asset(4) }));
        }

        [Test]
        public void PlanOrphanSweep_EntryWithoutAssets_Refuses()
        {
            var manifest = new VariantManifest();
            manifest.Register(VariantManifestTests.Entry("V"), "City", Now);

            var plan = PrunePlanner.PlanOrphanSweep(manifest, new[] { Asset(1) });

            Assert.That(plan.IsRefused, Is.True);
            Assert.That(plan.AssetNames, Is.Empty);
        }

        [Test]
        public void PlanOrphanSweep_MassDeletion_Refuses()
        {
            var manifest = new VariantManifest();
            manifest.Register(VariantManifestTests.Entry("V", Asset(0)), "City", Now);
            IEnumerable<string> existing = Enumerable.Range(0, 20).Select(Asset);

            var plan = PrunePlanner.PlanOrphanSweep(manifest, existing);

            Assert.That(plan.IsRefused, Is.True);
        }

        [Test]
        public void PlanOrphanSweep_FewOrphans_AllowedEvenIfMajority()
        {
            var manifest = new VariantManifest();
            manifest.Register(VariantManifestTests.Entry("V", Asset(0)), "City", Now);
            IEnumerable<string> existing = Enumerable.Range(0, 5).Select(Asset);

            var plan = PrunePlanner.PlanOrphanSweep(manifest, existing);

            Assert.That(plan.IsRefused, Is.False);
            Assert.That(plan.AssetNames.Count, Is.EqualTo(4));
        }
    }
}
