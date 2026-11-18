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
   Done: drag release and one-shot commands bake at once; the delay for the rest is 0.2 s by default.
2. **Deform on worker threads on the raw vertex buffer.** Removes most of the main-thread hitch and the managed
   copies. Core's `IDeformation` would need a job-friendly form.
3. **Cache source meshes between bakes.** Repeated edits of one building obtain them every time.
4. **Defer the disk save until after the swap.** The asset must still exist before the game saves.
5. **Bone-style live preview while dragging, bake on release.** The biggest gain and the biggest change; it only
   fits plan-only edits cleanly.

## Measuring

For the runs below, every bake temporarily logged its phases (a phase timer on branch `feature/burst-bake`, removed
before the merge; only the `Bake for building …: <variant>, N ms, M new assets` line remains). The phases don't add
up to the total: naming, validation and cache lookups were untimed.

| Phase | What it covered |
|---|---|
| `obtain` | `RenderPrefab.ObtainMeshes` |
| `read` | mesh copy, positions, unpacking normals and tangents (with the vertex job: mesh and stream copies) |
| `deform` | `VertexDeformer.Deform` (with the vertex job: the wait for it, incl. unpacking and packing) |
| `pack` | packing normals and tangents (managed path only) |
| `write` | writing positions and packed streams back, bounds |
| `save` | `GeometryAsset.Save` in `GeometryAssetStore.Create` |
| Swap | `PrefabSwapper` and lot sync, logged separately |

Still not measured: the game's own work in the frames after the swap (rebuilding batches, loading the unloaded
geometry asset again, GPU upload).

### Results

Run of 14 Nov 2026, build `1.0.1+dbc8296` (Debug), a city with 755 variants. Times in ms, steady state (repeated
edits of the same building; they varied by less than 5 %).

| Building | Vertices | Total | read | deform | pack | write | save | Swap | deform + pack |
|---|---|---|---|---|---|---|---|---|---|
| `EU_ResidentialMediumRow01_L4_1x5` | 15 400 | 81 | 7.7 | 38 | 5.2 | 0.6 | 6 | 0.5 | 53 % |
| `Hospital01` | 118 277 | 445 | 56 | 289 | 39 | 4 | 25 | 1.4 | 74 % |
| `FR_Hospital` | 239 226 | 848 | 109 | 571 | 79 | 9 | 45 | 2–7 | 77 % |

All other phases stayed below 4 ms each (`obtain` 0, `addPrefab` 0.1, `attachments` at most 3.7).

- **The vertex maths is the freeze.** `deform` costs about 2.4 µs per vertex at every size, `pack` 0.33 µs. Together
  they are half of a small bake and three quarters of a large one. Item 2 is justified.
- **`read` is next** (0.47 µs per vertex, 10–13 %). It includes unpacking and the managed copies, which item 2 also
  takes off the main thread or removes.
- **The manifest write is a second, constant hitch:** 33–36 ms after every bake, whatever the building, because
  the whole manifest (755 entries) is rewritten. For a row house that is almost half the bake again.
- **Item 3 has nothing to gain:** `obtain` is 0 ms; the source meshes are already loaded.
- **Item 4 gains little:** `save` is 5–7 % of the bake.
- **The swap is negligible** (0.5–9 ms).
- **First bake of a session:** 406 ms for the row house, from one-off costs in `read` (174 ms) and `save` (157 ms).
  Not repeated for the next building.

Open: the numbers are from a Debug build, and the frames after the swap are still not measured.

### Results with the vertex job

Run of 14 Nov 2026, build `1.0.1+0c06c7b` (Debug), same city. Times in ms, steady state.

| Building | Vertices | Total before → now | read | deform | write | save |
|---|---|---|---|---|---|---|
| `EU_ResidentialMediumRow01_L4_1x5` | 15 400 | 81 → 33 | 0.5 | 0.8 | 0.3 | 6–9 |
| `Hospital01` | 118 277 | 445 → 65 | 3 | 2.6 | 1.5 | 25 |
| `FR_Hospital` | 239 226 | 848 → 90 | 6 | 3.7 | 3.5 | 43 |

- The vertex maths is gone as a cost (about 0.02 µs per vertex); `save` is now the largest bake phase.
- The manifest write (33–45 ms after every bake) now costs about as much as a bake, or more for small buildings.
- First bake of a session: 165 ms (was 406).

## Second-long freezes: Placemaker's catalog rescan

About every tenth edit the game froze for 1–2 s, before and after the vertex job. Unsquare's logs don't show it;
the cause is in `Placemaker.log` (same run, from log timing only, not yet confirmed with Placemaker disabled):

- Placemaker logs "+N prefab(s) from the game or other mods since the last scan; rescanning" and rebuilds its
  whole asset catalog. Each rescan took 1.8–2.1 s; all 20 in the session followed Unsquare bakes, none happened
  without them, and N matches the prefabs the bakes added (variant plus extension variants).
- No mod logged anything inside any rescan window (Unsquare, Simulation, Move It, Find It), so it blocks the main
  thread. Example: FR_Hospital bakes at 44.6, 45.8, 46.9, 49.2, 50.1 s; the one long gap is a rescan (46.9–48.8).
- It is throttled to about one rescan per 9.5 s, batching all prefabs added since. At one edit per second, roughly
  every tenth edit lands on one.
- Find It re-indexes after every bake too, but takes 1–2 ms.

Unsquare must register its variants as prefabs, so it can't avoid triggering the rescan. The fix belongs in
Placemaker: skip variant prefabs (names containing `__uq`), update its catalog incrementally, or scan off the main
thread. To confirm, deform 20–30 times with Placemaker disabled.

## Replacing the bake with bones: downsides

Analysis of 17 Nov 2026, from Placemaker's build 20 decompile, the game's `PreCullingSystem`,
`ProceduralSkeletonSystem`, `RaycastJobs` and bone types, and the local `ModsData/Unsquare` folder. No code was
changed; nothing below was profiled.

### How the bone path works in detail

- Per base model, grid size (1, 2, 3, 4 or 6) and cage span, every mesh and LOD is cut along a plan grid (two
  triangles per cell, plus one piece in front of and one behind the span) and given a bone-weight stream (20 bytes
  per vertex, one bone per vertex). The cut model is cached on disk. The grid follows the cage's twist, so a larger
  twist needs a new cut model, built over several frames.
- Each piece is one affine map in plan, written as two bones (parent: position, rotation, scale; child: rotation).
- The affine of the whole cage goes into the instance matrix, the residual per piece into the bones. Both are written
  by jobs only for objects that passed culling (`PreCullingFlags.NearCamera`), on change and every 64 frames.
- The shape lives on the variant prefab entity, which is an instantiated copy of a template with the base's
  `PrefabData`.

### Is the assumption right?

Half. The graphics card does slightly more work, not less: every visible bent building is skinned per vertex, every
frame, in every pass including shadows. The gain is in assets:

- Today every shape has its own geometry: the local folder holds 1,960 `.Geometry` files, 684 MB, for 1,120 manifest
  entries of only 150 bases (`FR_Hospital` alone has 95 variants of about 5 MB).
- With bones, geometry is stored per base and grid size; a shape is a few dozen bone poses. Disk, memory, load time
  and edit time then grow with the number of bases, not of shapes.

### Downsides

1. **Approximates Unsquare's cage.** Pieces are flat, so curved (twisted) walls become facets with creases at the grid
   lines; Placemaker accepts 4–15 cm error. It only works because its shapes are plan-only: heights, tapers and
   turned tops would need cuts in height too, and the 255-bone limit keeps a 3D grid at about 3×3×2 cells. Cutting
   adds vertices to every LOD. Today's bake is smooth with exact normals.
2. **Only the picture bends.** Props, lanes, areas and nets still need prefab data per shape, so a prefab entity per
   shape remains (only `AddPrefab` is skipped). `ObjectGeometryData` bounds stay unbent (search tree, overlaps);
   Placemaker patches culling bounds every 64 frames. Precise raycasts follow bones, but not the instance matrix.
3. **Saves and prefab identity, the largest cost.** A light variant carries the base's `PrefabData`, so saves store
   the base; Placemaker swaps back before saving and rebuilds after load. Unsquare would need a new persistence, and
   the bake pipeline must stay to load and migrate saves since 1.0.0. Everything that goes through `PrefabBase`
   lands on the base and drops the shape: upgrades, level-ups, eyedropper, Move It, info panel. Unsquare solved these
   for named variants (`UpgradeOwnership`, `LevelUpCarrySystem`, `VariantArchetype`); they would need solving again.
4. **Cost per building.** `Bone` (40 B) and `BoneHistory` (64 B) per bone, outside the chunk and not saved: a 6×6
   grid has 148 bones, about 15 KB per mesh, so over 100 MB for 10,000 buildings. GPU bone memory (64 B per bone,
   twice with motion vectors) is only allocated for visible objects. Placemaker never removes the buffers.
5. **Fallback still needed.** Meshes with their own bones (animated), not upright sub-meshes and meshes with bone
   weights are refused and need the bake, so two pipelines.
6. **Fragile.** It writes into undocumented render structures (culling flags, `NativeBatchInstances`, `SubMesh`
   flags) and rewrites them every 64 frames. A game update can break it silently; the bake uses asset and prefab APIs.
7. **Shading, unverified.** If the shader transforms normals with the bone matrix, pieces with non-uniform scale get
   slightly wrong lighting. The shader code was not checked.

### A partial replacement to evaluate

- Keep the named variant prefabs (names, saves, attachments, upgrades unchanged). Point their meshes at one shared
  bone-weighted mesh per base and keep the cage on the variant prefab, from which a job writes the bones.
- Removes the per-shape geometry; keeps `AddPrefab` per shape, and with it Placemaker's rescan. The manifest no
  longer lists assets per variant: a format change, which needs confirmation and a `FormatVersion` bump.
- Instead of grid cuts, the cage could be exact for positions with 8 bones (two per plan corner, the second carrying
  the height-linear shear) and 4 bilinear weights per vertex. Needs 4 bone influences in the game's shader
  (unverified); normals would then be approximate, since blended bones miss the weight gradients.
- For edit latency alone the bake still has cheaper fixes: after the vertex job, asset save and the full manifest
  rewrite (33–45 ms) are the largest costs.
