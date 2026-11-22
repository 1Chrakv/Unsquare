using System;
using System.Collections.Generic;
using System.IO;
using Colossal.IO.AssetDatabase;
using Hash128 = Colossal.Hash128;
using Mesh = UnityEngine.Mesh;
using Unsquare.Core.Housekeeping;
using Unsquare.Core.Naming;

namespace Unsquare.Integration.Assets
{
    /// <summary>Creates, finds and deletes Unsquare's baked GeometryAssets in the user asset database.</summary>
    internal sealed class GeometryAssetStore
    {
        public bool TryGet(StableHash hash, out GeometryAsset asset) =>
            AssetDatabase.global.TryGetAsset(ToGuid(hash), out asset);

        /// <summary>Saves the meshes as a new asset and unloads it; the game reloads it on demand.</summary>
        public GeometryAsset Create(string assetName, StableHash hash, Mesh[] meshes)
        {
            var path = AssetDataPath.Create(ModPaths.AssetSubPath, assetName + GeometryAsset.kExtension,
                hasExtension: true, EscapeStrategy.None);
            var asset = AssetDatabase.user.AddAsset<GeometryAsset, Mesh[]>(path, meshes, ToGuid(hash));
            asset.Save(force: false);
            asset.Unload(force: true);
            return asset;
        }

        /// <summary>Own geometry asset files on disk (name without extension, size, last write), for the cache.</summary>
        /// <remarks>Read from the folder, not the asset database: it has the sizes, and the .cid sidecars are tiny.</remarks>
        public List<CachedAsset> ListOwnAssets()
        {
            var assets = new List<CachedAsset>();
            var directory = new DirectoryInfo(ModPaths.DataDirectory);
            if (!directory.Exists)
                return assets;
            foreach (var file in directory.EnumerateFiles("*" + GeometryAsset.kExtension, SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileNameWithoutExtension(file.Name);
                if (AssetNaming.IsOwnAssetName(name))
                    assets.Add(new CachedAsset(name, file.Length, file.LastWriteTimeUtc));
            }
            return assets;
        }

        /// <summary>Deletes own assets by name through the asset database; returns the names actually deleted.</summary>
        public List<string> Delete(ICollection<string> assetNames)
        {
            var deleted = new List<string>();
            if (assetNames.Count == 0)
                return deleted;
            var targets = new List<GeometryAsset>();
            foreach (var asset in AssetDatabase.user.GetAssets(default(SearchFilter<GeometryAsset>)))
            {
                string name = NameOf(asset);
                if (AssetNaming.IsOwnAssetName(name) && assetNames.Contains(name))
                    targets.Add(asset);
            }

            foreach (var asset in targets)
            {
                try
                {
                    string name = NameOf(asset);
                    asset.Delete();
                    deleted.Add(name);
                }
                catch (Exception e)
                {
                    Mod.Log.Warn($"Could not delete geometry asset {asset.name}: {e.Message}");
                }
            }
            return deleted;
        }

        public static Hash128 ToGuid(StableHash hash) => new(hash.Word(0), hash.Word(1), hash.Word(2), hash.Word(3));

        private static string NameOf(GeometryAsset asset) => Path.GetFileNameWithoutExtension(asset.name ?? string.Empty);
    }
}
