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
        private const string SkippedLodSuffix = "-fine";

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
        /// <param name="skipCoarsestLod">Leaves out the coarsest LOD, whose large triangles can't follow a twist.</param>
        public RenderPrefab GetOrBuild(RenderPrefab source, in SubmeshTransform transform, IDeformation deformation,
            string deformKey, bool skipCoarsestLod, ISet<string> usedAssets)
        {
            var hash = AssetNaming.Hash(source.name, DeformKey.ForSubmesh(deformKey, transform));
            string assetName = AssetNaming.AssetName(hash);
            // Both LOD sets may exist in one session (threshold changed), so they need distinct prefab names.
            string cloneName = skipCoarsestLod ? assetName + SkippedLodSuffix : assetName;
            if (!_cache.TryGetValue(cloneName, out var built))
            {
                built = TryBuild(source, transform, deformation, deformKey, skipCoarsestLod, hash, assetName,
                    cloneName);
                _cache[cloneName] = built;
            }
            if (built.Prefab == null)
                return null;
            usedAssets.UnionWith(built.Assets);
            return built.Prefab;
        }

        /// <summary>Forgets cached results, e.g. after assets were deleted.</summary>
        public void Clear() => _cache.Clear();

        private Built TryBuild(RenderPrefab source, SubmeshTransform transform, IDeformation deformation,
            string deformKey, bool skipCoarsestLod, StableHash hash, string assetName, string cloneName)
        {
            try
            {
                var assets = new HashSet<string>(StringComparer.Ordinal) { assetName };
                var asset = ObtainAsset(source, transform, deformation, hash, assetName, out var bounds);

                var clone = (RenderPrefab)source.Clone(cloneName);
                clone.Remove<ObsoleteIdentifiers>();
                clone.geometryAsset = asset;
                clone.bounds = new Bounds3(bounds.Min, bounds.Max);

                if (!TryDeformLods(clone, source, transform, deformation, deformKey, skipCoarsestLod, assets))
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
            IDeformation deformation, string deformKey, bool skipCoarsestLod, ISet<string> assets)
        {
            var lod = PrefabCloning.GetOwnedComponent<LodProperties>(clone, source);
            if (lod == null || lod.m_LodMeshes == null)
                return true;

            int skipped = skipCoarsestLod ? CoarsestIndex(lod.m_LodMeshes) : -1;
            var lods = new List<RenderPrefab>(lod.m_LodMeshes.Length);
            for (int i = 0; i < lod.m_LodMeshes.Length; i++)
            {
                var lodSource = lod.m_LodMeshes[i];
                if (lodSource == null || i == skipped)
                    continue;
                var deformed = GetOrBuild(lodSource, transform, deformation, deformKey, false, assets);
                if (deformed == null)
                    return false;
                lods.Add(deformed);
            }
            lod.m_LodMeshes = lods.ToArray();
            return true;
        }

        // The game orders LODs by index count, so the coarsest has the fewest; keep at least one LOD.
        private static int CoarsestIndex(RenderPrefab[] lods)
        {
            int coarsest = -1, present = 0;
            for (int i = 0; i < lods.Length; i++)
            {
                if (lods[i] == null)
                    continue;
                present++;
                if (coarsest < 0 || lods[i].indexCount < lods[coarsest].indexCount)
                    coarsest = i;
            }
            return present >= 2 ? coarsest : -1;
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
