using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class VertexDeformerTests
    {
        private static readonly CageBox Box = new(new float3(-5, 0, -5), new float3(5, 10, 5));

        // Shears the top face along x: a vertical wall leans over.
        private static readonly IDeformation Shear =
            new TrilinearCageDeformation(Box, CageOffsets.Identity.Move(CageHandle.Face(3), new float3(4, 0, 0)));

        [Test]
        public void Normal_StaysPerpendicularToDeformedSurface()
        {
            // Point on the +X wall: surface tangents are y and z, normal is +x.
            var p = new float3(5, 5, 1);
            var positions = new[] { p };
            var normals = new[] { new float3(1, 0, 0) };
            VertexDeformer.Deform(positions, normals, null, SubmeshTransform.Identity, Shear);

            Shear.Apply(p, out var j);
            float3 tangentY = math.mul(j, new float3(0, 1, 0));
            float3 tangentZ = math.mul(j, new float3(0, 0, 1));
            Assert.That(math.dot(normals[0], tangentY), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(math.dot(normals[0], tangentZ), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(math.length(normals[0]), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Tangent_FollowsJacobianAndKeepsHandedness()
        {
            var positions = new[] { new float3(5, 5, 1) };
            var tangents = new[] { new float4(0, 1, 0, -1) };
            VertexDeformer.Deform(positions, null, tangents, SubmeshTransform.Identity, Shear);

            Assert.That(tangents[0].w, Is.EqualTo(-1f));
            Assert.That(tangents[0].x, Is.GreaterThan(0f), "vertical tangent should lean towards +x");
            Assert.That(math.length(tangents[0].xyz), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void SubmeshTransform_IsAppliedAndReverted()
        {
            var transform = new SubmeshTransform(new float3(2, 0, -1), quaternion.RotateY(math.PI / 2));
            var local = new float3(1, 4, 0);
            var positions = new[] { local };
            VertexDeformer.Deform(positions, null, null, transform, Shear);

            float3 expected = transform.PointToLocal(Shear.Apply(transform.PointToBuilding(local), out _));
            Assert.That(TrilinearCageDeformationTests.Near(positions[0], expected), Is.True);
        }

        [Test]
        public void Identity_LeavesDataUnchangedAndReturnsBounds()
        {
            var identity = new TrilinearCageDeformation(Box, CageOffsets.Identity);
            var positions = new[] { new float3(-1, 0, 2), new float3(3, 4, -2) };
            var normals = new[] { new float3(0, 1, 0), new float3(1, 0, 0) };
            var bounds = VertexDeformer.Deform(positions, normals, null, SubmeshTransform.Identity, identity);

            Assert.That(TrilinearCageDeformationTests.Near(positions[1], new float3(3, 4, -2)), Is.True);
            Assert.That(TrilinearCageDeformationTests.Near(normals[0], new float3(0, 1, 0)), Is.True);
            Assert.That(TrilinearCageDeformationTests.Near(bounds.Min, new float3(-1, 0, -2)), Is.True);
            Assert.That(TrilinearCageDeformationTests.Near(bounds.Max, new float3(3, 4, 2)), Is.True);
        }

        [Test]
        public void MismatchedNormalArray_IsIgnored()
        {
            var positions = new[] { new float3(0, 1, 0), new float3(0, 2, 0) };
            var normals = new[] { new float3(1, 0, 0) };
            Assert.That(() => VertexDeformer.Deform(positions, normals, null, SubmeshTransform.Identity, Shear),
                Throws.Nothing);
            Assert.That(normals[0], Is.EqualTo(new float3(1, 0, 0)));
        }
    }
}
