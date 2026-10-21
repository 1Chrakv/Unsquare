using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Import;

namespace Unsquare.Tests.Import
{
    public class FlexItCornerTests
    {
        // Corner at the front right (+x, +z) of a 16 × 24 m building; narrow blend band.
        private const float CornerX = 8, PivotZ = 12, HalfWidth = 8, BlendHalf = 2;

        private static FlexItCorner Corner(float degrees) =>
            new(degrees, 1, HalfWidth, CornerX, PivotZ, BlendHalf);

        [Test]
        public void Map_ZeroAngle_IsIdentity()
        {
            var p = new float2(-3, 5);
            Assert.That(math.distance(Corner(0).Map(p), p), Is.LessThan(1e-5f));
        }

        [Test]
        public void Map_DeepInTurnedWing_RotatesAboutCornerAndShearsBack()
        {
            float degrees = 20, radians = math.radians(degrees);
            var p = new float2(6, -10); // near the corner side, far behind the front
            var d = p - new float2(CornerX, PivotZ);
            var rotated = new float2(math.cos(radians) * d.x + math.sin(radians) * d.y,
                math.cos(radians) * d.y - math.sin(radians) * d.x);
            var expected = new float2(rotated.x + CornerX - rotated.y * math.tan(radians), rotated.y + PivotZ);
            Assert.That(math.distance(Corner(degrees).Map(p), expected), Is.LessThan(1e-4f));
        }

        [Test]
        public void Map_DeepInTaperedPart_LeansTheFarEnd()
        {
            float degrees = 20;
            var p = new float2(-8, 11); // far end, at the front
            float lean = -math.tan(math.radians(degrees));
            var expected = new float2(p.x + (p.y - PivotZ) * lean, p.y);
            Assert.That(math.distance(Corner(degrees).Map(p), expected), Is.LessThan(1e-4f));
        }

        [Test]
        public void Map_AcrossBlendBand_IsContinuous()
        {
            var corner = Corner(25);
            var previous = corner.Map(new float2(0, 4));
            for (float x = 0.05f; x <= 8f; x += 0.05f)
            {
                var next = corner.Map(new float2(x, 4 - x));
                Assert.That(math.distance(next, previous), Is.LessThan(0.3f), $"jump at x = {x}");
                previous = next;
            }
        }
    }
}
