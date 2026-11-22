using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Persistence;

namespace Unsquare.Tests.Persistence
{
    public class SavedCageCodecTests
    {
        private static readonly CageOffsets Lot = CageOffsets.Identity.WithCornerSteps(5, new int3(3, -2, 7));
        private static readonly CageOffsets Building = CageOffsets.Identity.WithCornerSteps(1, new int3(-4, 10, 0));

        [TestCase(false)]
        [TestCase(true)]
        public void Encode_Linked_RoundTrips(bool flat)
        {
            var spec = new DeformSpec(Lot, flat);

            var decoded = RoundTrip(spec);

            Assert.That(decoded, Is.EqualTo(spec.ForVariant()));
        }

        [TestCase(BlendMode.None)]
        [TestCase(BlendMode.All)]
        public void Encode_Split_RoundTrips(BlendMode mode)
        {
            var spec = new DeformSpec(Lot, Building, true, mode);

            var decoded = RoundTrip(spec);

            Assert.That(decoded, Is.EqualTo(spec.ForVariant()));
            Assert.That(decoded.BlendMode, Is.EqualTo(mode));
        }

        [Test]
        public void Encode_Identity_RoundTripsToIdentity()
        {
            Assert.That(RoundTrip(DeformSpec.Identity).IsIdentity, Is.True);
        }

        [Test]
        public void Encode_Linked_WritesOneCage()
        {
            var values = new List<int>();

            SavedCageCodec.Encode(new DeformSpec(Lot, false), values);

            Assert.That(values.Count, Is.EqualTo(2 + CageOffsets.ValueCount));
        }

        [Test]
        public void Encode_Split_WritesBothCagesWithinMax()
        {
            var values = new List<int>();

            SavedCageCodec.Encode(new DeformSpec(Lot, Building, false), values);

            Assert.That(values.Count, Is.EqualTo(SavedCageCodec.MaxValueCount));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void TryDecode_UnknownFormat_ReturnsFalse(int format)
        {
            var values = Encode(new DeformSpec(Lot, false));

            Assert.That(SavedCageCodec.TryDecode(format, values, out _), Is.False);
        }

        [Test]
        public void TryDecode_Truncated_ReturnsFalse()
        {
            var values = Encode(new DeformSpec(Lot, Building, false));
            values.RemoveAt(values.Count - 1);

            Assert.That(SavedCageCodec.TryDecode(SavedCageCodec.FormatVersion, values, out _), Is.False);
        }

        [Test]
        public void TryDecode_UndefinedBlendMode_ReturnsFalse()
        {
            var values = Encode(new DeformSpec(Lot, false));
            values[1] = 1; // the retired paths-only mode

            Assert.That(SavedCageCodec.TryDecode(SavedCageCodec.FormatVersion, values, out _), Is.False);
        }

        [Test]
        public void TryDecode_UnknownFlag_ReturnsFalse()
        {
            var values = Encode(new DeformSpec(Lot, false));
            values[0] |= 4;

            Assert.That(SavedCageCodec.TryDecode(SavedCageCodec.FormatVersion, values, out _), Is.False);
        }

        private static List<int> Encode(DeformSpec spec)
        {
            var values = new List<int>();
            SavedCageCodec.Encode(spec, values);
            return values;
        }

        private static DeformSpec RoundTrip(DeformSpec spec)
        {
            Assert.That(SavedCageCodec.TryDecode(SavedCageCodec.FormatVersion, Encode(spec), out var decoded), Is.True);
            return decoded;
        }
    }
}
