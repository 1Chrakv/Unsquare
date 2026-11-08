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
        [TestCase(9f, 0f, 7f, true)] // flat piece just inside the footprint
        [TestCase(10f, 0f, 8f, true)] // centre on the footprint corner
        [TestCase(14f, 0f, 0f, false)] // car park beside the building
        [TestCase(0f, 0f, -12f, false)] // helipad behind it
        public void FollowsBuilding_FlatPiece_ByMeshCentreOverFootprint(float x, float y, float z, bool expected)
        {
            var mesh = new CageBox(new float3(-2, 0, -2), new float3(2, 1, 2));
            Assert.That(ExtensionCage.FollowsBuilding(mesh, new float3(x, y, z), Owner), Is.EqualTo(expected));
        }

        [Test]
        public void FollowsBuilding_OffCentreFlatMesh_UsesMeshCentre()
        {
            var mesh = new CageBox(new float3(10, 0, -1), new float3(14, 1, 1));
            Assert.That(ExtensionCage.FollowsBuilding(mesh, float3.zero, Owner), Is.False);
        }

        [Test]
        public void FollowsBuilding_TallWingBeside_FollowsBuilding()
        {
            var wing = new CageBox(new float3(-24, 0, -6), new float3(-10, 12, 6));
            Assert.That(ExtensionCage.FollowsBuilding(wing, float3.zero, Owner), Is.True);
        }

        [Test]
        public void FollowsBuilding_JustBelowStructureHeight_FollowsLot()
        {
            var shed = new CageBox(new float3(12, 0, -2), new float3(16, ExtensionCage.StructureMinHeight - 0.1f, 2));
            Assert.That(ExtensionCage.FollowsBuilding(shed, float3.zero, Owner), Is.False);
        }

        [Test]
        public void FollowsBuilding_RaisedByPosition_CountsTop()
        {
            var deck = new CageBox(new float3(-2, 0, -2), new float3(2, 1, 2));
            Assert.That(ExtensionCage.FollowsBuilding(deck, new float3(14, 5, 0), Owner), Is.True);
        }
    }
}
