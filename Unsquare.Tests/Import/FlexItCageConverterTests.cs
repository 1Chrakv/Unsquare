using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Import;

namespace Unsquare.Tests.Import
{
    public class FlexItCageConverterTests
    {
        private static readonly CageBox Lot = new(new float3(-8, 0, -12), new float3(8, 20, 12));

        // Grid rounding of the corners (0.05 m) blends into interior points at most as much.
        private const float Tolerance = 0.06f;

        [Test]
        public void ToOffsets_NothingGiven_IsIdentity()
        {
            Assert.That(FlexItCageConverter.ToOffsets(Lot, null, null).IsIdentity, Is.True);
        }

        [Test]
        public void ToOffsets_ZeroTaper_IsIdentity()
        {
            var taper = new FlexItTaper(0, 0, 0, 0, 6, 10, 10);
            Assert.That(FlexItCageConverter.ToOffsets(Lot, taper, null).IsIdentity, Is.True);
        }

        [Test]
        public void ToOffsets_FourAngleTaper_MatchesTaperInsideLot()
        {
            // FlexIt's half extents come from the meshes and differ from the lot; the map is still bilinear.
            var taper = new FlexItTaper(-15, 20, 10, -5, 6, 10, 9.5f);
            AssertMatches(FlexItCageConverter.ToOffsets(Lot, taper, null), p => taper.Map(p));
        }

        [Test]
        public void ToOffsets_ShearOnly_MatchesShear()
        {
            var shear = new FlexItShear(25, false, 11);
            AssertMatches(FlexItCageConverter.ToOffsets(Lot, null, shear), p => shear.Map(p));
        }

        [Test]
        public void ToOffsets_LeanFrontShear_MovesDepthWithWidth()
        {
            var shear = new FlexItShear(-30, true, 11);
            var offsets = FlexItCageConverter.ToOffsets(Lot, null, shear);
            AssertMatches(offsets, p => shear.Map(p));
            Assert.That(offsets.Meters(0).x, Is.EqualTo(0f));
            Assert.That(offsets.Meters(1).z, Is.EqualTo(8 * math.tan(math.radians(-30f))).Within(0.05f));
        }

        [Test]
        public void ToOffsets_TaperAndShear_AppliesShearAfterTaper()
        {
            var taper = new FlexItTaper(-20, 0, 0, 0, 7, 11, 10);
            var shear = new FlexItShear(20, false, 10.5f);
            AssertMatches(FlexItCageConverter.ToOffsets(Lot, taper, shear), p => shear.Map(taper.Map(p)));
        }

        [Test]
        public void ToOffsets_TopAndBottomCorners_MoveAlikeAndKeepHeight()
        {
            var offsets = FlexItCageConverter.ToOffsets(Lot, new FlexItTaper(10, -10, 5, 5, 6, 10, 9),
                new FlexItShear(12, false, 9));
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
            {
                Assert.That(offsets.Steps(corner).y, Is.EqualTo(0));
                Assert.That(offsets.Steps(corner), Is.EqualTo(offsets.Steps(corner | 2)));
            }
        }

        [Test]
        public void ToOffsets_ShearOnAppliedCage_ShearsTheDeformedBuilding()
        {
            var applied = CageOffsets.Identity.Move(CageHandle.Corner(7), new float3(2, 3, -1));
            var shear = new FlexItShear(15, false, 12);
            var offsets = FlexItCageConverter.ToOffsets(Lot, applied, null, shear);

            var before = new TrilinearCageDeformation(Lot, applied);
            var after = new TrilinearCageDeformation(Lot, offsets);
            var p = new float3(3, 14, 5);
            float3 deformed = before.Apply(p, out _);
            float2 want = shear.Map(deformed.xz);
            float3 got = after.Apply(p, out _);
            Assert.That(got.x, Is.EqualTo(want.x).Within(Tolerance));
            Assert.That(got.z, Is.EqualTo(want.y).Within(Tolerance));
            Assert.That(got.y, Is.EqualTo(deformed.y).Within(Tolerance));
        }

        [Test]
        public void TryFitOffsets_CornerFit_BeatsIdentityAndKeepsHeights()
        {
            var corner = new FlexItCorner(20, 1, 6, 6, 10, 2);
            var samples = PlanCageFit.Outline(new float2(-6, -10), new float2(6, 10), 8);
            Assert.That(FlexItCageConverter.TryFitOffsets(Lot, CageOffsets.Identity, samples, corner, null,
                out var offsets, out float error), Is.True);

            float identityError = 0f;
            foreach (var p in samples)
                identityError = math.max(identityError, math.distance(corner.Map(p), p));
            Assert.That(error, Is.LessThan(identityError));
            for (int i = 0; i < CageTopology.CornerCount; i++)
            {
                Assert.That(offsets.Steps(i).y, Is.EqualTo(0));
                Assert.That(offsets.Steps(i), Is.EqualTo(offsets.Steps(i | 2)));
            }
        }

        [Test]
        public void TryFitOffsets_CornerFit_TurnsTheCornerSideLikeFlexIt()
        {
            var corner = new FlexItCorner(20, 1, 6, 6, 10, 2);
            var samples = PlanCageFit.Outline(new float2(-6, -10), new float2(6, 10), 8);
            FlexItCageConverter.TryFitOffsets(Lot, CageOffsets.Identity, samples, corner, null, out var offsets, out _);

            // The far back corner lies in the turned wing: the cage moves it sideways the same way FlexIt does.
            var cage = new TrilinearCageDeformation(Lot, offsets);
            float2 back = new float2(-6, -10);
            float wanted = corner.Map(back).x - back.x;
            float got = cage.Apply(new float3(back.x, 0, back.y), out _).x - back.x;
            Assert.That(math.sign(got), Is.EqualTo(math.sign(wanted)));
        }

        private static void AssertMatches(CageOffsets offsets, System.Func<float2, float2> expected)
        {
            var cage = new TrilinearCageDeformation(Lot, offsets);
            for (float x = -8; x <= 8; x += 4)
            for (float z = -12; z <= 12; z += 6)
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
