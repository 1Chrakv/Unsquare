using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class AttachmentContextTests
    {
        private static readonly CageBox Box = new(new float3(-5, 0, -5), new float3(5, 10, 5));
        private static readonly IDeformation Identity = new TrilinearCageDeformation(Box, CageOffsets.Identity);

        [TestCase(0f, 5f, 0f, ExpectedResult = true)]
        [TestCase(5.3f, 10.4f, 0f, ExpectedResult = true)]
        [TestCase(8f, 0f, 0f, ExpectedResult = false)]
        [TestCase(0f, 0f, -12f, ExpectedResult = false)]
        public bool Affects_BuildingScope_OnlyInsideBoxWithMargin(float x, float y, float z) =>
            new AttachmentContext(Identity, Box, CageScope.Building, Box).Affects(new float3(x, y, z));

        [Test]
        public void Affects_LotScope_AlwaysTrue()
        {
            var context = new AttachmentContext(Identity, Box, CageScope.Lot, Box);
            Assert.That(context.Affects(new float3(100, 0, -100)), Is.True);
        }
    }
}
