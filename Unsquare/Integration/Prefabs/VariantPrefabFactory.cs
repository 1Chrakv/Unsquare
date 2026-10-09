using System;
using System.Collections.Generic;
using Game.Prefabs;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Naming;
using Unsquare.Integration.Prefabs.Attachments;
using Object = UnityEngine.Object;

namespace Unsquare.Integration.Prefabs
{
    /// <summary>
    /// Clones a building prefab with deformed render meshes and attachments (props, …) and registers it.
    /// The clone is hidden from menus, Find It and the zone spawner.
    /// </summary>
    internal sealed class VariantPrefabFactory
    {
        public const string VariantCategory = "Unsquare/Variants";
        private const string FindItCategory = "FindIt";

        private readonly PrefabSystem _prefabSystem;
        private readonly RenderVariantBuilder _renderBuilder;
        private readonly IReadOnlyList<IAttachmentDeformer> _attachmentDeformers;

        public VariantPrefabFactory(PrefabSystem prefabSystem, RenderVariantBuilder renderBuilder,
            IReadOnlyList<IAttachmentDeformer> attachmentDeformers)
        {
            _prefabSystem = prefabSystem;
            _renderBuilder = renderBuilder;
            _attachmentDeformers = attachmentDeformers;
        }

        public bool TryCreate(ObjectGeometryPrefab basePrefab, string variantName, CageBox box, CageScope scope,
            CageOffsets offsets,
            ISet<string> usedAssets, out ObjectGeometryPrefab variant)
        {
            variant = null;
            var deformation = new TrilinearCageDeformation(box, offsets);
            var context = new AttachmentContext(deformation, box, scope);
            if (!TryDeformMeshes(basePrefab, deformation, DeformKey.Create(box, offsets), usedAssets, out var meshes))
                return false;

            ObjectGeometryPrefab clone = null;
            try
            {
                clone = (ObjectGeometryPrefab)basePrefab.Clone(variantName);
                clone.m_Meshes = meshes;
                clone.Remove<SpawnableBuilding>();   // the zone spawner must never pick a variant
                clone.Remove<ObsoleteIdentifiers>(); // would alias the base prefab's old IDs
                clone.Remove<UIObject>();            // keep it out of build menus
                HideFromAssetBrowsers(clone, basePrefab);
                foreach (var attachmentDeformer in _attachmentDeformers)
                    attachmentDeformer.Apply(clone, basePrefab, context);

                if (!_prefabSystem.AddPrefab(clone))
                {
                    Mod.Log.Warn($"PrefabSystem rejected variant {variantName}.");
                    Object.Destroy(clone);
                    return false;
                }
                variant = clone;
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Creating variant {variantName} failed.");
                if (clone != null)
                    Object.Destroy(clone);
                return false;
            }
        }

        private bool TryDeformMeshes(ObjectGeometryPrefab basePrefab, IDeformation deformation, string deformKey,
            ISet<string> usedAssets, out ObjectMeshInfo[] meshes)
        {
            meshes = new ObjectMeshInfo[basePrefab.m_Meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                var info = PrefabCloning.ShallowCopy(basePrefab.m_Meshes[i]);
                meshes[i] = info;
                if (!(info.m_Mesh is RenderPrefab render))
                    continue;
                var transform = new SubmeshTransform(info.m_Position, info.m_Rotation);
                var deformed = _renderBuilder.GetOrBuild(render, transform, deformation, deformKey, usedAssets);
                if (deformed == null)
                    return false;
                info.m_Mesh = deformed;
            }
            return true;
        }

        private static void HideFromAssetBrowsers(ObjectGeometryPrefab clone, PrefabBase source)
        {
            var category = PrefabCloning.GetOwnedComponent<EditorAssetCategoryOverride>(clone, source);
            if (category == null)
                category = clone.AddComponent<EditorAssetCategoryOverride>();
            category.m_IncludeCategories = new[] { VariantCategory };
            category.m_ExcludeCategories = new[] { FindItCategory };
        }
    }
}
