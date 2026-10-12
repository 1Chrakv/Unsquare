using System;
using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game;
using Game.Simulation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Transform = Game.Objects.Transform;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// Samples the original terrain (before lots and roads flattened it) under a building. Only the GPU heightmap has
    /// it, so a small region is read back asynchronously; disabled while no request is pending.
    /// </summary>
    public partial class BaseTerrainSystem : GameSystemBase
    {
        private readonly List<Pending> _pending = new();
        private TerrainSystem _terrain;

        protected override void OnCreate()
        {
            base.OnCreate();
            _terrain = World.GetOrCreateSystemManaged<TerrainSystem>();
            Enabled = false;
        }

        /// <summary>
        /// Requests the original terrain heights at building-space xz points; <paramref name="done"/> gets them relative
        /// to the building's ground (y = 0), or null on failure. False if the terrain cannot be read.
        /// </summary>
        internal bool TryRequest(Entity building, IReadOnlyList<float2> points, Action<float[]> done)
        {
            if (points == null || points.Count == 0 || done == null)
                throw new ArgumentException("Points and callback required.");
            try
            {
                var heightmap = _terrain.heightmap;
                if (heightmap == null || !SystemInfo.supportsAsyncGPUReadback ||
                    !EntityManager.TryGetComponent(building, out Transform transform))
                    return false;

                var data = _terrain.GetHeightData();
                var world = new float3[points.Count];
                float2 min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < world.Length; i++)
                {
                    var local = new float3(points[i].x, 0f, points[i].y);
                    world[i] = transform.m_Position + math.rotate(transform.m_Rotation, local);
                    float2 texel = TerrainUtils.ToHeightmapSpace(ref data, world[i]).xz;
                    min = math.min(min, texel);
                    max = math.max(max, texel);
                }

                // One texel margin for bilinear sampling, clamped to the map.
                int2 size = new int2(heightmap.width, heightmap.height);
                int2 from = math.clamp((int2)math.floor(min) - 1, 0, size - 1);
                int2 to = math.clamp((int2)math.floor(max) + 2, 0, size - 1);
                int2 extent = to - from + 1;
                var request = AsyncGPUReadback.Request(heightmap, 0, from.x, extent.x, from.y, extent.y, 0, 1,
                    GraphicsFormat.R16_UNorm);
                _pending.Add(new Pending(request, building, transform.m_Position.y, world, from, extent, data, done));
                Enabled = true;
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Requesting the original terrain under building {building} failed.");
                return false;
            }
        }

        protected override void OnUpdate()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var pending = _pending[i];
                if (!pending.Request.done)
                    continue;
                _pending.RemoveAt(i);
                try
                {
                    pending.Done(Sample(pending));
                }
                catch (Exception e)
                {
                    Mod.Log.Error(e, $"Handling the original terrain under building {pending.Building} failed.");
                }
            }
            Enabled = _pending.Count > 0;
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            _pending.Clear(); // the buildings belong to the previous city
            Enabled = false;
        }

        private float[] Sample(Pending pending)
        {
            try
            {
                if (pending.Request.hasError)
                {
                    Mod.Log.Warn($"Reading the original terrain under building {pending.Building} failed.");
                    return null;
                }
                var texels = pending.Request.GetData<ushort>();
                var data = pending.Data;
                var heights = new float[pending.World.Length];
                for (int i = 0; i < heights.Length; i++)
                {
                    float2 xz = TerrainUtils.ToHeightmapSpace(ref data, pending.World[i]).xz - pending.From;
                    float original = TerrainUtils.ToWorldSpace(ref data, Bilinear(texels, pending.Extent, xz));
                    heights[i] = original - pending.GroundY;
                }
                Mod.Log.Info($"[Fit] Building {pending.Building}: original ground at the corners " +
                             string.Join("/", Array.ConvertAll(heights, h => h.ToString("+0.##;-0.##;0"))) + " m");
                return heights;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Sampling the original terrain under building {pending.Building} failed.");
                return null;
            }
        }

        // Same interpolation as TerrainUtils.SampleHeight, on the read-back region.
        private static float Bilinear(Unity.Collections.NativeArray<ushort> texels, int2 extent, float2 xz)
        {
            int4 corners = new int4((int2)math.floor(xz), (int2)math.floor(xz) + 1);
            corners = math.clamp(corners, 0, extent.xyxy - 1);
            int4 index = corners.yyww * extent.x + corners.xzxz;
            float4 h = new float4(texels[index.x], texels[index.y], texels[index.z], texels[index.w]);
            float2 t = math.saturate(xz - corners.xy);
            float2 row = math.lerp(h.xz, h.yw, t.x);
            return math.lerp(row.x, row.y, t.y);
        }

        private sealed class Pending
        {
            public Pending(AsyncGPUReadbackRequest request, Entity building, float groundY, float3[] world, int2 from,
                int2 extent, TerrainHeightData data, Action<float[]> done)
            {
                Request = request;
                Building = building;
                GroundY = groundY;
                World = world;
                From = from;
                Extent = extent;
                Data = data;
                Done = done;
            }

            public AsyncGPUReadbackRequest Request { get; }
            public Entity Building { get; }
            public float GroundY { get; }
            public float3[] World { get; }
            public int2 From { get; }
            public int2 Extent { get; }
            public TerrainHeightData Data { get; }
            public Action<float[]> Done { get; }
        }
    }
}
