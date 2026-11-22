using System;
using System.Collections.Generic;
using Game.Prefabs;
using Unsquare.Core.Caching;
using Mesh = UnityEngine.Mesh;

namespace Unsquare.Integration.Meshes
{
    /// <summary>
    /// Keeps the meshes of recently baked base render prefabs loaded, within a vertex budget, and releases the rest.
    /// Every obtain on a GeometryAsset counts a reference; without a release its meshes stay loaded all session.
    /// </summary>
    internal sealed class SourceMeshCache
    {
        private readonly Dictionary<RenderPrefab, Mesh[]> _held = new();
        private readonly LruBudget<RenderPrefab> _order;
        private readonly List<RenderPrefab> _evicted = new();

        public SourceMeshCache(long budgetVertices)
        {
            _order = new LruBudget<RenderPrefab>(budgetVertices);
        }

        public long BudgetVertices
        {
            get => _order.Budget;
            set => _order.Budget = value;
        }

        public int Count => _order.Count;
        public long HeldVertices => _order.TotalCost;

        /// <summary>Meshes of <paramref name="source"/>, valid until the next call; null if it has no geometry.</summary>
        public Mesh[] Obtain(RenderPrefab source)
        {
            if (!_held.TryGetValue(source, out var meshes) || !AllAlive(meshes))
            {
                if (meshes != null)
                    Release(source); // destroyed under us: drop our reference and load again
                meshes = source.ObtainMeshes();
                if (meshes == null)
                    return null;
                _held[source] = meshes;
            }
            _order.Touch(source, Math.Max(0, source.vertexCount));
            _evicted.Clear();
            _order.CollectOverBudget(_evicted);
            foreach (var evicted in _evicted)
                Release(evicted);
            return meshes;
        }

        /// <summary>Releases every held source, e.g. when a city is left or after a bulk load.</summary>
        public void ReleaseAll()
        {
            if (_held.Count == 0)
                return;
            int count = _held.Count;
            long vertices = _order.TotalCost;
            foreach (var source in new List<RenderPrefab>(_held.Keys))
                Release(source);
            _order.Clear();
            Mod.Log.Info($"Released source meshes of {count} render prefabs ({vertices} vertices).");
        }

        // Exactly one release per obtain, so references the game holds itself stay balanced.
        private void Release(RenderPrefab source)
        {
            _held.Remove(source);
            _order.Remove(source);
            try
            {
                if (source != null)
                    source.ReleaseMeshes();
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"Releasing source meshes of {source.name} failed: {e.Message}");
            }
        }

        private static bool AllAlive(Mesh[] meshes)
        {
            foreach (var mesh in meshes)
            {
                if (mesh == null)
                    return false;
            }
            return true;
        }
    }
}
