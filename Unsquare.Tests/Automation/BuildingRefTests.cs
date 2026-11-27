using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Automation;

namespace Unsquare.Tests.Automation
{
    public class BuildingRefTests
    {
        [Test]
        public void TryParse_PrefabAtPosition_ReadsBoth()
        {
            Assert.That(BuildingRef.TryParse("EU_ResidentialLow01@-12.5,340", out var reference), Is.True);
            Assert.That(reference.Prefab, Is.EqualTo("EU_ResidentialLow01"));
            Assert.That(reference.Position.x, Is.EqualTo(-12.5f));
            Assert.That(reference.Position.y, Is.EqualTo(340f));
        }

        [Test]
        public void TryParse_PrefabWithAtSign_SplitsAtLastOne()
        {
            Assert.That(BuildingRef.TryParse("Odd@Name@1,2", out var reference), Is.True);
            Assert.That(reference.Prefab, Is.EqualTo("Odd@Name"));
        }

        [TestCase("")]
        [TestCase("NoPosition")]
        [TestCase("@1,2")]
        [TestCase("Prefab@1")]
        [TestCase("Prefab@x,2")]
        public void TryParse_Malformed_Fails(string text)
        {
            Assert.That(BuildingRef.TryParse(text, out _), Is.False);
        }

        [Test]
        public void ToString_RoundTripsThroughTryParse()
        {
            var reference = new BuildingRef("Hospital01", new float2(10.04f, -3.96f));
            Assert.That(reference.ToString(), Is.EqualTo("Hospital01@10.0,-4.0"));
            Assert.That(BuildingRef.TryParse(reference.ToString(), out var parsed), Is.True);
            Assert.That(parsed.Prefab, Is.EqualTo("Hospital01"));
        }

        [Test]
        public void FindIn_SeveralOfThePrefab_TakesNearestWithinTolerance()
        {
            var reference = new BuildingRef("House", new float2(10, 10));
            var candidates = new (string, float2)[]
            {
                ("House", new float2(11.5f, 10)),
                ("Shop", new float2(10, 10)),
                ("House", new float2(10.3f, 10)),
                ("House", new float2(100, 10)),
            };
            Assert.That(reference.FindIn(candidates), Is.EqualTo(2));
        }

        [Test]
        public void FindIn_VariantOfThePrefab_Matches()
        {
            var reference = new BuildingRef("House", new float2(0, 0));
            var candidates = new (string, float2)[] { ("House__uq0123456789ab", new float2(0.5f, 0)) };
            Assert.That(reference.FindIn(candidates), Is.EqualTo(0));
        }

        [Test]
        public void FindIn_NothingWithinTolerance_ReturnsMinusOne()
        {
            var reference = new BuildingRef("House", new float2(0, 0));
            var candidates = new (string, float2)[] { ("House", new float2(5, 0)) };
            Assert.That(reference.FindIn(candidates), Is.EqualTo(-1));
            Assert.That(reference.FindIn(candidates, tolerance: 6f), Is.EqualTo(0));
        }
    }
}
