using System.Linq;
using NUnit.Framework;
using Unsquare.Core.Naming;

namespace Unsquare.Tests.Naming
{
    public class VariantLocaleKeysTests
    {
        [Test]
        public void Aliases_MapEveryFamilyToTheBaseKey()
        {
            var aliases = VariantLocaleKeys.Aliases("ElementarySchool02__uq5eecd6d0b20c", "ElementarySchool02");

            Assert.That(aliases.Select(a => a.VariantKey), Is.EqualTo(new[]
            {
                "Assets.NAME[ElementarySchool02__uq5eecd6d0b20c]",
                "Assets.DESCRIPTION[ElementarySchool02__uq5eecd6d0b20c]",
                "Assets.UPGRADE_NAME[ElementarySchool02__uq5eecd6d0b20c]",
                "Assets.UPGRADE_DESCRIPTION[ElementarySchool02__uq5eecd6d0b20c]",
            }));
            Assert.That(aliases.Select(a => a.BaseKey), Is.EqualTo(new[]
            {
                "Assets.NAME[ElementarySchool02]",
                "Assets.DESCRIPTION[ElementarySchool02]",
                "Assets.UPGRADE_NAME[ElementarySchool02]",
                "Assets.UPGRADE_DESCRIPTION[ElementarySchool02]",
            }));
        }

        [TestCase(null, "Base")]
        [TestCase("", "Base")]
        [TestCase("Base__uq000000000001", null)]
        public void Aliases_MissingName_Throws(string variant, string baseName)
        {
            Assert.That(() => VariantLocaleKeys.Aliases(variant, baseName), Throws.ArgumentException);
        }
    }
}
