using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unsquare.Core.Cage;
using Unsquare.Core.Persistence;

namespace Unsquare.Tests.Persistence
{
    public class VariantManifestTests
    {
        private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        internal static ManifestEntry Entry(string variant, params string[] assets) => new()
        {
            BaseType = "BuildingPrefab",
            BaseName = "Base",
            VariantName = variant,
            Offsets = new int[24],
            Assets = assets.ToList(),
        };

        [Test]
        public void Register_NewEntry_AddsCityAndTimestamp()
        {
            var manifest = new VariantManifest();
            manifest.Register(Entry("Base__uq000000000001", "UQ_a"), "Springfield", Now);

            Assert.That(manifest.TryGet("BuildingPrefab/Base__uq000000000001", out var entry), Is.True);
            Assert.That(entry.Cities, Is.EqualTo(new[] { "Springfield" }));
            Assert.That(entry.LastUsedUtc, Is.EqualTo(Now));
            Assert.That(manifest.IsDirty, Is.True);
        }

        [Test]
        public void Register_Existing_MergesAssetsWithoutDuplicates()
        {
            var manifest = new VariantManifest();
            manifest.Register(Entry("V", "UQ_a", "UQ_b"), "C1", Now);
            manifest.Register(Entry("V", "UQ_b", "UQ_c"), "C2", Now.AddDays(1));

            Assert.That(manifest.Count, Is.EqualTo(1));
            manifest.TryGet("BuildingPrefab/V", out var entry);
            Assert.That(entry.Assets, Is.EquivalentTo(new[] { "UQ_a", "UQ_b", "UQ_c" }));
            Assert.That(entry.Cities, Is.EquivalentTo(new[] { "C1", "C2" }));
            Assert.That(entry.LastUsedUtc, Is.EqualTo(Now.AddDays(1)));
        }

        [Test]
        public void MarkUsage_AddsUsedAndRemovesUnusedCity()
        {
            var manifest = new VariantManifest();
            manifest.Register(Entry("Used"), "City", Now);
            manifest.Register(Entry("Unused"), "City", Now);
            manifest.MarkUsage("City", new HashSet<string> { "BuildingPrefab/Used" }, Now.AddDays(3));

            manifest.TryGet("BuildingPrefab/Used", out var used);
            manifest.TryGet("BuildingPrefab/Unused", out var unused);
            Assert.That(used.Cities, Is.EqualTo(new[] { "City" }));
            Assert.That(used.LastUsedUtc, Is.EqualTo(Now.AddDays(3)));
            Assert.That(unused.Cities, Is.Empty);
            Assert.That(unused.LastUsedUtc, Is.EqualTo(Now));
        }

        [Test]
        public void SaveAndLoad_RoundTrips()
        {
            using var temp = new TempDirectory();
            string path = temp.File("variants.json");
            var manifest = new VariantManifest();
            manifest.Register(Entry("V1", "UQ_a"), "City", Now);
            manifest.Save(path);
            Assert.That(manifest.IsDirty, Is.False);

            Assert.That(VariantManifest.Load(path, out var loaded), Is.EqualTo(JsonReadStatus.Ok));
            Assert.That(loaded.TryGet("BuildingPrefab/V1", out var entry), Is.True);
            Assert.That(entry.Assets, Is.EqualTo(new[] { "UQ_a" }));
            Assert.That(entry.Offsets.Length, Is.EqualTo(24));
            Assert.That(entry.LastUsedUtc, Is.EqualTo(Now));
            Assert.That(entry.LastUsedUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void MergeAssets_AddsNamesButKeepsUsage()
        {
            using var temp = new TempDirectory();
            var manifest = new VariantManifest();
            manifest.Register(Entry("V", "UQ_a"), "City", Now);
            manifest.Save(temp.File("m.json"));

            Assert.That(manifest.MergeAssets("BuildingPrefab/V", new[] { "UQ_a", "UQ_b" }), Is.True);
            Assert.That(manifest.MergeAssets("BuildingPrefab/V", new[] { "UQ_b" }), Is.False);
            Assert.That(manifest.MergeAssets("BuildingPrefab/Missing", new[] { "UQ_c" }), Is.False);

            manifest.TryGet("BuildingPrefab/V", out var entry);
            Assert.That(entry.Assets, Is.EqualTo(new[] { "UQ_a", "UQ_b" }));
            Assert.That(entry.LastUsedUtc, Is.EqualTo(Now));
            Assert.That(manifest.IsDirty, Is.True);
        }

        [Test]
        public void Load_OlderFormat_DiscardsEntries()
        {
            using var temp = new TempDirectory();
            string path = temp.File("old.json");
            System.IO.File.WriteAllText(path,
                "{\"Version\":1,\"Entries\":[{\"BaseType\":\"BuildingPrefab\",\"BaseName\":\"B\"," +
                "\"VariantName\":\"B__uq000000000001\",\"Offsets\":[],\"Assets\":[\"UQ_a\"]}]}");

            VariantManifest.Load(path, out var manifest);

            Assert.That(manifest.Count, Is.EqualTo(0));
            Assert.That(manifest.DiscardedOutdatedCount, Is.EqualTo(1));
            Assert.That(manifest.IsDirty, Is.True);
        }

        [Test]
        public void SaveAndLoad_CurrentFormat_KeepsEntries()
        {
            using var temp = new TempDirectory();
            string path = temp.File("m.json");
            var manifest = new VariantManifest();
            manifest.Register(Entry("V", "UQ_a"), "City", Now);
            manifest.Save(path);

            VariantManifest.Load(path, out var loaded);

            Assert.That(loaded.TryGet("BuildingPrefab/V", out _), Is.True);
            Assert.That(loaded.DiscardedOutdatedCount, Is.EqualTo(0));
        }

        [Test]
        public void Register_Existing_UpdatesFlatTerrain()
        {
            var manifest = new VariantManifest();
            manifest.Register(Entry("V"), "City", Now);
            var flat = Entry("V");
            flat.FlatTerrain = true;
            manifest.Register(flat, "City", Now);
            Assert.That(manifest.TryGet("BuildingPrefab/V", out var entry), Is.True);
            Assert.That(entry.FlatTerrain, Is.True);
        }

        [Test]
        public void Load_MissingFlatTerrain_DefaultsToLifted()
        {
            using var temp = new TempDirectory();
            string path = temp.File("m.json");
            System.IO.File.WriteAllText(path,
                "{\"Version\":" + VariantManifest.FormatVersion + ",\"Entries\":[{\"BaseType\":\"BuildingPrefab\"," +
                "\"BaseName\":\"B\",\"VariantName\":\"B__uq000000000001\",\"Offsets\":[],\"Assets\":[]}]}");

            VariantManifest.Load(path, out var manifest);

            Assert.That(manifest.TryGet("BuildingPrefab/B__uq000000000001", out var entry), Is.True);
            Assert.That(entry.FlatTerrain, Is.False);
        }

        [Test]
        public void AddExtension_ListsKeyAndAssetsOnce()
        {
            using var temp = new TempDirectory();
            var manifest = new VariantManifest();
            manifest.Register(Entry("V", "UQ_a"), "City", Now);
            manifest.Save(temp.File("m.json"));

            Assert.That(manifest.AddExtension("BuildingPrefab/V", "BuildingExtensionPrefab/Ext", new[] { "UQ_e" }),
                Is.True);
            Assert.That(manifest.AddExtension("BuildingPrefab/V", "BuildingExtensionPrefab/Ext", new[] { "UQ_e" }),
                Is.False);
            Assert.That(manifest.AddExtension("BuildingPrefab/Missing", "BuildingExtensionPrefab/Ext", new string[0]),
                Is.False);

            manifest.TryGet("BuildingPrefab/V", out var entry);
            Assert.That(entry.Extensions, Is.EqualTo(new[] { "BuildingExtensionPrefab/Ext" }));
            Assert.That(entry.Assets, Is.EqualTo(new[] { "UQ_a", "UQ_e" }));
            Assert.That(entry.LastUsedUtc, Is.EqualTo(Now));
            Assert.That(manifest.IsDirty, Is.True);
        }

        [Test]
        public void Register_Existing_KeepsExtensions()
        {
            var manifest = new VariantManifest();
            manifest.Register(Entry("V"), "City", Now);
            manifest.AddExtension("BuildingPrefab/V", "BuildingExtensionPrefab/Ext", new string[0]);
            manifest.Register(Entry("V"), "City", Now);

            manifest.TryGet("BuildingPrefab/V", out var entry);
            Assert.That(entry.Extensions, Is.EqualTo(new[] { "BuildingExtensionPrefab/Ext" }));
        }

        [Test]
        public void SaveAndLoad_RoundTripsExtensions()
        {
            using var temp = new TempDirectory();
            string path = temp.File("m.json");
            var manifest = new VariantManifest();
            manifest.Register(Entry("V"), "City", Now);
            manifest.AddExtension("BuildingPrefab/V", "BuildingExtensionPrefab/Ext", new[] { "UQ_e" });
            manifest.Save(path);

            VariantManifest.Load(path, out var loaded);

            Assert.That(loaded.TryGet("BuildingPrefab/V", out var entry), Is.True);
            Assert.That(entry.Extensions, Is.EqualTo(new[] { "BuildingExtensionPrefab/Ext" }));
        }

        [Test]
        public void Load_MissingExtensions_DefaultsToEmpty()
        {
            using var temp = new TempDirectory();
            string path = temp.File("m.json");
            System.IO.File.WriteAllText(path,
                "{\"Version\":" + VariantManifest.FormatVersion + ",\"Entries\":[{\"BaseType\":\"BuildingPrefab\"," +
                "\"BaseName\":\"B\",\"VariantName\":\"B__uq000000000001\",\"Offsets\":[],\"Assets\":[]," +
                "\"Extensions\":null}]}");

            VariantManifest.Load(path, out var manifest);

            Assert.That(manifest.TryGet("BuildingPrefab/B__uq000000000001", out var entry), Is.True);
            Assert.That(entry.Extensions, Is.Empty);
        }

        [Test]
        public void Remove_MarksDirty()
        {
            using var temp = new TempDirectory();
            var manifest = new VariantManifest();
            manifest.Register(Entry("V"), null, Now);
            manifest.Save(temp.File("m.json"));
            Assert.That(manifest.Remove("BuildingPrefab/V"), Is.True);
            Assert.That(manifest.IsDirty, Is.True);
        }
    }
}
