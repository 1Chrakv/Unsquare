using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class CageOffsetsTests
    {
        [Test]
        public void Identity_IsIdentity()
        {
            Assert.That(CageOffsets.Identity.IsIdentity, Is.True);
        }

        [Test]
        public void Move_Corner_OnlyMovesThatCorner()
        {
            var moved = CageOffsets.Identity.Move(CageHandle.Corner(6), new float3(0, 1, 0));
            for (int i = 0; i < CageTopology.CornerCount; i++)
            {
                var expected = i == 6 ? new int3(0, 10, 0) : int3.zero;
                Assert.That(moved.Steps(i), Is.EqualTo(expected), $"corner {i}");
            }
        }

        [Test]
        public void Move_Face_MovesItsFourCorners()
        {
            var face = CageHandle.Face(5); // +Z
            var moved = CageOffsets.Identity.Move(face, new float3(0, 0, 0.5f));
            var faceCorners = CageTopology.CornersOf(face);
            for (int i = 0; i < CageTopology.CornerCount; i++)
            {
                var expected = faceCorners.Contains(i) ? new int3(0, 0, 5) : int3.zero;
                Assert.That(moved.Steps(i), Is.EqualTo(expected), $"corner {i}");
            }
        }

        [Test]
        public void Move_Edge_MovesItsTwoCorners()
        {
            var moved = CageOffsets.Identity.Move(CageHandle.Edge(4), new float3(-0.3f, 0, 0));
            Assert.That(Enumerable.Range(0, 8).Count(i => !moved.Steps(i).Equals(int3.zero)), Is.EqualTo(2));
        }

        [Test]
        public void Move_BelowHalfStep_ReturnsSameInstance()
        {
            var same = CageOffsets.Identity.Move(CageHandle.Corner(0), new float3(0.04f));
            Assert.That(same, Is.SameAs(CageOffsets.Identity));
        }

        [Test]
        public void Move_IsImmutable()
        {
            CageOffsets.Identity.Move(CageHandle.Corner(1), new float3(1));
            Assert.That(CageOffsets.Identity.IsIdentity, Is.True);
        }

        [Test]
        public void ToArray_FromArray_RoundTrips()
        {
            var offsets = CageOffsets.Identity
                .Move(CageHandle.Corner(2), new float3(1.2f, -0.4f, 3f))
                .Move(CageHandle.Face(0), new float3(0.7f, 0, 0));
            var copy = CageOffsets.FromArray(offsets.ToArray());
            Assert.That(copy, Is.EqualTo(offsets));
            Assert.That(copy.GetHashCode(), Is.EqualTo(offsets.GetHashCode()));
        }

        [Test]
        public void FromMeters_QuantizesToStep()
        {
            var meters = new float3[8];
            meters[3] = new float3(0.26f, -0.14f, 1f);
            var offsets = CageOffsets.FromMeters(meters);
            Assert.That(offsets.Steps(3), Is.EqualTo(new int3(3, -1, 10)));
        }

        [Test]
        public void ToCanonicalString_IsStable()
        {
            var offsets = CageOffsets.Identity.WithCornerSteps(1, new int3(1, -2, 3));
            Assert.That(offsets.ToCanonicalString(),
                Is.EqualTo("0,0,0;1,-2,3;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0;0,0,0"));
        }

        [Test]
        public void FromArray_WrongLength_Throws()
        {
            Assert.That(() => CageOffsets.FromArray(new int[5]), Throws.ArgumentException);
        }
    }
}
