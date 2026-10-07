using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class CageBoxTests
    {
        private static readonly CageBox Box = new(new float3(-5, 0, -10), new float3(5, 20, 10));

        [Test]
        public void Corner_UsesMinOrMaxPerAxis()
        {
            Assert.That(Box.Corner(0), Is.EqualTo(new float3(-5, 0, -10)));
            Assert.That(Box.Corner(3), Is.EqualTo(new float3(5, 20, -10)));
            Assert.That(Box.Corner(7), Is.EqualTo(new float3(5, 20, 10)));
        }

        [Test]
        public void HandlePosition_FaceIsFaceCenter()
        {
            Assert.That(Box.HandlePosition(CageHandle.Face(3)), Is.EqualTo(new float3(0, 20, 0)));
            Assert.That(Box.HandlePosition(CageHandle.Face(4)), Is.EqualTo(new float3(0, 10, -10)));
        }

        [Test]
        public void HandlePosition_EdgeIsEdgeMidpoint()
        {
            var edge = CageHandle.Edge(0); // X edge through corners 0 and 1
            Assert.That(Box.HandlePosition(edge), Is.EqualTo(new float3(0, 0, -10)));
        }

        [Test]
        public void Normalize_MapsMinToZeroAndMaxToOne()
        {
            Assert.That(Box.Normalize(Box.Min), Is.EqualTo(float3.zero));
            Assert.That(Box.Normalize(Box.Max), Is.EqualTo(new float3(1)));
        }

        [Test]
        public void Constructor_FlatBox_Throws()
        {
            Assert.That(() => new CageBox(float3.zero, new float3(1, 0, 1)), Throws.ArgumentException);
        }
    }
}
