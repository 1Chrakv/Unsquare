using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class ExtensionCageTests
    {
        private static readonly CageBox Owner = new(new float3(-10, 0, -8), new float3(10, 20, 8));

        // Saved extension variant names depend on these answers.
        [TestCase(0f, 20f, 0f, true)] // storey on the roof
        [TestCase(9f, 0f, 7f, true)] // wing just inside the footprint
        [TestCase(10f, 0f, 8f, true)] // centre on the footprint corner
        [TestCase(14f, 0f, 0f, false)] // car park beside the building
        [TestCase(0f, 0f, -12f, false)] // helipad behind it
        public void FollowsBuilding_ByMeshCentreOverFootprint(float x, float y, float z, bool expected)
        {
            var mesh = new CageBox(new float3(-2, 0, -2), new float3(2, 3, 2));
            Assert.That(ExtensionCage.FollowsBuilding(mesh, new float3(x, y, z), Owner), Is.EqualTo(expected));
        }

        [Test]
        public void FollowsBuilding_OffCentreMesh_UsesMeshCentre()
        {
            var mesh = new CageBox(new float3(10, 0, -1), new float3(14, 3, 1));
            Assert.That(ExtensionCage.FollowsBuilding(mesh, float3.zero, Owner), Is.False);
        }
    }
}
