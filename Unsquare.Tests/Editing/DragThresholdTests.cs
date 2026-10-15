using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class DragThresholdTests
    {
        private static readonly float3 Forward = new(0, 0, -1);

        [Test]
        public void IsExceeded_SmallTurn_ReturnsFalse()
        {
            Assert.That(DragThreshold.IsExceeded(Forward, new float3(0.001f, 0, -1), 0.01f), Is.False);
            Assert.That(DragThreshold.IsExceeded(Forward, Forward, 0.01f), Is.False);
        }

        [Test]
        public void IsExceeded_LargerTurn_ReturnsTrue()
        {
            Assert.That(DragThreshold.IsExceeded(Forward, new float3(0.02f, 0, -1), 0.01f), Is.True);
        }
    }
}
