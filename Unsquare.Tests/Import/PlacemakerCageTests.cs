using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Import;

namespace Unsquare.Tests.Import
{
    public class PlacemakerCageTests
    {
        private static readonly CageBox Lot = new(new float3(-8, 0, -12), new float3(8, 20, 12));

        private static PlacemakerCage Cage(float2 min, float2 max, float2 shift) =>
            new(min, max, shift, -shift, shift, -shift);

        [Test]
        public void IsPlausible_TypicalBend_IsTrue()
        {
            Assert.That(Cage(new float2(-8, -12), new float2(8, 12), new float2(1.5f, -0.5f)).IsPlausible(Lot),
                Is.True);
        }

        [Test]
        public void IsPlausible_InvertedSpan_IsFalse()
        {
            Assert.That(Cage(new float2(8, -12), new float2(-8, 12), float2.zero).IsPlausible(Lot), Is.False);
        }

        [Test]
        public void IsPlausible_SpanBesideLot_IsFalse()
        {
            Assert.That(Cage(new float2(20, -12), new float2(30, 12), float2.zero).IsPlausible(Lot), Is.False);
        }

        [Test]
        public void IsPlausible_ShiftInCentimetres_IsFalse()
        {
            // A 1.5 m shift read as 150: what a change of units would look like.
            Assert.That(Cage(new float2(-8, -12), new float2(8, 12), new float2(150, 0)).IsPlausible(Lot), Is.False);
        }

        [Test]
        public void IsPlausible_NaNShift_IsFalse()
        {
            Assert.That(Cage(new float2(-8, -12), new float2(8, 12), new float2(float.NaN, 0)).IsPlausible(Lot),
                Is.False);
        }
    }
}
