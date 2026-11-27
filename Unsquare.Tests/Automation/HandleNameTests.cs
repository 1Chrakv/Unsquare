using NUnit.Framework;
using Unsquare.Core.Automation;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Automation
{
    public class HandleNameTests
    {
        [Test]
        public void TryParse_PrintedName_RoundTrips()
        {
            var handle = CageHandle.Face(5);
            Assert.That(HandleName.TryParse(handle.ToString(), out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(handle));
        }

        [Test]
        public void TryParse_CageWithoutIndex_IsTheWholeCage()
        {
            Assert.That(HandleName.TryParse("cage", out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(CageHandle.Cage()));
        }

        [TestCase("Corner8")]
        [TestCase("Edge-1")]
        [TestCase("Roof1")]
        [TestCase("3")]
        [TestCase("")]
        public void TryParse_Invalid_ReturnsFalse(string text)
        {
            Assert.That(HandleName.TryParse(text, out _), Is.False);
        }
    }
}
