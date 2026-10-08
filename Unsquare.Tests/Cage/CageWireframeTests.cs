using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class CageWireframeTests
    {
        private static readonly CageBox Box = new(new float3(-5, 0, -5), new float3(5, 10, 5));

        [Test]
        public void Fill_Identity_EdgesHaveBoxLengths()
        {
            var edges = new CageSegment[CageTopology.EdgeCount];
            CageWireframe.Fill(Box, CageOffsets.Identity, edges);
            for (int e = 0; e < edges.Length; e++)
            {
                float expected = Box.Size[CageTopology.EdgeAxis(e)];
                Assert.That(math.distance(edges[e].A, edges[e].B), Is.EqualTo(expected).Within(1e-4f), $"edge {e}");
            }
        }

        [Test]
        public void Fill_FollowsMovedCorner()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(7), new float3(0, 3, 0));
            var edges = new CageSegment[CageTopology.EdgeCount];
            CageWireframe.Fill(Box, offsets, edges);
            // Edge 3 is the X edge through corners 6 and 7.
            Assert.That(edges[3].B, Is.EqualTo(new float3(5, 13, 5)));
            Assert.That(edges[3].A, Is.EqualTo(new float3(-5, 10, 5)));
        }

        [Test]
        public void Fill_WrongArrayLength_Throws()
        {
            Assert.That(() => CageWireframe.Fill(Box, CageOffsets.Identity, new CageSegment[3]), Throws.ArgumentException);
        }
    }
}
