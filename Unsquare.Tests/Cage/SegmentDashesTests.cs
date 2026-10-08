using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class SegmentDashesTests
    {
        private readonly List<CageSegment> _dashes = new();

        [SetUp]
        public void SetUp() => _dashes.Clear();

        [Test]
        public void Split_EvenLength_GivesWholeDashesStartingAtA()
        {
            SegmentDashes.Split(new CageSegment(float3.zero, new float3(10, 0, 0)), 1f, 1f, _dashes);

            Assert.That(_dashes.Count, Is.EqualTo(5));
            Assert.That(_dashes[0].A, Is.EqualTo(float3.zero));
            foreach (var dash in _dashes)
                Assert.That(math.distance(dash.A, dash.B), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Split_EndsMidDash_ClipsLastDashAtB()
        {
            var b = new float3(0, 0, 4.5f);
            SegmentDashes.Split(new CageSegment(float3.zero, b), 1f, 1f, _dashes);

            Assert.That(_dashes.Count, Is.EqualTo(3));
            Assert.That(math.distance(_dashes[2].B, b), Is.LessThan(1e-4f));
            Assert.That(math.distance(_dashes[2].A, _dashes[2].B), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Split_VerticalSegment_DashesStayVertical()
        {
            SegmentDashes.Split(new CageSegment(new float3(2, 0, 3), new float3(2, 12, 3)), 1f, 0.6f, _dashes);

            Assert.That(_dashes, Is.Not.Empty);
            foreach (var dash in _dashes)
            {
                Assert.That(dash.A.xz, Is.EqualTo(new float2(2, 3)));
                Assert.That(dash.B.xz, Is.EqualTo(new float2(2, 3)));
                Assert.That(dash.B.y, Is.GreaterThan(dash.A.y));
            }
        }

        [Test]
        public void Split_ZeroLength_AddsNothing()
        {
            SegmentDashes.Split(new CageSegment(new float3(1), new float3(1)), 1f, 1f, _dashes);
            Assert.That(_dashes, Is.Empty);
        }

        [Test]
        public void Split_AppendsWithoutClearing()
        {
            var segment = new CageSegment(float3.zero, new float3(3, 0, 0));
            SegmentDashes.Split(segment, 1f, 1f, _dashes);
            SegmentDashes.Split(segment, 1f, 1f, _dashes);
            Assert.That(_dashes.Count, Is.EqualTo(4));
        }

        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(-1f, 1f)]
        public void Split_NonPositiveLengths_Throw(float dash, float gap)
        {
            var segment = new CageSegment(float3.zero, new float3(3, 0, 0));
            Assert.That(() => SegmentDashes.Split(segment, dash, gap, _dashes), Throws.ArgumentException);
        }
    }
}
