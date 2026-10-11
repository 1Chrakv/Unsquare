using NUnit.Framework;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class HandleSelectionTests
    {
        [Test]
        public void Default_IsEdgeModeWithoutHandle()
        {
            Assert.That(HandleSelection.Default.PickKind, Is.EqualTo(CageHandleKind.Edge));
            Assert.That(HandleSelection.Default.HasHandle, Is.False);
            Assert.That(default(HandleSelection), Is.EqualTo(HandleSelection.Default));
        }

        [Test]
        public void CyclePickKind_EdgeFaceCorner_WrapsToEdge()
        {
            var selection = HandleSelection.Default.CyclePickKind();
            Assert.That(selection.PickKind, Is.EqualTo(CageHandleKind.Face));
            selection = selection.CyclePickKind();
            Assert.That(selection.PickKind, Is.EqualTo(CageHandleKind.Corner));
            Assert.That(selection.CyclePickKind().PickKind, Is.EqualTo(CageHandleKind.Edge));
        }

        [Test]
        public void WithPickKind_HandleSelected_KeepsHandle()
        {
            var selection = HandleSelection.Default.Select(CageHandle.Edge(4)).WithPickKind(CageHandleKind.Face);
            Assert.That(selection.HasHandle, Is.True);
            Assert.That(selection.Handle, Is.EqualTo(CageHandle.Edge(4)));
        }

        [Test]
        public void NextAndPrevious_NothingSelected_StartAtEnds()
        {
            Assert.That(HandleSelection.Default.Next().Handle, Is.EqualTo(CageHandle.Edge(0)));
            Assert.That(HandleSelection.Default.Previous().Handle, Is.EqualTo(CageHandle.Edge(11)));
        }

        [Test]
        public void Next_OtherKindSelected_StartsInPickKind()
        {
            var selection = HandleSelection.Default.Select(CageHandle.Edge(4)).WithPickKind(CageHandleKind.Face);
            Assert.That(selection.Next().Handle, Is.EqualTo(CageHandle.Face(0)));
        }

        [Test]
        public void Next_LastOfKind_Wraps()
        {
            var selection = HandleSelection.Default.WithPickKind(CageHandleKind.Corner).Select(CageHandle.Corner(7));
            Assert.That(selection.Next().Handle, Is.EqualTo(CageHandle.Corner(0)));
            Assert.That(selection.Previous().Handle, Is.EqualTo(CageHandle.Corner(6)));
        }

        [Test]
        public void Clear_KeepsPickKind()
        {
            var selection = HandleSelection.Default.WithPickKind(CageHandleKind.Face).Select(CageHandle.Face(2)).Clear();
            Assert.That(selection.HasHandle, Is.False);
            Assert.That(selection.PickKind, Is.EqualTo(CageHandleKind.Face));
        }
    }
}
