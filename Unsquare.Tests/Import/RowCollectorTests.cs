using System.Collections.Generic;
using NUnit.Framework;
using Unsquare.Core.Import;

namespace Unsquare.Tests.Import
{
    public class RowCollectorTests
    {
        private const int None = 0;

        // Neighbour table: building -> (left, right); missing buildings are unknown.
        private static TryNeighbours<int> Links(Dictionary<int, (int Left, int Right)> links) =>
            (int building, out int left, out int right) =>
            {
                bool found = links.TryGetValue(building, out var pair);
                left = found ? pair.Left : None;
                right = found ? pair.Right : None;
                return found;
            };

        [Test]
        public void Collect_NoNeighbours_ReturnsStartOnly()
        {
            var links = Links(new Dictionary<int, (int, int)> { [1] = (None, None) });
            Assert.That(RowCollector.Collect(1, None, links, 64), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void Collect_UnknownStart_ReturnsStartOnly()
        {
            var links = Links(new Dictionary<int, (int, int)>());
            Assert.That(RowCollector.Collect(7, None, links, 64), Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void Collect_ChainBothWays_ReturnsWholeRowNearestFirst()
        {
            var links = Links(new Dictionary<int, (int, int)>
            {
                [1] = (None, 2), [2] = (1, 3), [3] = (2, 4), [4] = (3, None),
            });
            Assert.That(RowCollector.Collect(2, None, links, 64), Is.EqualTo(new[] { 2, 1, 3, 4 }));
        }

        [Test]
        public void Collect_Cycle_VisitsEachOnce()
        {
            var links = Links(new Dictionary<int, (int, int)> { [1] = (3, 2), [2] = (1, 3), [3] = (2, 1) });
            Assert.That(RowCollector.Collect(1, None, links, 64), Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Collect_LongRow_StopsAtMaxCount()
        {
            var table = new Dictionary<int, (int, int)>();
            for (int i = 1; i <= 100; i++)
                table[i] = (i - 1, i == 100 ? None : i + 1);
            Assert.That(RowCollector.Collect(1, None, Links(table), 10), Has.Count.EqualTo(10));
        }

        [Test]
        public void Collect_OneSidedLink_FollowsIt()
        {
            // Placemaker may record a joint on one house only; the other side still reaches it.
            var links = Links(new Dictionary<int, (int, int)> { [1] = (None, 2), [2] = (None, None) });
            Assert.That(RowCollector.Collect(1, None, links, 64), Is.EqualTo(new[] { 1, 2 }));
        }
    }
}
