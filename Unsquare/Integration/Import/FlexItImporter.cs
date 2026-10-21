using System;
using Colossal.Entities;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unsquare.Core.Cage;
using Unsquare.Core.Import;
using Unsquare.Core.Naming;
using Unsquare.Integration.Prefabs;
using Unsquare.Integration.Systems;

namespace Unsquare.Integration.Import
{
    /// <summary>
    /// Moves every FlexIt building in the loaded city back to its base prefab, clears its FlexIt shear and works out
    /// the Unsquare cage (exact for tapers and shears, closest fit for corners); arch and bend stay on the base.
    /// </summary>
    internal sealed class FlexItImporter
    {
        private const int OutlineSamplesPerSide = 16;

        private readonly World _world;
        private readonly PrefabSystem _prefabSystem;
        private readonly IVariantPipeline _pipeline;
        private readonly EntityQuery _buildingQuery;

        public FlexItImporter(World world, PrefabSystem prefabSystem, IVariantPipeline pipeline, EntityQuery buildings)
        {
            _world = world;
            _prefabSystem = prefabSystem;
            _pipeline = pipeline;
            _buildingQuery = buildings;
        }

        private EntityManager EntityManager => _world.EntityManager;

        public FlexItImportBatch Prepare()
        {
            var batch = new FlexItImportBatch();
            if (!FlexItBridge.TryFind(_world, out var bridge))
            {
                batch.FlexItMissing = true;
                return batch;
            }

            var skews = bridge.ReadSkews();
            using var buildings = _buildingQuery.ToEntityArray(Allocator.Temp);
            foreach (var building in buildings)
            {
                try
                {
                    PrepareBuilding(building, bridge, skews.TryGetValue(building, out var skew) ? skew : null, batch);
                }
                catch (Exception e)
                {
                    batch.FailedCount++;
                    Mod.Log.Error(e, $"FlexIt import of building {building} failed.");
                }
            }
            Mod.Log.Info($"FlexIt import: {batch.Conversions.Count} to convert, {batch.ResetCount} curved reset, " +
                         $"{batch.FailedCount} failed ({skews.Count} FlexIt skews).");
            return batch;
        }

        private void PrepareBuilding(Entity building, FlexItBridge bridge, (float Degrees, bool LeanFront)? skew,
            FlexItImportBatch batch)
        {
            if (!EntityManager.TryGetComponent(building, out PrefabRef prefabRef) ||
                !_prefabSystem.TryGetPrefab(prefabRef, out PrefabBase current) || current == null)
                return;
            FlexItVariant? variant = FlexItVariantName.TryParse(current.name, out var parsed) ? parsed : null;
            FlexItShear? shear = null;
            if (skew.HasValue)
            {
                // FlexIt shears around the street edge of the prefab the building is on now.
                FlexItFootprint.TryGetFrontZ(EntityManager, prefabRef.m_Prefab, out float shearPivot);
                shear = new FlexItShear(skew.Value.Degrees, skew.Value.LeanFront, shearPivot);
            }

            var action = FlexItImportPlanner.Decide(variant, shear);
            if (action == FlexItImportAction.Skip)
                return;
            string source = variant.HasValue ? current.name : $"{current.name} (shear {shear})";
            if (!TryGetBase(building, current, variant, out var basePrefab, out var baseEntity, out var applied))
            {
                batch.FailedCount++;
                Mod.Log.Warn($"FlexIt import: no base prefab for {source} (building {building}).");
                return;
            }

            CageOffsets offsets = null;
            if (action == FlexItImportAction.Convert &&
                !TryConvert(basePrefab, baseEntity, applied.Offsets, variant, shear, out offsets))
            {
                batch.FailedCount++;
                Mod.Log.Warn($"FlexIt import: {basePrefab.name} has no lot, cannot convert {source}.");
                return;
            }
            if (action == FlexItImportAction.Approximate)
            {
                if (TryApproximate(basePrefab, baseEntity, applied.Offsets, variant.Value, shear, out offsets,
                        out float error))
                    Mod.Log.Info($"FlexIt import: building {building} approximated corner fit {source} " +
                                 $"(max deviation {error:0.0} m).");
                else
                    action = FlexItImportAction.ResetToBase;
            }

            // Only FlexIt variants need a rebase; Unsquare's own swap remaps its lot from the applied cage.
            // Rebase first: if that fails, the building keeps its FlexIt look completely.
            if (variant.HasValue && !_pipeline.TryRebase(building, basePrefab))
            {
                batch.FailedCount++;
                Mod.Log.Warn($"FlexIt import: could not move building {building} off {source}.");
                return;
            }
            if (shear.HasValue)
                bridge.TryRemoveSkew(building);

            if (action == FlexItImportAction.ResetToBase)
            {
                batch.ResetCount++;
                Mod.Log.Info($"FlexIt import: building {building} reset from curved {source}.");
            }
            else
            {
                bool approximated = action == FlexItImportAction.Approximate;
                batch.Conversions.Add((building, applied.WithOffsets(offsets), source, approximated));
            }
        }

        // A sheared building may already be on an Unsquare variant (both mods used): its cage is the starting point.
        private bool TryGetBase(Entity building, PrefabBase current, FlexItVariant? variant,
            out ObjectGeometryPrefab basePrefab, out Entity baseEntity, out DeformSpec applied)
        {
            baseEntity = Entity.Null;
            basePrefab = null;
            applied = DeformSpec.Identity;
            if (variant.HasValue)
            {
                var id = new PrefabID(current.GetType().Name, variant.Value.BaseName);
                if (_prefabSystem.TryGetPrefab(id, out PrefabBase found))
                    basePrefab = found as ObjectGeometryPrefab;
            }
            else if (VariantNaming.IsVariantName(current.name))
            {
                if (_pipeline.TryDescribe(building, out var description))
                {
                    basePrefab = description.BasePrefab;
                    applied = description.Applied;
                }
            }
            else
            {
                basePrefab = current as ObjectGeometryPrefab;
            }
            return basePrefab != null && _prefabSystem.TryGetEntity(basePrefab, out baseEntity);
        }

        private bool TryConvert(ObjectGeometryPrefab basePrefab, Entity baseEntity, CageOffsets applied,
            FlexItVariant? variant, FlexItShear? shear, out CageOffsets offsets)
        {
            offsets = null;
            if (!CageBoxMeasurer.TryMeasureLot(basePrefab, out var lotBox))
                return false;
            FlexItTaper? taper = null;
            if (variant.HasValue)
            {
                float halfDepth = FlexItFootprint.HalfExtent(basePrefab, alongX: false);
                // Without geometry data FlexIt pivots on its measured half depth.
                if (!FlexItFootprint.TryGetFrontZ(EntityManager, baseEntity, out float pivotZ))
                    pivotZ = halfDepth;
                taper = new FlexItTaper(variant.Value, FlexItFootprint.HalfExtent(basePrefab, alongX: true),
                    halfDepth, pivotZ);
            }
            offsets = FlexItCageConverter.ToOffsets(lotBox, applied, taper, shear);
            return true;
        }

        // Fitted over the mesh outline: the facades are what the player compares with the FlexIt look.
        private bool TryApproximate(ObjectGeometryPrefab basePrefab, Entity baseEntity, CageOffsets applied,
            FlexItVariant variant, FlexItShear? shear, out CageOffsets offsets, out float maxError)
        {
            offsets = null;
            maxError = 0f;
            if (!CageBoxMeasurer.TryMeasureLot(basePrefab, out var lotBox))
                return false;
            if (!CageBoxMeasurer.TryMeasureMeshes(basePrefab, out var meshBox))
                meshBox = lotBox;

            int side = variant.B >= 0 ? 1 : -1;
            float halfWidth = FlexItFootprint.HalfExtent(basePrefab, alongX: true);
            if (!FlexItFootprint.TryGetCornerX(EntityManager, baseEntity, side, out float cornerX))
                cornerX = side * halfWidth;
            if (!FlexItFootprint.TryGetFrontZ(EntityManager, baseEntity, out float pivotZ))
                pivotZ = FlexItFootprint.HalfExtent(basePrefab, alongX: false);
            var corner = new FlexItCorner(variant.A, side, halfWidth, cornerX, pivotZ,
                FlexItFootprint.BlendHalf(basePrefab));

            var samples = PlanCageFit.Outline(meshBox.Min.xz, meshBox.Max.xz, OutlineSamplesPerSide);
            return FlexItCageConverter.TryFitOffsets(lotBox, applied, samples, corner, shear, out offsets,
                out maxError);
        }
    }
}
