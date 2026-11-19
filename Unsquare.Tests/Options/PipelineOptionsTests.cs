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
            Assert.That(options.RebakeDelaySeconds, Is.EqualTo(0.2));
            Assert.That(options.GeometryCacheGigabytes, Is.EqualTo(2f));
            Assert.That(options.GeometryCacheBytes, Is.EqualTo(2L * 1024 * 1024 * 1024));
            Assert.That(options.AreaMatchMeters, Is.EqualTo(1f));
            Assert.That(options.CoarseLodTwistMeters, Is.EqualTo(1f));
            Assert.That(options.KeepOnLevelUp, Is.True);
        }

        [TestCase(-0.1, 2f, 1f, 1f)]
        [TestCase(0.4, 0f, 1f, 1f)]
        [TestCase(0.4, float.NaN, 1f, 1f)]
        [TestCase(0.4, 2f, 0f, 1f)]
        [TestCase(0.4, 2f, 1f, -0.1f)]
        public void Constructor_InvalidValues_Throw(double delay, float cacheGigabytes, float areaMatch, float lodTwist)
        {
            Assert.That(() => new PipelineOptions(CageLimits.Default, AttachmentOptions.Default, delay, cacheGigabytes,
                areaMatch, lodTwist, true), Throws.ArgumentException);
        }

        [Test]
        public void Constructor_NullParts_Throw()
        {
            Assert.That(() => new PipelineOptions(null, AttachmentOptions.Default, 0.4, 2f, 1f, 1f, true),
                Throws.InstanceOf<ArgumentNullException>());
            Assert.That(() => new PipelineOptions(CageLimits.Default, null, 0.4, 2f, 1f, 1f, true),
                Throws.InstanceOf<ArgumentNullException>());
        }
    }
}
