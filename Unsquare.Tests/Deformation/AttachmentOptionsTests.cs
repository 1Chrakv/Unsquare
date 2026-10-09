using NUnit.Framework;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class AttachmentOptionsTests
    {
        [Test]
        public void IsEnabled_ReflectsEachToggle()
        {
            var options = new AttachmentOptions(true, false, true, false, PlacementMode.Upright, 0f);
            Assert.That(options.IsEnabled(AttachmentKind.Props), Is.True);
            Assert.That(options.IsEnabled(AttachmentKind.Areas), Is.False);
            Assert.That(options.IsEnabled(AttachmentKind.Lanes), Is.True);
            Assert.That(options.IsEnabled(AttachmentKind.Nets), Is.False);
        }

        [Test]
        public void AttachmentOptions_NegativeReach_Throws()
        {
            Assert.That(() => new AttachmentOptions(true, true, true, true, PlacementMode.Follow, -1f),
                Throws.ArgumentException);
        }
    }
}
