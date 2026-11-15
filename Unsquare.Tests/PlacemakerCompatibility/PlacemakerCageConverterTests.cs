using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Import;
using Unsquare.Core.PlacemakerCompatibility;

namespace Unsquare.Tests.PlacemakerCompatibility
{
    public class PlacemakerCageConverterTests
    {
        private static readonly CageBox Lot = new(new float3(-8, 0, -12), new float3(8, 20, 12));

        // Grid rounding of the corners (0.05 m) blends into interior points at most as much.
        private const float Tolerance = 0.06f;

        [Test]
        public void ToOffsets_NoShifts_IsIdentity()
        {
            var cage = Cage(new float2(-8, -12), new float2(8, 12), 0, 0, 0, 0);
            Assert.That(PlacemakerCageConverter.ToOffsets(Lot, cage).IsIdentity, Is.True);
        }

        [Test]
        public void ToOffsets_SpanCoversLot_MatchesEverywhere()
        {
            var cage = Cage(new float2(-8, -14), new float2(8, 14), new float2(-1.5f, 0.5f), new float2(0, 2),
                new float2(0.5f, 0), new float2(2.2f, -1));
            Assert.That(cage.CoversDepth(Lot), Is.True);
            AssertMatches(PlacemakerCageConverter.ToOffsets(Lot, cage), cage.Map, -12, 12);
        }

        [Test]
        public void ToOffsets_ShorterSpan_ExactWithinSpan()
        {
            // A body from z -4 to 8 on the 24 m deep lot: the yard holds the edge shift, the body is bilinear.
            var cage = Cage(new float2(-8, -4), new float2(8, 8), new float2(-1, 0), new float2(0, 1),
                new float2(1, 0), new float2(0, -2));
            Assert.That(cage.CoversDepth(Lot), Is.False);
            AssertMatches(PlacemakerCageConverter.ToOffsets(Lot, cage), cage.Map, -4, 8);
        }

        [Test]
        public void ToOffsets_AnyShape_KeepsHeightsAndMovesColumnsAlike()
        {
            var cage = Cage(new float2(-6, -10), new float2(6, 10), new float2(1, 1), new float2(-1, 2),
                new float2(0.4f, -0.3f), new float2(-2, 0));
            var offsets = PlacemakerCageConverter.ToOffsets(Lot, cage);
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
            {
                Assert.That(offsets.Steps(corner).y, Is.EqualTo(0));
                Assert.That(offsets.Steps(corner), Is.EqualTo(offsets.Steps(corner | 2)));
            }
        }

        [Test]
        public void TryFitOffsets_BilinearShape_FitsExactly()
        {
            var cage = Cage(new float2(-8, -12), new float2(8, 12), new float2(-1, 0.5f), new float2(0.5f, 1),
                new float2(1, -0.5f), new float2(-0.5f, 0));
            var samples = PlanCageFit.Outline(new float2(-8, -12), new float2(8, 12), 8);
            Assert.That(PlacemakerCageConverter.TryFitOffsets(Lot, samples, cage, out var offsets, out float error),
                Is.True);
            Assert.That(error, Is.LessThan(1e-3f));
            AssertMatches(offsets, cage.Map, -12, 12);
        }

        [Test]
        public void TryFitOffsets_ClampedYard_BeatsIdentity()
        {
            var cage = Cage(new float2(-8, -4), new float2(8, 8), new float2(-2, 0), new float2(0, 2),
                new float2(2, 0), new float2(0, -2));
            var samples = PlanCageFit.Outline(new float2(-8, -12), new float2(8, 12), 8);
            Assert.That(PlacemakerCageConverter.TryFitOffsets(Lot, samples, cage, out _, out float error), Is.True);
            float identityError = 0f;
            foreach (var p in samples)
                identityError = math.max(identityError, math.distance(cage.Map(p), p));
            Assert.That(error, Is.LessThan(identityError));
        }

        [Test]
        public void Map_OutsideDepth_HoldsEdgeShift()
        {
            var cage = Cage(new float2(-8, -4), new float2(8, 8), 0, 0, new float2(1, 2), new float2(1, 2));
            Assert.That(cage.Map(new float2(0, 11)), Is.EqualTo(new float2(1, 13)));
            Assert.That(cage.MapUnclamped(new float2(0, 14)), Is.EqualTo(new float2(1.5f, 17)));
        }

        [TestCase("Placemaker Shape EU_Row01 d88aa31b 0_0.10_0", true)]
        [TestCase("EU_Row01", false)]
        [TestCase("PlacemakerShape EU_Row01", false)]
        [TestCase(null, false)]
        public void IsVariant_Name_RecognisesPrefix(string name, bool expected)
        {
            Assert.That(PlacemakerVariantName.IsVariant(name), Is.EqualTo(expected));
        }

        [Test]
        public void IsVariant_UnsquareVariantOfPlacemakerShape_IsFalse()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(7), new float3(1, 0, 0));
            string stacked = Unsquare.Core.Naming.VariantNaming.VariantName("Placemaker Shape EU_Row01 d88aa31b 0_0",
                offsets);
            Assert.That(PlacemakerVariantName.IsVariant(stacked), Is.False);
        }

        private static PlacemakerCage Cage(float2 min, float2 max, float2 backLeft, float2 backRight,
            float2 frontLeft, float2 frontRight) => new(min, max, backLeft, backRight, frontLeft, frontRight);

        private static void AssertMatches(CageOffsets offsets, System.Func<float2, float2> expected, float minZ,
            float maxZ)
        {
            var cage = new TrilinearCageDeformation(Lot, offsets);
            for (float x = -8; x <= 8; x += 4)
            for (float z = minZ; z <= maxZ; z += (maxZ - minZ) / 4)
            for (float y = 0; y <= 20; y += 10)
            {
                float3 deformed = cage.Apply(new float3(x, y, z), out _);
                float2 want = expected(new float2(x, z));
                Assert.That(deformed.x, Is.EqualTo(want.x).Within(Tolerance), $"x at {x},{y},{z}");
                Assert.That(deformed.z, Is.EqualTo(want.y).Within(Tolerance), $"z at {x},{y},{z}");
                Assert.That(deformed.y, Is.EqualTo(y).Within(1e-4f));
            }
        }
    }
}
