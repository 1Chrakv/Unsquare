using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unsquare.Core.Automation;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Automation
{
    public class BridgeArgsTests
    {
        [Test]
        public void Float3_ArrayOrObject_ReadsVector()
        {
            var args = new BridgeArgs(JObject.Parse("{\"a\": [1, 2, 3], \"b\": {\"y\": 4}}"));
            Assert.That(args.Float3("a").z, Is.EqualTo(3f));
            Assert.That(args.Float3("b").y, Is.EqualTo(4f));
            Assert.That(args.Float3("b").x, Is.EqualTo(0f));
        }

        [Test]
        public void Float3_WrongShape_ThrowsArgumentError()
        {
            var args = new BridgeArgs(JObject.Parse("{\"a\": [1, 2]}"));
            Assert.Throws<BridgeArgumentException>(() => args.Float3("a"));
        }

        [Test]
        public void Enum_IgnoresCase_AndRejectsUnknown()
        {
            var args = new BridgeArgs(JObject.Parse("{\"kind\": \"face\", \"bad\": \"Sphere\", \"num\": \"7\"}"));
            Assert.That(args.Enum<CageHandleKind>("kind"), Is.EqualTo(CageHandleKind.Face));
            var error = Assert.Throws<BridgeArgumentException>(() => args.Enum<CageHandleKind>("bad"));
            Assert.That(error.Message, Does.Contain("Corner"));
            Assert.Throws<BridgeArgumentException>(() => args.Enum<CageHandleKind>("num"));
        }

        [Test]
        public void Missing_RequiredThrows_FallbackReturned()
        {
            var args = BridgeArgs.Empty;
            Assert.Throws<BridgeArgumentException>(() => args.String("save"));
            Assert.That(args.Int("frames", 5), Is.EqualTo(5));
            Assert.That(args.Has("frames"), Is.False);
        }

        [Test]
        public void Object_Nested_ReadsMembers_AndRejectsScalars()
        {
            var args = new BridgeArgs(JObject.Parse("{\"rotate\": {\"axis\": 1, \"degrees\": 30}, \"n\": 3}"));
            Assert.That(args.Object("rotate").Float("degrees"), Is.EqualTo(30f));
            Assert.Throws<BridgeArgumentException>(() => args.Object("n"));
        }

        [Test]
        public void Int_WrongType_ThrowsArgumentError()
        {
            var args = new BridgeArgs(JObject.Parse("{\"frames\": \"many\"}"));
            Assert.Throws<BridgeArgumentException>(() => args.Int("frames"));
        }

        [Test]
        public void Sources_PathBeatsBodyBeatsQuery()
        {
            var path = new Dictionary<string, string> { ["ref"] = "fromPath" };
            var query = new Dictionary<string, string> { ["ref"] = "fromQuery", ["limit"] = "3" };
            var args = new BridgeArgs(JObject.Parse("{\"ref\": \"fromBody\"}"), path, query);
            Assert.That(args.String("ref"), Is.EqualTo("fromPath"));
            Assert.That(args.Int("limit"), Is.EqualTo(3));
            Assert.That(new BridgeArgs(JObject.Parse("{\"ref\": \"fromBody\"}"), null, query).String("ref"),
                Is.EqualTo("fromBody"));
        }
    }
}
