using NUnit.Framework;
using Unsquare.Core.Interop;

namespace Unsquare.Tests.Interop
{
    public class ReflectionContractTests
    {
        // Stand-ins for another mod's public surface.
        public struct FakeShape
        {
            public float Min;
            public int Count;
        }

        public sealed class FakeSystem
        {
            public bool IsVariant(long prefab) => prefab > 0;
            public bool TryGetShape(long prefab, out FakeShape shape)
            {
                shape = default;
                return false;
            }
            public bool TryGetByRef(long prefab, ref FakeShape shape) => false;
            public int Count(long prefab) => 0;
        }

        [Test]
        public void Method_Present_FoundAndComplete()
        {
            var contract = new ReflectionContract(typeof(FakeSystem));
            Assert.That(contract.Method("IsVariant", typeof(bool), typeof(long)), Is.Not.Null);
            Assert.That(contract.IsComplete, Is.True);
            Assert.That(contract.Describe(), Is.EqualTo("ok"));
        }

        [Test]
        public void Method_Renamed_ReportedMissing()
        {
            var contract = new ReflectionContract(typeof(FakeSystem));
            Assert.That(contract.Method("IsVariantPrefab", typeof(bool), typeof(long)), Is.Null);
            Assert.That(contract.Missing, Is.EqualTo(new[] { "FakeSystem.IsVariantPrefab" }));
        }

        [Test]
        public void Method_OtherReturnType_ReportedMissing()
        {
            var contract = new ReflectionContract(typeof(FakeSystem));
            Assert.That(contract.Method("Count", typeof(bool), typeof(long)), Is.Null);
            Assert.That(contract.IsComplete, Is.False);
        }

        [Test]
        public void Method_OtherParameterType_ReportedMissing()
        {
            var contract = new ReflectionContract(typeof(FakeSystem));
            Assert.That(contract.Method("IsVariant", typeof(bool), typeof(int)), Is.Null);
            Assert.That(contract.IsComplete, Is.False);
        }

        [Test]
        public void TryGetter_OutParameter_GivesValueType()
        {
            var contract = new ReflectionContract(typeof(FakeSystem));
            Assert.That(contract.TryGetter("TryGetShape", typeof(long), out var valueType), Is.Not.Null);
            Assert.That(valueType, Is.EqualTo(typeof(FakeShape)));
            Assert.That(contract.IsComplete, Is.True);
        }

        [Test]
        public void TryGetter_RefInsteadOfOut_ReportedMissing()
        {
            var contract = new ReflectionContract(typeof(FakeSystem));
            Assert.That(contract.TryGetter("TryGetByRef", typeof(long), out var valueType), Is.Null);
            Assert.That(valueType, Is.Null);
            Assert.That(contract.Missing, Is.EqualTo(new[] { "FakeSystem.TryGetByRef" }));
        }

        [Test]
        public void Field_ChangedType_ReportedMissing()
        {
            var contract = new ReflectionContract(typeof(FakeShape));
            Assert.That(contract.Field("Min", typeof(float)), Is.Not.Null);
            Assert.That(contract.Field("Count", typeof(float)), Is.Null);
            Assert.That(contract.Describe(), Is.EqualTo("missing FakeShape.Count"));
        }

        [Test]
        public void Include_OtherContract_AddsItsMissingMembers()
        {
            var system = new ReflectionContract(typeof(FakeSystem));
            var shape = new ReflectionContract(typeof(FakeShape));
            shape.Field("Max", typeof(float));
            system.Include(shape);
            Assert.That(system.Missing, Is.EqualTo(new[] { "FakeShape.Max" }));
        }
    }
}
