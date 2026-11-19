using System;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Deformation;
using Unsquare.Core.Persistence;

namespace Unsquare.Tests.Persistence
{
    public class BoundsCacheTests
    {
        [Test]
        public void SaveAndLoad_RoundTrips()
        {
            using var temp = new TempDirectory();
            string path = temp.File("bounds.json");
            var cache = new BoundsCache();
            cache.Set("UQ_a", new MinMaxBounds(new float3(-1.5f, 0, -2), new float3(1.5f, 10.25f, 2)));
            cache.Save(path);

            Assert.That(BoundsCache.Load(path, out var loaded), Is.EqualTo(JsonReadStatus.Ok));
            Assert.That(loaded.TryGet("UQ_a", out var bounds), Is.True);
            Assert.That(bounds.Min, Is.EqualTo(new float3(-1.5f, 0, -2)));
            Assert.That(bounds.Max, Is.EqualTo(new float3(1.5f, 10.25f, 2)));
        }

        [Test]
        public void Set_EmptyBounds_Throws()
        {
            Assert.That(() => new BoundsCache().Set("UQ_a", MinMaxBounds.Empty), Throws.ArgumentException);
        }

        [Test]
        public void Remove_UnknownKey_ReturnsFalseAndStaysClean()
        {
            var cache = new BoundsCache();
            Assert.That(cache.Remove("nope"), Is.False);
            Assert.That(cache.IsDirty, Is.False);
        }

        [Test]
        public void SaveAndLoad_WithLastUse_RoundTripsWithinMinutes()
        {
            using var temp = new TempDirectory();
            string path = temp.File("bounds.json");
            var cache = new BoundsCache();
            cache.Set("UQ_a", Box);
            cache.Touch("UQ_a", Now);
            cache.Save(path);

            BoundsCache.Load(path, out var loaded);
            Assert.That(loaded.TryGetLastUsed("UQ_a", out var lastUsed), Is.True);
            Assert.That(Math.Abs((lastUsed - Now).TotalMinutes), Is.LessThan(5));
            Assert.That(loaded.TryGet("UQ_a", out var bounds), Is.True);
            Assert.That(bounds.Max, Is.EqualTo(Box.Max));
        }

        [Test]
        public void Load_SixElementEntry_HasNoLastUse()
        {
            using var temp = new TempDirectory();
            string path = temp.File("bounds.json");
            File.WriteAllText(path, "{\"UQ_a\":[0,0,0,1,1,1]}");

            BoundsCache.Load(path, out var cache);
            Assert.That(cache.TryGet("UQ_a", out _), Is.True);
            Assert.That(cache.TryGetLastUsed("UQ_a", out _), Is.False);
        }

        [Test]
        public void Touch_UnknownAsset_DoesNothingAndStaysClean()
        {
            var cache = new BoundsCache();
            cache.Touch("UQ_a", Now);
            Assert.That(cache.TryGetLastUsed("UQ_a", out _), Is.False);
            Assert.That(cache.IsDirty, Is.False);
        }

        [Test]
        public void Touch_WithinTheHour_StaysCleanAndAfterwardsIsDirty()
        {
            using var temp = new TempDirectory();
            var cache = new BoundsCache();
            cache.Set("UQ_a", Box);
            cache.Touch("UQ_a", Now);
            cache.Save(temp.File("bounds.json"));

            cache.Touch("UQ_a", Now.AddMinutes(30));
            Assert.That(cache.IsDirty, Is.False);
            cache.Touch("UQ_a", Now.AddHours(2));
            Assert.That(cache.IsDirty, Is.True);
            cache.TryGetLastUsed("UQ_a", out var lastUsed);
            Assert.That(Math.Abs((lastUsed - Now.AddHours(2)).TotalMinutes), Is.LessThan(5));
        }

        [Test]
        public void Set_ExistingEntry_KeepsLastUse()
        {
            var cache = new BoundsCache();
            cache.Set("UQ_a", Box);
            cache.Touch("UQ_a", Now);
            cache.Set("UQ_a", Box);
            Assert.That(cache.TryGetLastUsed("UQ_a", out _), Is.True);
        }

        private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        private static readonly MinMaxBounds Box = new(new float3(0, 0, 0), new float3(1, 2, 3));
    }
}
