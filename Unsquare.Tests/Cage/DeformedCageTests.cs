using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class DeformedCageTests
    {
        private static readonly CageBox Box = new(new float3(-5, 0, -5), new float3(5, 10, 5));

        [Test]
        public void HandlePosition_Identity_EqualsRestPosition()
        {
            for (int f = 0; f < CageTopology.FaceCount; f++)
            {
                var handle = CageHandle.Face(f);
                Assert.That(DeformedCage.HandlePosition(Box, CageOffsets.Identity, handle),
                    Is.EqualTo(Box.HandlePosition(handle)));
            }
        }

        [Test]
        public void HandlePosition_FollowsMovedHandle()
        {
            var face = CageHandle.Face(3);
            var offsets = CageOffsets.Identity.Move(face, new float3(1, 2, 0));
            Assert.That(DeformedCage.HandlePosition(Box, offsets, face), Is.EqualTo(new float3(1, 12, 0)));
        }

        [Test]
        public void HandlePosition_EdgeMovesHalfWhenOneCornerMoves()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(1), new float3(2, 0, 0));
            var edge = CageHandle.Edge(0); // corners 0 and 1
            Assert.That(DeformedCage.HandlePosition(Box, offsets, edge), Is.EqualTo(new float3(1, 0, -5)));
        }
    }
}
