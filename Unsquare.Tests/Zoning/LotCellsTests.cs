using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Zoning;

namespace Unsquare.Tests.Zoning
{
    public class LotCellsTests
    {
        private static readonly float2 Position = new(100f, -40f);

        [Test]
        public void CellCenter_FirstCellUnrotated_IsFrontPlusXCorner()
        {
            var center = LotCells.CellCenter(Position, quaternion.identity, new int2(2, 3), int2.zero);
            Assert.That(center.x, Is.EqualTo(104f).Within(1e-4f));
            Assert.That(center.y, Is.EqualTo(-32f).Within(1e-4f));
        }

        [Test]
        public void CellCenter_OddLot_MiddleCellIsPosition()
        {
            var center = LotCells.CellCenter(Position, quaternion.RotateY(0.7f), new int2(3, 5), new int2(1, 2));
            Assert.That(math.distance(center, Position), Is.LessThan(1e-4f));
        }

        [Test]
        public void CellCenter_Rotated90_FollowsBuildingAxes()
        {
            // RotateY(90°) turns local +X into world −Z and local +Z into world +X.
            var center = LotCells.CellCenter(Position, quaternion.RotateY(math.PI / 2f), new int2(2, 1), int2.zero);
            Assert.That(center.x, Is.EqualTo(100f).Within(1e-4f));
            Assert.That(center.y, Is.EqualTo(-44f).Within(1e-4f));
        }

        [Test]
        public void CellCenter_NeighbourCells_AreOneCellApart()
        {
            var rotation = quaternion.RotateY(1.1f);
            var lot = new int2(4, 3);
            var a = LotCells.CellCenter(Position, rotation, lot, new int2(1, 1));
            Assert.That(math.distance(a, LotCells.CellCenter(Position, rotation, lot, new int2(2, 1))),
                Is.EqualTo(LotCells.CellSize).Within(1e-4f));
            Assert.That(math.distance(a, LotCells.CellCenter(Position, rotation, lot, new int2(1, 2))),
                Is.EqualTo(LotCells.CellSize).Within(1e-4f));
        }

        [Test]
        public void Bounds_Unrotated_SpansLot()
        {
            LotCells.Bounds(Position, quaternion.identity, new int2(2, 3), out var min, out var max);
            Assert.That(min.x, Is.EqualTo(92f).Within(1e-4f));
            Assert.That(max.x, Is.EqualTo(108f).Within(1e-4f));
            Assert.That(min.y, Is.EqualTo(-52f).Within(1e-4f));
            Assert.That(max.y, Is.EqualTo(-28f).Within(1e-4f));
        }

        [Test]
        public void Bounds_Rotated_ContainsAllCellCenters()
        {
            var rotation = quaternion.RotateY(0.4f);
            var lot = new int2(3, 4);
            LotCells.Bounds(Position, rotation, lot, out var min, out var max);
            for (int y = 0; y < lot.y; y++)
            for (int x = 0; x < lot.x; x++)
            {
                var c = LotCells.CellCenter(Position, rotation, lot, new int2(x, y));
                Assert.That(math.all(c > min & c < max), Is.True, $"cell {x},{y}");
            }
        }

        [Test]
        public void CellCenter_EmptyLot_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                LotCells.CellCenter(Position, quaternion.identity, int2.zero, int2.zero));
        }
    }
}
