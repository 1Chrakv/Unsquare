using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Automation;

namespace Unsquare.Tests.Automation
{
    public class ScreenCropTests
    {
        [Test]
        public void TryAround_PointsInside_PadsTheBoundingRect()
        {
            var points = new[] { new float2(100, 200), new float2(300, 400) };
            Assert.That(ScreenCrop.TryAround(points, 1920, 1080, 0.25f, 0, out var rect), Is.True);
            Assert.That(rect.X, Is.EqualTo(50));
            Assert.That(rect.Y, Is.EqualTo(150));
            Assert.That(rect.Width, Is.EqualTo(300));
            Assert.That(rect.Height, Is.EqualTo(300));
        }

        [Test]
        public void TryAround_TinyBuilding_GrowsToTheMinimumSize()
        {
            var points = new[] { new float2(500, 500), new float2(510, 505) };
            ScreenCrop.TryAround(points, 1920, 1080, 0f, 200, out var rect);
            Assert.That(rect.Width, Is.InRange(200, 201), "rounded outwards to whole pixels");
            Assert.That(rect.Height, Is.InRange(200, 201));
        }

        [Test]
        public void TryAround_PartlyOffScreen_IsClampedToTheScreen()
        {
            var points = new[] { new float2(-100, 900), new float2(200, 1200) };
            ScreenCrop.TryAround(points, 1920, 1080, 0f, 0, out var rect);
            Assert.That(rect.X, Is.EqualTo(0));
            Assert.That(rect.Y + rect.Height, Is.EqualTo(1080));
        }

        [Test]
        public void TryAround_AllOffScreen_ReturnsFalse()
        {
            var points = new[] { new float2(-500, -500), new float2(-400, -450) };
            Assert.That(ScreenCrop.TryAround(points, 1920, 1080, 0f, 0, out _), Is.False);
        }

        [Test]
        public void TryAround_NoPoints_ReturnsFalse()
        {
            Assert.That(ScreenCrop.TryAround(new float2[0], 1920, 1080, 0f, 0, out _), Is.False);
        }
    }
}
