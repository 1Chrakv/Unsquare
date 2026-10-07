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
    }
}
