using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class NudgeStepAndDragTests
    {
        [Test]
        public void NudgeStep_CyclesThroughSizesAndBack()
        {
            var step = NudgeStep.Default;
            Assert.That(step.Meters, Is.EqualTo(0.5f));
            Assert.That(step.Next().Meters, Is.EqualTo(2f));
            Assert.That(step.Next().Next().Meters, Is.EqualTo(0.1f));
            Assert.That(step.Next().Next().Next(), Is.EqualTo(step));
        }

        [Test]
        public void FromSizes_SnapsToGridAndSorts()
        {
            var step = NudgeStep.FromSizes(3.04f, 0.01f, 1.26f);
            Assert.That(step.Meters, Is.EqualTo(1.3f).Within(1e-5f));
            Assert.That(step.Next().Meters, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(step.Next().Next().Meters, Is.EqualTo(CageOffsets.StepMeters).Within(1e-5f));
        }

        [Test]
        public void WithSizes_KeepsPosition()
        {
            var large = NudgeStep.Default.Next();
            var resized = large.WithSizes(0.2f, 1f, 5f);
            Assert.That(resized.Meters, Is.EqualTo(5f).Within(1e-5f));
            Assert.That(resized.Next().Meters, Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void Default_EqualsFromSizesOfDefaults()
        {
            Assert.That(NudgeStep.FromSizes(0.1f, 0.5f, 2f), Is.EqualTo(NudgeStep.Default));
        }

        [Test]
        public void NudgeStep_SizesAreOnTheOffsetGrid()
        {
            var step = NudgeStep.Default;
            for (int i = 0; i < 3; i++, step = step.Next())
            {
                float steps = step.Meters / CageOffsets.StepMeters;
                Assert.That(steps, Is.EqualTo(math.round(steps)).Within(1e-4f), step.ToString());
            }
        }

        [Test]
        public void Drag_ManySmallFrames_DoNotDrift()
        {
            var drag = new CageDrag(CageHandle.Face(3), CageOffsets.Identity);
            CageOffsets offsets = drag.StartOffsets;
            float3 total = float3.zero;
            for (int frame = 0; frame < 20; frame++)
            {
                total += new float3(0, 0.03f, 0); // below the 0.1 m grid per frame
                offsets = drag.OffsetsFor(total);
            }
            Assert.That(offsets.Meters(2).y, Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void Drag_KeepsStartOffsetsOfOtherCorners()
        {
            var start = CageOffsets.Identity.Move(CageHandle.Corner(0), new float3(1, 0, 0));
            var drag = new CageDrag(CageHandle.Corner(7), start);
            var offsets = drag.OffsetsFor(new float3(0, 2, 0));
            Assert.That(offsets.Steps(0), Is.EqualTo(new int3(10, 0, 0)));
            Assert.That(offsets.Steps(7), Is.EqualTo(new int3(0, 20, 0)));
        }
    }
}
