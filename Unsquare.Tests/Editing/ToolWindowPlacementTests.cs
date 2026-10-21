using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class ToolWindowPlacementTests
    {
        private const float Margin = 0.05f;

        [Test]
        public void TryPlace_OnScreen_FlipsYToTopDown()
        {
            Assert.That(ToolWindowPlacement.TryPlace(new float3(0.3f, 0.8f, 50f), Margin, out var position), Is.True);
            Assert.That(position.x, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(position.y, Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void TryPlace_BehindCamera_ReturnsFalse()
        {
            Assert.That(ToolWindowPlacement.TryPlace(new float3(0.5f, 0.5f, -1f), Margin, out _), Is.False);
            Assert.That(ToolWindowPlacement.TryPlace(new float3(0.5f, 0.5f, 0f), Margin, out _), Is.False);
        }

        [Test]
        public void TryPlace_OffScreen_ClampsIntoMargin()
        {
            Assert.That(ToolWindowPlacement.TryPlace(new float3(-2f, 3f, 10f), Margin, out var position), Is.True);
            Assert.That(position.x, Is.EqualTo(Margin).Within(1e-5f));
            Assert.That(position.y, Is.EqualTo(Margin).Within(1e-5f));
        }

        [Test]
        public void TryPlace_NotFinite_ReturnsFalse()
        {
            Assert.That(ToolWindowPlacement.TryPlace(new float3(float.NaN, 0.5f, 10f), Margin, out _), Is.False);
        }

        [Test]
        public void TryPlaceAbove_HigherCorner_RaisesWindowKeepsAnchorX()
        {
            var corners = new[] { new float3(0.2f, 0.6f, 40f), new float3(0.7f, 0.75f, 60f) };
            Assert.That(ToolWindowPlacement.TryPlaceAbove(new float3(0.4f, 0.65f, 50f), corners, Margin,
                out var position), Is.True);
            Assert.That(position.x, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(position.y, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void TryPlaceAbove_CornerBehindCamera_IsIgnored()
        {
            var corners = new[] { new float3(0.5f, 0.95f, -5f) };
            Assert.That(ToolWindowPlacement.TryPlaceAbove(new float3(0.5f, 0.6f, 50f), corners, Margin,
                out var position), Is.True);
            Assert.That(position.y, Is.EqualTo(0.4f).Within(1e-5f));
        }

        [Test]
        public void TryPlaceAbove_AnchorBehindCamera_ReturnsFalse()
        {
            var corners = new[] { new float3(0.5f, 0.7f, 10f) };
            Assert.That(ToolWindowPlacement.TryPlaceAbove(new float3(0.5f, 0.6f, -1f), corners, Margin, out _),
                Is.False);
        }

        [Test]
        public void TryPlaceAbove_CornerAboveScreen_ClampsIntoMargin()
        {
            var corners = new[] { new float3(0.5f, 1.4f, 10f) };
            Assert.That(ToolWindowPlacement.TryPlaceAbove(new float3(0.5f, 0.6f, 50f), corners, Margin,
                out var position), Is.True);
            Assert.That(position.y, Is.EqualTo(Margin).Within(1e-5f));
        }
    }
}
