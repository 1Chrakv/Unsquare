using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class CageTopologyTests
    {
        [Test]
        public void Edges_EachConnectsTwoCornersDifferingOnlyOnItsAxis()
        {
            var seen = new HashSet<(int, int)>();
            for (int e = 0; e < CageTopology.EdgeCount; e++)
            {
                var c = CageTopology.CornersOf(CageHandle.Edge(e));
                Assert.That(c.Count, Is.EqualTo(2));
                Assert.That(c[0] ^ c[1], Is.EqualTo(1 << CageTopology.EdgeAxis(e)));
                Assert.That(seen.Add((c[0], c[1])), Is.True, "duplicate edge");
            }
        }

        [Test]
        public void Faces_EachHasFourCornersOnTheSameSide()
        {
            for (int f = 0; f < CageTopology.FaceCount; f++)
            {
                var c = CageTopology.CornersOf(CageHandle.Face(f));
                int axis = CageTopology.FaceAxis(f);
                Assert.That(c.Count, Is.EqualTo(4));
                Assert.That(c.All(i => CageTopology.CornerSide(i)[axis] == CageTopology.FaceSide(f)), Is.True);
            }
        }

        [Test]
        public void EveryCorner_BelongsToThreeEdgesAndThreeFaces()
        {
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
            {
                int edges = Enumerable.Range(0, CageTopology.EdgeCount)
                    .Count(e => CageTopology.CornersOf(CageHandle.Edge(e)).Contains(corner));
                int faces = Enumerable.Range(0, CageTopology.FaceCount)
                    .Count(f => CageTopology.CornersOf(CageHandle.Face(f)).Contains(corner));
                Assert.That(edges, Is.EqualTo(3));
                Assert.That(faces, Is.EqualTo(3));
            }
        }

        [Test]
        public void CornerSide_MatchesIndexBits()
        {
            Assert.That(CageTopology.CornerSide(0), Is.EqualTo(new int3(0, 0, 0)));
            Assert.That(CageTopology.CornerSide(5), Is.EqualTo(new int3(1, 0, 1)));
            Assert.That(CageTopology.CornerSide(7), Is.EqualTo(new int3(1, 1, 1)));
        }

        [Test]
        public void Handle_InvalidIndex_Throws()
        {
            Assert.That(() => CageHandle.Corner(8), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => CageHandle.Edge(-1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => CageHandle.Face(6), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
