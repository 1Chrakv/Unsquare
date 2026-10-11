using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class EditHistoryTests
    {
        private static CageOffsets Up(float meters) =>
            CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0, meters, 0));

        [Test]
        public void Record_SameOffsets_IsIgnored()
        {
            var history = new EditHistory<int>(10);
            history.Record(1, Up(1), Up(1));
            Assert.That(history.CanUndo, Is.False);
        }

        [Test]
        public void TryUndo_AfterRecord_ReturnsEditAndEnablesRedo()
        {
            var history = new EditHistory<int>(10);
            history.Record(1, CageOffsets.Identity, Up(1));
            Assert.That(history.TryUndo(out var edit), Is.True);
            Assert.That(edit.Building, Is.EqualTo(1));
            Assert.That(edit.Before, Is.EqualTo(CageOffsets.Identity));
            Assert.That(edit.After, Is.EqualTo(Up(1)));
            Assert.That(history.CanUndo, Is.False);
            Assert.That(history.CanRedo, Is.True);
        }

        [Test]
        public void TryUndo_Empty_ReturnsFalse()
        {
            var history = new EditHistory<int>(10);
            Assert.That(history.TryUndo(out _), Is.False);
            Assert.That(history.TryRedo(out _), Is.False);
        }

        [Test]
        public void Record_AfterUndo_ClearsRedo()
        {
            var history = new EditHistory<int>(10);
            history.Record(1, CageOffsets.Identity, Up(1));
            history.TryUndo(out _);
            history.Record(1, CageOffsets.Identity, Up(2));
            Assert.That(history.CanRedo, Is.False);
            Assert.That(history.UndoCount, Is.EqualTo(1));
        }

        [Test]
        public void UndoRedo_RoundTrip_RestoresOrder()
        {
            var history = new EditHistory<int>(10);
            history.Record(1, CageOffsets.Identity, Up(1));
            history.Record(2, CageOffsets.Identity, Up(2));
            history.TryUndo(out var first);
            history.TryUndo(out var second);
            Assert.That(first.Building, Is.EqualTo(2));
            Assert.That(second.Building, Is.EqualTo(1));
            history.TryRedo(out var redone);
            Assert.That(redone.Building, Is.EqualTo(1));
            Assert.That(history.UndoCount, Is.EqualTo(1));
            Assert.That(history.RedoCount, Is.EqualTo(1));
        }

        [Test]
        public void Record_BeyondCapacity_DropsOldest()
        {
            var history = new EditHistory<int>(2);
            history.Record(1, CageOffsets.Identity, Up(1));
            history.Record(2, CageOffsets.Identity, Up(1));
            history.Record(3, CageOffsets.Identity, Up(1));
            Assert.That(history.UndoCount, Is.EqualTo(2));
            history.TryUndo(out _);
            history.TryUndo(out var oldest);
            Assert.That(oldest.Building, Is.EqualTo(2));
        }

        [Test]
        public void Capacity_Lowered_TrimsOldest()
        {
            var history = new EditHistory<int>(5);
            for (int i = 0; i < 5; i++)
                history.Record(i, CageOffsets.Identity, Up(1));
            history.Capacity = 2;
            Assert.That(history.UndoCount, Is.EqualTo(2));
            history.TryUndo(out var newest);
            Assert.That(newest.Building, Is.EqualTo(4));
        }

        [Test]
        public void Forget_RemovesOnlyThatBuilding()
        {
            var history = new EditHistory<int>(10);
            history.Record(1, CageOffsets.Identity, Up(1));
            history.Record(2, CageOffsets.Identity, Up(1));
            history.Record(1, Up(1), Up(2));
            history.TryUndo(out _);
            history.Forget(1);
            Assert.That(history.UndoCount, Is.EqualTo(1));
            Assert.That(history.CanRedo, Is.False);
            history.TryUndo(out var left);
            Assert.That(left.Building, Is.EqualTo(2));
        }

        [Test]
        public void Ctor_ZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new EditHistory<int>(0));
        }
    }
}
