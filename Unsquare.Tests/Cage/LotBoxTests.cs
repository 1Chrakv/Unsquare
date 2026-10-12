using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Cage
{
    public class LotBoxTests
    {
        [Test]
        public void FromLot_SpansLotCellsAroundOrigin()
        {
            var mesh = new MinMaxBounds(new float3(-4, 0, -6), new float3(4, 9, 3));
            var box = LotBox.FromLot(new int2(3, 5), mesh);

            Assert.That(box.Min.xz, Is.EqualTo(new float2(-12, -20)));
            Assert.That(box.Max.xz, Is.EqualTo(new float2(12, 20)));
        }

        [Test]
        public void FromLot_HeightFromGroundToMeshTop()
        {
            var mesh = new MinMaxBounds(new float3(-4, 0.5f, -6), new float3(4, 9, 3));
            var box = LotBox.FromLot(new int2(2, 2), mesh);

            Assert.That(box.Min.y, Is.EqualTo(0f));
            Assert.That(box.Max.y, Is.EqualTo(9f));
        }

        [Test]
        public void FromLot_KeepsBasementBelowGround()
        {
            var mesh = new MinMaxBounds(new float3(-4, -3, -6), new float3(4, 9, 3));
            Assert.That(LotBox.FromLot(new int2(2, 2), mesh).Min.y, Is.EqualTo(-3f));
        }

        [Test]
        public void FromLot_FlatOrEmptyMesh_UsesMinimumHeight()
        {
            Assert.That(LotBox.FromLot(new int2(2, 2), MinMaxBounds.Empty).Max.y, Is.EqualTo(LotBox.MinHeight));
            var flat = new MinMaxBounds(new float3(-1, 0, -1), new float3(1, 0.2f, 1));
            Assert.That(LotBox.FromLot(new int2(2, 2), flat).Max.y, Is.EqualTo(LotBox.MinHeight));
        }

        [Test]
        public void FromLot_LowMesh_RaisesTopToMinimumHeight()
        {
            var kiosk = new MinMaxBounds(new float3(-2, 0, -2), new float3(2, 5, 2));
            Assert.That(LotBox.FromLot(new int2(2, 2), kiosk).Max.y, Is.EqualTo(8f));
        }

        [Test]
        public void FromLot_NonPositiveCells_Throws()
        {
            Assert.That(() => LotBox.FromLot(new int2(0, 2), MinMaxBounds.Empty), Throws.ArgumentException);
        }
    }
}
