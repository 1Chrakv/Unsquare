using NUnit.Framework;
using Unsquare.Core.Import;

namespace Unsquare.Tests.Import
{
    public class FlexItImportPlannerTests
    {
        private static readonly FlexItVariant Taper = new("Base", FlexItMode.Taper, 10, 0, 0, 0);
        private static readonly FlexItVariant Arch = new("Base", FlexItMode.Arch, 30, 0, 0, 0);
        private static readonly FlexItShear Shear = new(15, false, 8);

        [Test]
        public void Decide_Nothing_Skips()
        {
            Assert.That(FlexItImportPlanner.Decide(null, null), Is.EqualTo(FlexItImportAction.Skip));
        }

        [Test]
        public void Decide_TaperOrShear_Converts()
        {
            Assert.That(FlexItImportPlanner.Decide(Taper, null), Is.EqualTo(FlexItImportAction.Convert));
            Assert.That(FlexItImportPlanner.Decide(null, Shear), Is.EqualTo(FlexItImportAction.Convert));
            Assert.That(FlexItImportPlanner.Decide(Taper, Shear), Is.EqualTo(FlexItImportAction.Convert));
        }

        [Test]
        public void Decide_CornerFit_Approximates()
        {
            var corner = new FlexItVariant("Base", FlexItMode.Corner, 20, 1, 0, 0);
            Assert.That(FlexItImportPlanner.Decide(corner, null), Is.EqualTo(FlexItImportAction.Approximate));
            Assert.That(FlexItImportPlanner.Decide(corner, Shear), Is.EqualTo(FlexItImportAction.Approximate));
        }

        [Test]
        public void Decide_Bend_Resets()
        {
            var bend = new FlexItVariant("Base", FlexItMode.Bend, 20, 10, 0, 0);
            Assert.That(FlexItImportPlanner.Decide(bend, null), Is.EqualTo(FlexItImportAction.ResetToBase));
        }

        [Test]
        public void Decide_CurvedVariant_ResetsEvenWithShear()
        {
            Assert.That(FlexItImportPlanner.Decide(Arch, null), Is.EqualTo(FlexItImportAction.ResetToBase));
            Assert.That(FlexItImportPlanner.Decide(Arch, Shear), Is.EqualTo(FlexItImportAction.ResetToBase));
        }
    }
}
