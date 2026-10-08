using NUnit.Framework;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class CageHandleCursorTests
    {
        [Test]
        public void Default_IsFirstCorner()
        {
            Assert.That(default(CageHandleCursor).Handle, Is.EqualTo(CageHandle.Corner(0)));
        }

        [Test]
        public void Next_WrapsWithinKind()
        {
            var last = new CageHandleCursor(CageHandleKind.Face, 5);
            Assert.That(last.Next().Index, Is.EqualTo(0));
            Assert.That(last.Next().Kind, Is.EqualTo(CageHandleKind.Face));
        }

        [Test]
        public void Previous_WrapsToLast()
        {
            var first = new CageHandleCursor(CageHandleKind.Edge, 0);
            Assert.That(first.Previous().Index, Is.EqualTo(11));
        }

        [Test]
        public void WithKind_KeepsIndexIfValid()
        {
            var cursor = new CageHandleCursor(CageHandleKind.Corner, 3);
            Assert.That(cursor.WithKind(CageHandleKind.Face).Handle, Is.EqualTo(CageHandle.Face(3)));
        }

        [Test]
        public void WithKind_ClampsIndexToNewKind()
        {
            var cursor = new CageHandleCursor(CageHandleKind.Edge, 10);
            Assert.That(cursor.WithKind(CageHandleKind.Face).Index, Is.EqualTo(5));
            Assert.That(cursor.WithKind(CageHandleKind.Corner).Index, Is.EqualTo(7));
        }

        [Test]
        public void Constructor_OutOfRangeIndex_Wraps()
        {
            Assert.That(new CageHandleCursor(CageHandleKind.Corner, 9).Index, Is.EqualTo(1));
            Assert.That(new CageHandleCursor(CageHandleKind.Corner, -1).Index, Is.EqualTo(7));
        }
    }
}
