using System;
using System.Collections.Generic;
using Colossal.Entities;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Import;
using Unsquare.Core.Naming;
using Unsquare.Core.Options;
using Unsquare.Core.PlacemakerCompatibility;
using Unsquare.Integration.Systems;

namespace Unsquare.Integration.PlacemakerCompatibility
{
    /// <summary>
    /// Buildings Placemaker has bent: tells them, works out the Unsquare cage of their shape, and keeps which ones
    /// the player took over (adopted) until their swap. Runtime only; cleared on load.
    /// </summary>
    internal sealed class PlacemakerAdoptions : IForeignPrefabs
    {
        // Longest row converted at once.
        private const int MaxRowSize = 64;

        private readonly World _world;
        private readonly EntityManager _entityManager;
        private readonly PrefabSystem _prefabSystem;
        private readonly LotBoxReader _boxes;
        private readonly Func<CageLimits> _limits;
        private readonly Dictionary<Entity, ObjectGeometryPrefab> _adopted = new();
        private readonly Dictionary<Entity, (ObjectGeometryPrefab Base, PlacemakerAdoption Shape)> _shapes = new();
        private PlacemakerBridge _bridge;
        private bool _bridgeLooked;
        private bool _loaded;

        public PlacemakerAdoptions(World world, PrefabSystem prefabSystem, LotBoxReader boxes, Func<CageLimits> limits)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _entityManager = world.EntityManager;
            _prefabSystem = prefabSystem ?? throw new ArgumentNullException(nameof(prefabSystem));
            _boxes = boxes ?? throw new ArgumentNullException(nameof(boxes));
            _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        }

        /// <summary>On a Placemaker variant and not taken over yet: it can't be described (so not edited) until then.</summary>
        public bool IsUndecided(Entity building) =>
            !_adopted.ContainsKey(building) && TryGetCurrent(building, out _, out var current) &&
            IsForeignPrefab(building, current);

        /// <summary>
        /// Takes a Placemaker building over: it is described on Placemaker's plain base from now on, and requesting
        /// the returned spec swaps it straight to an Unsquare variant of the same shape.
        /// </summary>
        public PlacemakerAdoption Prepare(Entity building)
        {
            if (!_loaded || !TryGetCurrent(building, out var prefab, out var current) ||
                !IsForeignPrefab(building, current))
                return new PlacemakerAdoption(PlacemakerAdoptionStatus.Failed);
            if (!TryGetBridge(out var bridge))
                return new PlacemakerAdoption(PlacemakerAdoptionStatus.PlacemakerMissing);
            // A shape Placemaker is still baking or swapping in would replace ours after our swap.
            if (!bridge.TryIsBusyWith(building, out bool busy))
                return new PlacemakerAdoption(PlacemakerAdoptionStatus.PlacemakerMissing);
            if (busy)
                return new PlacemakerAdoption(PlacemakerAdoptionStatus.PlacemakerBusy);
            var shape = GetShape(prefab, current.name, out var basePrefab);
            if (shape.Status != PlacemakerAdoptionStatus.Ready)
                return shape;
            _adopted[building] = basePrefab;
            Mod.Log.Info($"Building {building}: adopting {current.name} on {basePrefab.name}.");
            return shape;
        }

        /// <summary>
        /// The undecided buildings in the building's row: itself and every house Placemaker joined to it, directly or
        /// along the row. Empty without Placemaker.
        /// </summary>
        public IReadOnlyList<Entity> Row(Entity building)
        {
            var row = new List<Entity>();
            if (!TryGetBridge(out var bridge))
                return row;
            bool Neighbours(Entity house, out Entity left, out Entity right)
            {
                bool read = bridge.TryGetJoinedNeighbours(house, out left, out right);
                left = IsBuilding(left) ? left : Entity.Null;
                right = IsBuilding(right) ? right : Entity.Null;
                return read;
            }

            foreach (var member in RowCollector.Collect(building, Entity.Null, Neighbours, MaxRowSize))
            {
                // Plain or already decided houses are walked through but keep what they have.
                if (IsBuilding(member) && IsUndecided(member))
                    row.Add(member);
            }
            return row;
        }

        // Placemaker's light copies share the plain prefab's PrefabData (and so its name): ask Placemaker by entity.
        public bool IsForeignPrefab(Entity building, PrefabBase current)
        {
            if (PlacemakerVariantName.IsVariant(current.name))
                return true;
            if (VariantNaming.IsVariantName(current.name) || !TryGetBridge(out var bridge))
                return false;
            return _entityManager.TryGetComponent(building, out PrefabRef prefabRef) &&
                   bridge.IsVariant(prefabRef.m_Prefab);
        }

        public bool TryGetTakenOverBase(Entity building, out ObjectGeometryPrefab basePrefab) =>
            _adopted.TryGetValue(building, out basePrefab);

        public bool TryGetPreview(Entity building, out ObjectGeometryPrefab basePrefab, out DeformSpec spec)
        {
            basePrefab = null;
            spec = null;
            if (!IsBuilding(building) || !TryGetCurrent(building, out var prefab, out var current) ||
                _adopted.ContainsKey(building) || !IsForeignPrefab(building, current))
                return false;
            var shape = GetShape(prefab, current.name, out basePrefab);
            spec = shape.Spec;
            return shape.Status == PlacemakerAdoptionStatus.Ready;
        }

        public void Release(Entity building) => _adopted.Remove(building);

        public void ReleaseAll() => _adopted.Clear();

        public void Clear()
        {
            _adopted.Clear();
            _shapes.Clear();
            _bridge = null;
            _bridgeLooked = false;
            _loaded = false;
        }

        public void OnLoaded()
        {
            _loaded = true;
            TryGetBridge(out _); // logs Placemaker's version and which features work, once per load
        }

        private bool TryGetCurrent(Entity building, out Entity prefab, out PrefabBase current)
        {
            prefab = Entity.Null;
            current = null;
            if (!_entityManager.TryGetComponent(building, out PrefabRef prefabRef) ||
                !_prefabSystem.TryGetPrefab(prefabRef, out current) || current == null)
                return false;
            prefab = prefabRef.m_Prefab;
            return true;
        }

        // Placemaker's plan may still name houses that were demolished since.
        private bool IsBuilding(Entity entity) =>
            entity != Entity.Null && _entityManager.Exists(entity) && _entityManager.HasComponent<Building>(entity) &&
            !_entityManager.HasComponent<Deleted>(entity);

        private bool TryGetBridge(out PlacemakerBridge bridge)
        {
            if (!_bridgeLooked)
            {
                PlacemakerBridge.TryFind(_world, out _bridge);
                _bridgeLooked = true;
            }
            bridge = _bridge;
            return bridge != null;
        }

        // A Placemaker variant's shape never changes, so its conversion is worked out once per prefab.
        private PlacemakerAdoption GetShape(Entity variantPrefab, string variantName, out ObjectGeometryPrefab basePrefab)
        {
            if (!_shapes.TryGetValue(variantPrefab, out var cached))
            {
                var shape = ConvertShape(variantPrefab, variantName, out var found);
                cached = (found, shape);
                _shapes[variantPrefab] = cached;
            }
            basePrefab = cached.Base;
            return cached.Shape;
        }

        private PlacemakerAdoption ConvertShape(Entity variantPrefab, string variantName,
            out ObjectGeometryPrefab basePrefab)
        {
            basePrefab = null;
            var failed = new PlacemakerAdoption(PlacemakerAdoptionStatus.Failed);
            if (!TryGetBridge(out var bridge))
                return new PlacemakerAdoption(PlacemakerAdoptionStatus.PlacemakerMissing);
            if (!bridge.TryGetCage(variantPrefab, out var cage) || !bridge.TryGetBase(variantPrefab, out var baseEntity))
                return new PlacemakerAdoption(PlacemakerAdoptionStatus.PlacemakerMissing);
            if (!_prefabSystem.TryGetPrefab(baseEntity, out PrefabBase found) ||
                !(found is ObjectGeometryPrefab geometry) || !_boxes(geometry, out var lotBox, out var buildingBox))
            {
                Mod.Log.Warn($"{variantName}: no base prefab with a lot.");
                return failed;
            }
            // Placemaker skips prefabs without an asset, i.e. ours; bending one anyway means that rule changed.
            if (VariantNaming.IsVariantName(geometry.name))
            {
                Mod.Log.Warn($"{variantName}: Placemaker {bridge.Version} bent the Unsquare variant {geometry.name}. " +
                             $"Tested with {PlacemakerBridge.TestedWith}, which leaves them alone; not converted.");
                return failed;
            }
            if (!cage.IsPlausible(lotBox))
            {
                Mod.Log.Warn($"{variantName}: Placemaker {bridge.Version} gave an implausible shape ({cage}) for " +
                             $"the lot {lotBox}; not converted.");
                return failed;
            }

            var spec = new DeformSpec(PlacemakerCageConverter.ToOffsets(lotBox, cage), false);
            bool approximated = false;
            float maxError = 0f;
            // Extending the body's shape over a deep yard can fold the lot: fit Placemaker's held yard shift instead.
            if (CageValidator.ValidateSplit(lotBox, buildingBox, spec, _limits()) != CageValidity.Valid)
            {
                var samples = new List<float2>(PlanCageFit.Outline(buildingBox.Min.xz, buildingBox.Max.xz, 16));
                samples.AddRange(PlanCageFit.Outline(lotBox.Min.xz, lotBox.Max.xz, 16));
                if (!PlacemakerCageConverter.TryFitOffsets(lotBox, samples.ToArray(), cage, out var fitted,
                        out maxError))
                    return failed;
                spec = new DeformSpec(fitted, false);
                approximated = true;
            }
            basePrefab = geometry;
            Mod.Log.Info($"Placemaker shape {variantName} ({cage}) on {geometry.name}: " +
                         (approximated ? $"fitted, max deviation {maxError:0.00} m." : "exact."));
            return new PlacemakerAdoption(PlacemakerAdoptionStatus.Ready, spec, approximated, maxError);
        }
    }
}
