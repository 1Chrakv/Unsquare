using System;
using System.Collections.Generic;
using System.IO;
using Colossal.IO.AssetDatabase;
using Hash128 = Colossal.Hash128;
using Mesh = UnityEngine.Mesh;
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

        /// <summary>Names (without extension) of all geometry assets that carry our prefix.</summary>
        public List<string> ListOwnAssetNames()
        {
            var names = new List<string>();
            foreach (var asset in AssetDatabase.user.GetAssets(default(SearchFilter<GeometryAsset>)))
            {
                string name = NameOf(asset);
                if (AssetNaming.IsOwnAssetName(name))
                    names.Add(name);
            }
            return names;
        }

        /// <summary>Deletes own assets by name and returns how many were removed.</summary>
        public int Delete(ICollection<string> assetNames)
        {
            if (assetNames.Count == 0)
                return 0;
            var targets = new List<GeometryAsset>();
            foreach (var asset in AssetDatabase.user.GetAssets(default(SearchFilter<GeometryAsset>)))
            {
                string name = NameOf(asset);
                if (AssetNaming.IsOwnAssetName(name) && assetNames.Contains(name))
                    targets.Add(asset);
            }

            int deleted = 0;
            foreach (var asset in targets)
            {
                try
                {
                    asset.Delete();
                    deleted++;
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
