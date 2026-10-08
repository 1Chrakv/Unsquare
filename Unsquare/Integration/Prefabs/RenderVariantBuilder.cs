using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Game.Prefabs;
using Unsquare.Core.Deformation;
using Unsquare.Core.Naming;
using Unsquare.Core.Persistence;
using Unsquare.Integration.Assets;
using Unsquare.Integration.Meshes;
using Object = UnityEngine.Object;

namespace Unsquare.Integration.Prefabs
{
    /// <summary>
    /// Produces deformed clones of render prefabs (incl. LODs) backed by baked GeometryAssets.
    /// Results are cached per asset name; failures are cached too so they are not retried every frame.
    /// </summary>
    internal sealed class RenderVariantBuilder
    {
        private readonly GeometryAssetStore _store;
        private readonly Dictionary<string, Built> _cache = new(StringComparer.Ordinal);

        public RenderVariantBuilder(GeometryAssetStore store)
        {
            _store = store;
        }

        /// <summary>Replaced on every preload; lets restores skip vertex work.</summary>
        public BoundsCache BoundsCache { get; set; } = new();

        /// <summary>Geometry assets written to disk this session; lets callers report reuse.</summary>
        public int CreatedAssetCount { get; private set; }

        /// <summary>Returns the deformed clone or null; adds every asset name it relies on to <paramref name="usedAssets"/>.</summary>
        public RenderPrefab GetOrBuild(RenderPrefab source, in SubmeshTransform transform, IDeformation deformation,
            string deformKey, ISet<string> usedAssets)
        {
            var hash = AssetNaming.Hash(source.name, DeformKey.ForSubmesh(deformKey, transform));
            string assetName = AssetNaming.AssetName(hash);
            if (!_cache.TryGetValue(assetName, out var built))
            {
                built = TryBuild(source, transform, deformation, deformKey, hash, assetName);
                _cache[assetName] = built;
            }
            if (built.Prefab == null)
                return null;
            usedAssets.UnionWith(built.Assets);
            return built.Prefab;
        }

        /// <summary>Forgets cached results, e.g. after assets were deleted.</summary>
        public void Clear() => _cache.Clear();

        private Built TryBuild(RenderPrefab source, SubmeshTransform transform, IDeformation deformation,
            string deformKey, StableHash hash, string assetName)
        {
            try
            {
                var assets = new HashSet<string>(StringComparer.Ordinal) { assetName };
                var asset = ObtainAsset(source, transform, deformation, hash, assetName, out var bounds);

                var clone = (RenderPrefab)source.Clone(assetName);
                clone.Remove<ObsoleteIdentifiers>();
                clone.geometryAsset = asset;
                clone.bounds = new Bounds3(bounds.Min, bounds.Max);

                if (!TryDeformLods(clone, source, transform, deformation, deformKey, assets))
                {
                    Object.Destroy(clone);
                    return Built.Failed;
                }
                return new Built(clone, assets);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Baking render variant of {source.name} failed.");
                return Built.Failed;
            }
        }

        private GeometryAsset ObtainAsset(RenderPrefab source, SubmeshTransform transform, IDeformation deformation,
            StableHash hash, string assetName, out MinMaxBounds bounds)
        {
            bool exists = _store.TryGet(hash, out var asset);
            if (exists && BoundsCache.TryGet(assetName, out bounds))
                return asset;

            var meshes = MeshDeformer.DeformCopies(source.ObtainMeshes(), transform, deformation, out bounds);
            if (exists)
                MeshDeformer.Destroy(meshes); // only needed the bounds
            else
            {
                asset = _store.Create(assetName, hash, meshes); // asset owns the meshes now
                CreatedAssetCount++;
            }
            BoundsCache.Set(assetName, bounds);
            return asset;
        }

        // Without deformed LODs the original shape pops back at a distance.
        private bool TryDeformLods(RenderPrefab clone, RenderPrefab source, SubmeshTransform transform,
            IDeformation deformation, string deformKey, ISet<string> assets)
        {
            var lod = PrefabCloning.GetOwnedComponent<LodProperties>(clone, source);
            if (lod == null || lod.m_LodMeshes == null)
                return true;

            var lods = new RenderPrefab[lod.m_LodMeshes.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                var lodSource = lod.m_LodMeshes[i];
                if (lodSource == null)
                    continue;
                lods[i] = GetOrBuild(lodSource, transform, deformation, deformKey, assets);
                if (lods[i] == null)
                    return false;
            }
            lod.m_LodMeshes = lods;
            return true;
        }

        private sealed class Built
        {
            public static readonly Built Failed = new(null, Array.Empty<string>());

            public Built(RenderPrefab prefab, ICollection<string> assets)
            {
                Prefab = prefab;
                Assets = assets;
            }

            public RenderPrefab Prefab { get; }
            public ICollection<string> Assets { get; }
        }
    }
}
