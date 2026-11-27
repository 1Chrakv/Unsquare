using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Automation;

namespace Unsquare.Tests.Automation
{
    public class ScriptedDragTests
    {
        [Test]
        public void MotionAt_Translation_GrowsLinearlyToTotal()
        {
            var drag = ScriptedDrag.Translate(new float3(0, 4, 0), 4);
            Assert.That(drag.MotionAt(1).Translation.y, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(drag.MotionAt(4).Translation.y, Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void MotionAt_Rotation_KeepsAxisAndGrowsAngle()
        {
            var drag = ScriptedDrag.Rotate(1, 90f, 2);
            var half = drag.MotionAt(1);
            Assert.That(half.IsRotation, Is.True);
            Assert.That(half.Axis, Is.EqualTo(1));
            Assert.That(half.Angle, Is.EqualTo(math.PI / 4).Within(1e-5f));
        }

        [Test]
        public void MotionAt_OutsideFrames_Throws()
        {
            var drag = ScriptedDrag.Translate(new float3(1, 0, 0), 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => drag.MotionAt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => drag.MotionAt(4));
        }

        [Test]
        public void CancelsAt_FromCancelFrameOn()
        {
            var drag = ScriptedDrag.Translate(new float3(1, 0, 0), 5, cancelAtFrame: 3);
            Assert.That(drag.CancelsAt(2), Is.False);
            Assert.That(drag.CancelsAt(3), Is.True);
            Assert.That(ScriptedDrag.Translate(new float3(1, 0, 0), 5).CancelsAt(5), Is.False);
        }

        [Test]
        public void Constructor_InvalidFrames_Throws()
        {
            Assert.Throws<ArgumentException>(() => ScriptedDrag.Translate(float3.zero, 0));
            Assert.Throws<ArgumentException>(() => ScriptedDrag.Translate(float3.zero, 2, cancelAtFrame: 3));
        }
    }
}
