using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class CageKernelTests
    {
        private static readonly CageBox Box = new(new float3(-6, 0, -8), new float3(6, 15, 8));
        private static readonly float3 Offset = new(1.5f, 12f, -2f);

        private static readonly CageOffsets Skewed = CageOffsets.Identity
            .Move(CageHandle.Corner(7), new float3(2f, 3f, -1f))
            .Move(CageHandle.Corner(2), new float3(-1f, 0.5f, 2f))
            .Move(CageHandle.Face(3), new float3(1.5f, 0f, 0.5f));

        private static readonly float3[] Points =
        {
            new(0.3f, 2f, -1f), new(-6f, 0f, -8f), new(6f, 15f, 8f), new(9f, 20f, -11f), new(-2.5f, 7.25f, 3f),
        };

        [Test]
        public void Apply_AtCorner_MovesByThatCornersOffset()
        {
            var kernel = new CageKernel(Box, Skewed);
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
            {
                float3 moved = kernel.Apply(Box.Corner(corner), out _);
                Assert.That(math.distance(moved, Box.Corner(corner) + Skewed.Meters(corner)), Is.LessThan(1e-4f),
                    $"corner {corner}");
            }
        }

        [Test]
        public void Apply_Jacobian_MatchesFiniteDifferences()
        {
            var kernel = new CageKernel(Box, Skewed);
            const float h = 1e-2f;
            foreach (var p in Points)
            {
                kernel.Apply(p, out var jacobian);
                for (int axis = 0; axis < 3; axis++)
                {
                    float3 step = float3.zero;
                    step[axis] = h;
                    float3 slope = (kernel.Apply(p + step, out _) - kernel.Apply(p - step, out _)) / (2f * h);
                    Assert.That(math.distance(jacobian[axis], slope), Is.LessThan(1e-2f), $"{p}, axis {axis}");
                }
            }
        }

        [Test]
        public void TryFrom_TrilinearCage_GivesItsKernel()
        {
            var cage = new TrilinearCageDeformation(Box, Skewed);

            Assert.That(CageKernel.TryFrom(cage, out var kernel), Is.True);
            foreach (var p in Points)
            {
                float3 expected = cage.Apply(p, out var expectedJacobian);
                Assert.That(kernel.Apply(p, out var jacobian), Is.EqualTo(expected));
                Assert.That(jacobian, Is.EqualTo(expectedJacobian));
            }
        }

        [Test]
        public void TryFrom_TranslatedCage_MatchesTheTranslatedDeformation()
        {
            var translated = new TranslatedDeformation(new TrilinearCageDeformation(Box, Skewed), Offset);

            Assert.That(CageKernel.TryFrom(translated, out var kernel), Is.True);
            foreach (var p in Points)
            {
                float3 expected = translated.Apply(p, out var expectedJacobian);
                Assert.That(kernel.Apply(p, out var jacobian), Is.EqualTo(expected));
                Assert.That(jacobian, Is.EqualTo(expectedJacobian));
            }
        }

        [Test]
        public void TryFrom_TwiceTranslatedCage_AddsTheShifts()
        {
            var inner = new TranslatedDeformation(new TrilinearCageDeformation(Box, Skewed), Offset);
            var outer = new TranslatedDeformation(inner, new float3(-3f, 1f, 4f));

            Assert.That(CageKernel.TryFrom(outer, out var kernel), Is.True);
            foreach (var p in Points)
                Assert.That(math.distance(kernel.Apply(p, out _), outer.Apply(p, out _)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TryFrom_BlendedDeformation_IsRefused()
        {
            var building = new TrilinearCageDeformation(Box, Skewed);
            var lot = new TrilinearCageDeformation(Box, CageOffsets.Identity);
            var footprint = new CageBox(new float3(-3, 0, -4), new float3(3, 15, 4));
            var blend = new BlendedDeformation(building, lot, footprint, CageBlend.Ring(Box, footprint,
                new DeformSpec(Skewed, false)));

            Assert.That(CageKernel.TryFrom(blend, out _), Is.False);
        }

        [Test]
        public void DeformVertex_MatchesArrayDeform()
        {
            var cage = new TrilinearCageDeformation(Box, Skewed);
            var transform = new SubmeshTransform(new float3(1f, 2f, -3f), quaternion.RotateY(0.7f));
            foreach (var p in Points)
            {
                var positions = new[] { p };
                var normals = new[] { math.normalize(new float3(1f, 2f, -0.5f)) };
                var tangents = new[] { new float4(math.normalize(new float3(-2f, 1f, 0f)), -1f) };
                float3 position = p, normal = normals[0];
                float4 tangent = tangents[0];

                VertexDeformer.Deform(positions, normals, tangents, transform, cage);
                VertexDeformer.DeformVertex(cage.Kernel, transform, ref position, ref normal, ref tangent, true, true);

                Assert.That(position, Is.EqualTo(positions[0]));
                Assert.That(normal, Is.EqualTo(normals[0]));
                Assert.That(tangent, Is.EqualTo(tangents[0]));
            }
        }

        [Test]
        public void DeformVertex_WithoutNormalAndTangent_LeavesThemAlone()
        {
            var kernel = new CageKernel(Box, Skewed);
            float3 position = Points[0], normal = new(0f, 1f, 0f);
            var tangent = new float4(1f, 0f, 0f, 1f);

            VertexDeformer.DeformVertex(kernel, SubmeshTransform.Identity, ref position, ref normal, ref tangent,
                false, false);

            Assert.That(normal, Is.EqualTo(new float3(0f, 1f, 0f)));
            Assert.That(tangent, Is.EqualTo(new float4(1f, 0f, 0f, 1f)));
            Assert.That(position, Is.Not.EqualTo(Points[0]));
        }
    }
}
