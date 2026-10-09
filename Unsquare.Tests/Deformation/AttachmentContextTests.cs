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
            new AttachmentContext(Identity, Box, CageScope.Building, Box, AttachmentOptions.Default).Affects(new float3(x, y, z));

        [Test]
        public void Affects_LotScope_AlwaysTrue()
        {
            var context = new AttachmentContext(Identity, Box, CageScope.Lot, Box, AttachmentOptions.Default);
            Assert.That(context.Affects(new float3(100, 0, -100)), Is.True);
        }
    
        [Test]
        public void Affects_BuildingScope_UsesMarginFromOptions()
        {
            var wide = new AttachmentOptions(true, true, true, true, PlacementMode.Follow, 6f, 3f);
            var anchor = new float3(7, 5, 0);
            Assert.That(new AttachmentContext(Identity, Box, CageScope.Building, Box, AttachmentOptions.Default)
                .Affects(anchor), Is.False);
            Assert.That(new AttachmentContext(Identity, Box, CageScope.Building, Box, wide).Affects(anchor), Is.True);
        }

        [Test]
        public void IsEnabled_ReflectsEachToggle()
        {
            var options = new AttachmentOptions(true, false, true, false, PlacementMode.Upright, 0f, 0f);
            Assert.That(options.IsEnabled(AttachmentKind.Props), Is.True);
            Assert.That(options.IsEnabled(AttachmentKind.Areas), Is.False);
            Assert.That(options.IsEnabled(AttachmentKind.Lanes), Is.True);
            Assert.That(options.IsEnabled(AttachmentKind.Nets), Is.False);
        }

        [Test]
        public void AttachmentOptions_NegativeDistances_Throw()
        {
            Assert.That(() => new AttachmentOptions(true, true, true, true, PlacementMode.Follow, -1f, 0f),
                Throws.ArgumentException);
            Assert.That(() => new AttachmentOptions(true, true, true, true, PlacementMode.Follow, 6f, -1f),
                Throws.ArgumentException);
        }
    }
}
