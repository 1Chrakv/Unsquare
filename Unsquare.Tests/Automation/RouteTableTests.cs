using System;
using System.Linq;
using NUnit.Framework;
using Unsquare.Core.Automation;

namespace Unsquare.Tests.Automation
{
    public class RouteTableTests
    {
        private static RouteTable<string> Table()
        {
            var table = new RouteTable<string>();
            table.Add("GET state", "state");
            table.Add("POST session/nudge", "nudge");
            table.Add("GET buildings/{ref}/cage", "cage");
            table.Add("GET buildings", "buildings");
            return table;
        }

        [Test]
        public void TryMatch_LiteralPath_FindsHandler()
        {
            Assert.That(Table().TryMatch("POST", "/v1/session/nudge", out var handler, out _, out _), Is.True);
            Assert.That(handler, Is.EqualTo("nudge"));
        }

        [Test]
        public void TryMatch_Parameter_CapturesUnescapedSegment()
        {
            Assert.That(Table().TryMatch("GET", "/v1/buildings/House%401.5%2C2/cage", out var handler,
                out var parameters, out _), Is.True);
            Assert.That(handler, Is.EqualTo("cage"));
            Assert.That(parameters["ref"], Is.EqualTo("House@1.5,2"));
        }

        [Test]
        public void TryMatch_EscapedEntityRef_KeepsWholeSegment()
        {
            Assert.That(Table().TryMatch("GET", "/v1/buildings/%2372118%3A5/cage", out var handler,
                out var parameters, out _), Is.True);
            Assert.That(handler, Is.EqualTo("cage"));
            Assert.That(parameters["ref"], Is.EqualTo("#72118:5"));
        }

        [Test]
        public void TryMatch_MethodIsCaseInsensitive()
        {
            Assert.That(Table().TryMatch("get", "/v1/state", out _, out _, out _), Is.True);
        }

        [Test]
        public void TryMatch_WrongMethod_ReportsPathKnown()
        {
            Assert.That(Table().TryMatch("GET", "/v1/session/nudge", out _, out _, out var methodAllowed), Is.False);
            Assert.That(methodAllowed, Is.True);
        }

        [TestCase("/v1/nothing")]
        [TestCase("/v2/state")]
        [TestCase("/state")]
        [TestCase("/v1/buildings//cage")]
        public void TryMatch_UnknownPath_FailsWithoutMethodHint(string path)
        {
            Assert.That(Table().TryMatch("GET", path, out _, out _, out var methodAllowed), Is.False);
            Assert.That(methodAllowed, Is.False);
        }

        [Test]
        public void Add_SameShapeTwice_Throws()
        {
            var table = Table();
            Assert.Throws<ArgumentException>(() => table.Add("GET buildings/{other}/cage", "again"));
        }

        [Test]
        public void Patterns_ListsEveryRoute()
        {
            Assert.That(Table().Patterns.ToList(), Does.Contain("GET buildings/{ref}/cage"));
        }
    }
}
