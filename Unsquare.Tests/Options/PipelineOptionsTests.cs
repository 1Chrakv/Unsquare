using System;
using NUnit.Framework;
using Unsquare.Core.Deformation;
using Unsquare.Core.Options;

namespace Unsquare.Tests.Options
{
    public class PipelineOptionsTests
    {
        [Test]
        public void Default_MatchesPreviousConstants()
        {
            var options = PipelineOptions.Default;
            Assert.That(options.Limits.MaxOffsetFactor, Is.EqualTo(2f));
            Assert.That(options.Limits.MinOffsetLimitMeters, Is.EqualTo(5f));
            Assert.That(options.Limits.MinVolumeRatio, Is.EqualTo(0.05f));
            Assert.That(options.Attachments.FacadeReachMeters, Is.EqualTo(6f));
            Assert.That(options.Attachments.AttachedPropMode, Is.EqualTo(PlacementMode.Follow));
            Assert.That(options.RebakeDelaySeconds, Is.EqualTo(0.4));
            Assert.That(options.PruneUnused, Is.True);
            Assert.That(options.PruneAfter, Is.EqualTo(TimeSpan.FromDays(14)));
            Assert.That(options.AreaMatchMeters, Is.EqualTo(1f));
            Assert.That(options.NetMatchMeters, Is.EqualTo(1.5f));
        }

        [TestCase(-0.1, 14, 1f, 1f)]
        [TestCase(0.4, 0, 1f, 1f)]
        [TestCase(0.4, 14, 0f, 1f)]
        [TestCase(0.4, 14, 1f, -1f)]
        public void Constructor_InvalidValues_Throw(double delay, int days, float areaMatch, float netMatch)
        {
            Assert.That(() => new PipelineOptions(CageLimits.Default, AttachmentOptions.Default, delay, true, days,
                areaMatch, netMatch), Throws.ArgumentException);
        }

        [Test]
        public void Constructor_NullParts_Throw()
        {
            Assert.That(() => new PipelineOptions(null, AttachmentOptions.Default, 0.4, true, 14, 1f, 1f),
                Throws.InstanceOf<ArgumentNullException>());
            Assert.That(() => new PipelineOptions(CageLimits.Default, null, 0.4, true, 14, 1f, 1f),
                Throws.InstanceOf<ArgumentNullException>());
        }
    }
}
