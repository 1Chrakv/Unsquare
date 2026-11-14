# Bake speed: Unsquare compared with Placemaker

Analysis of 13 Nov 2026, from reading Placemaker's build 20 and both mods' logs. No code was changed and nothing
below was profiled: the only measured numbers are the log lines quoted here.

**Result:** Placemaker shows a new shape almost at once; Unsquare takes about half a second per edit. The main
reason is that Placemaker's build 20 no longer bakes a mesh per shape. The rest is where and when the work runs.

## What Unsquare does per edit

| Step | Cost | Where |
|---|---|---|
| Wait for quiet after the last edit | 0.4 s (`PipelineOptions.RebakeDelaySeconds`) | `Debouncer` in `VariantSystem.OnUpdate` |
| Bake | 149, 85 and 84 ms in the log (row house `EU_ResidentialMediumRow01_L4_1x5`, 2 new assets each) | `VariantSystem.Bake`, main thread, one frame |
| Register and wait for prefab initialisation | at least one more frame | `PrefabSystem.AddPrefab`, `VariantSystem.IsInitialized` |
| Swap | not measured | `PrefabSwapper`, `LotInstanceSync` |

The bake stopwatch covers `TryGetOrCreateVariant` only. How the 85–149 ms split between the steps below is **not
measured**.

Per main mesh and per LOD (`RenderVariantBuilder`, `MeshDeformer`):

- The mesh is instantiated and its vertices go through managed arrays several times (`mesh.vertices`, `float3[]`,
  and back).
- The deformation is called through `IDeformation` per vertex, with a matrix inverse for each normal
  (`VertexDeformer`).
- Packed normals and tangents are read and written byte by byte; each write copies the whole vertex stream
  (`PackedVertexChannel`).
- The geometry asset is saved to disk synchronously and unloaded (`GeometryAssetStore.Create`).
- Source meshes are obtained again for every bake; nothing is kept between bakes.

After the meshes: attachment deformers, prefab clone, `AddPrefab`, then manifest save.

## What Placemaker does (build 20)

Two paths:

- **Bone path** (its "light" variants, most buildings since build 20):
  - Each base model is cut once along a grid and given bones; that model is cached on disk.
  - A new shape is only bone poses plus an instance matrix, written by jobs every frame.
  - The variant is an instantiated copy of a template prefab entity. There is no mesh work, no disk write, no
    prefab registration and no initialisation wait per shape.
- **Baked path** (fallback):
  - The bending runs in worker-thread jobs directly on the raw vertex buffer bytes.
  - The main thread only copies buffers and finishes under a budget of a few ms per frame.
  - Source meshes are kept in a size-limited cache; finished geometry is reused from a disk cache by name.
  - Build 16 logged 107 ms of main-thread finish in total for 7 variants (worst single step 30 ms), so roughly
    10–15 ms per variant.

Both paths:

- No wait after an edit.
- One variant is shared by all buildings with the same base and shape, and its planner produces few distinct shapes
  per row.
- The jobs are plain worker jobs; no Burst attribute was found.
- There are no build 20 timings: the bone path does not log per variant.

## Why the comparison is not like for like

- **Simpler deformation:** Placemaker shifts the four ground corners in plan only, no height change or twist.
  Unsquare's 3D cage would crease at the grid lines if done with bones.
- **Less per building:** no nets, upgrade extensions, terrain lift or remapping of existing paths and areas.
- **Nothing persisted:** it stores only the shape per building, swaps back to the base before saving and rebuilds
  after load. Unsquare's variants are named prefabs with assets that saves depend on.
- **Side effect of the bone path:** Placemaker never removes the skeleton and bone buffers it adds to buildings.

## What Unsquare could take from it

Ordered by expected gain per effort. Items 2–4 keep prefab names, asset names and the manifest unchanged.

1. **Shorten or drop the 0.4 s wait.** It is the largest part of the perceived delay, about four times the bake.
2. **Deform on worker threads on the raw vertex buffer.** Removes most of the main-thread hitch and the managed
   copies. Core's `IDeformation` would need a job-friendly form.
3. **Cache source meshes between bakes.** Repeated edits of one building obtain them every time.
4. **Defer the disk save until after the swap.** The asset must still exist before the game saves.
5. **Bone-style live preview while dragging, bake on release.** The biggest gain and the biggest change; it only
   fits plan-only edits cleanly.

## Open question

Measure first: split the bake stopwatch into mesh work, asset save, attachments and prefab registration. Without
that, items 2–4 cannot be ranked.
