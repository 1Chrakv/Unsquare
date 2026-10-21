using NUnit.Framework;
using Unsquare.Core.Import;

namespace Unsquare.Tests.Import
{
    public class FlexItVariantNameTests
    {
        [Test]
        public void TryParse_TwoValueTaper_ReadsAnglesAndBase()
        {
            Assert.That(FlexItVariantName.TryParse("EU_Residential_02__taper-12_7", out var variant), Is.True);
            Assert.That(variant.BaseName, Is.EqualTo("EU_Residential_02"));
            Assert.That(variant.Mode, Is.EqualTo(FlexItMode.Taper));
            Assert.That(new[] { variant.A, variant.B, variant.C, variant.D }, Is.EqualTo(new[] { -12, 7, 0, 0 }));
            Assert.That(variant.IsCurved, Is.False);
        }

        [Test]
        public void TryParse_FourValueTaper_ReadsFrontAndBack()
        {
            Assert.That(FlexItVariantName.TryParse("Base__taper1_2_-3_4", out var variant), Is.True);
            Assert.That(new[] { variant.A, variant.B, variant.C, variant.D }, Is.EqualTo(new[] { 1, 2, -3, 4 }));
        }

        [TestCase("Base__corner20_-1", FlexItMode.Corner)]
        [TestCase("Base__arch45_0", FlexItMode.Arch)]
        [TestCase("Base__bend30_15", FlexItMode.Bend)]
        public void TryParse_CurvedModes_AreCurved(string name, FlexItMode mode)
        {
            Assert.That(FlexItVariantName.TryParse(name, out var variant), Is.True);
            Assert.That(variant.Mode, Is.EqualTo(mode));
            Assert.That(variant.BaseName, Is.EqualTo("Base"));
            Assert.That(variant.IsCurved, Is.True);
        }

        [Test]
        public void TryParse_BaseContainingSeparator_UsesLastOccurrence()
        {
            Assert.That(FlexItVariantName.TryParse("Odd__taperName__taper5_5", out var variant), Is.True);
            Assert.That(variant.BaseName, Is.EqualTo("Odd__taperName"));
        }

        [TestCase(null)]
        [TestCase("Plain_Building")]
        [TestCase("Base__uq0123456789ab")]
        [TestCase("Base__taper5")]
        [TestCase("Base__taper5_x")]
        [TestCase("Base__arch1_2_3_4")]
        [TestCase("__taper5_5")]
        public void TryParse_OtherNames_ReturnFalse(string name)
        {
            Assert.That(FlexItVariantName.TryParse(name, out _), Is.False);
        }
    }
}
