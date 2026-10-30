using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Naming;

namespace Unsquare.Tests.Naming
{
    public class NamingTests
    {
        private static readonly CageBox Box = new(new float3(-5, 0, -5), new float3(5, 10, 5));
        private static readonly CageOffsets Offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(1, 2, 0));

        [Test]
        public void StableHash_IsDeterministicAndKnown()
        {
            // MD5("abc") is a fixed test vector.
            Assert.That(StableHash.Compute("abc").ToHex(16), Is.EqualTo("900150983cd24fb0d6963f7d28e17f72"));
            Assert.That(StableHash.Compute("abc").Word(0), Is.EqualTo(0x98500190u));
        }

        [Test]
        public void VariantName_RoundTripsToBaseName()
        {
            string name = VariantNaming.VariantName("EU_Residential_02", Offsets);
            Assert.That(name, Does.StartWith("EU_Residential_02__uq"));
            Assert.That(VariantNaming.TryParse(name, out var baseName), Is.True);
            Assert.That(baseName, Is.EqualTo("EU_Residential_02"));
        }

        [Test]
        public void VariantName_BaseContainingSeparatorText_ParsesLastOccurrence()
        {
            string name = VariantNaming.VariantName("Odd__uqName", Offsets);
            Assert.That(VariantNaming.TryParse(name, out var baseName), Is.True);
            Assert.That(baseName, Is.EqualTo("Odd__uqName"));
        }

        [Test]
        public void VariantName_DependsOnOffsetsOnly()
        {
            string a = VariantNaming.VariantName("Base", Offsets);
            string b = VariantNaming.VariantName("Base", CageOffsets.FromArray(Offsets.ToArray()));
            string c = VariantNaming.VariantName("Base", Offsets.Move(CageHandle.Corner(0), new float3(0.1f, 0, 0)));
            Assert.That(b, Is.EqualTo(a));
            Assert.That(c, Is.Not.EqualTo(a));
        }

        [Test]
        public void VariantName_UsesLotSeed()
        {
            // Saves reference names from this exact seed.
            string expected = "Base__uq" +
                              StableHash.Compute("Unsquare:lot-offsets:v1:" + Offsets.ToCanonicalString()).ToHex(6);
            Assert.That(VariantNaming.VariantName("Base", Offsets), Is.EqualTo(expected));
        }

        [Test]
        public void VariantName_FlatTerrain_DiffersAndKeepsShape()
        {
            string lifted = VariantNaming.VariantName("Base", Offsets);
            string flat = VariantNaming.VariantName("Base", Offsets, flatTerrain: true);
            Assert.That(flat, Is.Not.EqualTo(lifted));
            Assert.That(VariantNaming.TryParse(flat, out var baseName), Is.True);
            Assert.That(baseName, Is.EqualTo("Base"));
            Assert.That(VariantNaming.VariantName("Base", Offsets, flatTerrain: false), Is.EqualTo(lifted));
        }

        [TestCase("Plain_Building")]
        [TestCase("Base__uq123")]
        [TestCase("Base__uqZZZZZZZZZZZZ")]
        [TestCase("__uq0123456789ab")]
        public void TryParse_NonVariantNames_ReturnFalse(string name)
        {
            Assert.That(VariantNaming.TryParse(name, out _), Is.False);
        }

        [Test]
        public void DeformKey_ChangesWithBoxAndOffsets()
        {
            string key = DeformKey.Create(Box, Offsets);
            Assert.That(key, Does.StartWith($"cage|v{DeformKey.FormatVersion}|-500,0,-500|500,1000,500|"));
            var otherBox = new CageBox(Box.Min, Box.Max + new float3(0, 0.5f, 0));
            Assert.That(DeformKey.Create(otherBox, Offsets), Is.Not.EqualTo(key));
            Assert.That(DeformKey.Create(Box, CageOffsets.Identity), Is.Not.EqualTo(key));
        }

        [Test]
        public void AssetName_IsRecognisedAndDistinctPerRender()
        {
            string key = DeformKey.Create(Box, Offsets);
            string a = AssetNaming.AssetName("RenderA", key);
            string b = AssetNaming.AssetName("RenderB", key);
            Assert.That(AssetNaming.IsOwnAssetName(a), Is.True);
            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(AssetNaming.AssetName("RenderA", key), Is.EqualTo(a));
        }

        [Test]
        public void ForSubmesh_DiffersPerPlacementAndIsStable()
        {
            string key = DeformKey.Create(Box, Offsets);
            var left = new SubmeshTransform(new float3(-3, 0, 0), quaternion.identity);
            var right = new SubmeshTransform(new float3(3, 0, 0), quaternion.identity);
            var turned = new SubmeshTransform(new float3(-3, 0, 0), quaternion.RotateY(math.PI));

            Assert.That(DeformKey.ForSubmesh(key, left), Is.EqualTo(DeformKey.ForSubmesh(key, left)));
            Assert.That(DeformKey.ForSubmesh(key, left), Is.Not.EqualTo(DeformKey.ForSubmesh(key, right)));
            Assert.That(DeformKey.ForSubmesh(key, left), Is.Not.EqualTo(DeformKey.ForSubmesh(key, turned)));
            Assert.That(DeformKey.ForSubmesh(key, left), Does.StartWith(key + "|at-300,0,0|"));
        }

        [Test]
        public void ExtensionVariantName_UsesExtensionSeedAndParsesToBase()
        {
            string owner = VariantNaming.VariantName("ParkingHall03", Offsets);
            string name = VariantNaming.ExtensionVariantName("ParkingHall03 Floor", owner);

            // Saves reference names from this exact seed.
            string expected = "ParkingHall03 Floor__uq" +
                              StableHash.Compute("Unsquare:extension:v1:" + owner).ToHex(6);
            Assert.That(name, Is.EqualTo(expected));
            Assert.That(VariantNaming.TryParse(name, out var baseName), Is.True);
            Assert.That(baseName, Is.EqualTo("ParkingHall03 Floor"));
        }

        [Test]
        public void ExtensionVariantName_DiffersPerOwnerVariant()
        {
            string a = VariantNaming.VariantName("Base", Offsets);
            string b = VariantNaming.VariantName("Base", Offsets, flatTerrain: true);
            Assert.That(VariantNaming.ExtensionVariantName("Ext", a),
                Is.Not.EqualTo(VariantNaming.ExtensionVariantName("Ext", b)));
            Assert.That(VariantNaming.ExtensionVariantName("Ext", a),
                Is.EqualTo(VariantNaming.ExtensionVariantName("Ext", a)));
        }

        [TestCase("Base")]
        [TestCase(null)]
        public void ExtensionVariantName_OwnerNotAVariant_Throws(string owner)
        {
            Assert.That(() => VariantNaming.ExtensionVariantName("Ext", owner), Throws.ArgumentException);
        }

        [TestCase(null)]
        [TestCase("UQ_short")]
        [TestCase("XX_0123456789abcdef")]
        public void IsOwnAssetName_RejectsForeignNames(string name)
        {
            Assert.That(AssetNaming.IsOwnAssetName(name), Is.False);
        }
    }
}
