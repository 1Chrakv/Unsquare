# Unsquare – Claude guidelines

Cities: Skylines II mod that lets players freely deform buildings by moving the corners, edges and faces of
their bounding cuboid (trilinear cage deformation). Deformed geometry is baked into variant prefabs; see
`Unsquare/HowTo.md` for the pipeline.

## Layout

| Path | Contents | Depends on |
|---|---|---|
| `Unsquare/Core/` | Pure logic: cage model, deformation math, naming, debouncing, persistence, prune planning | `Unity.Mathematics`, `Newtonsoft.Json` only |
| `Unsquare/Integration/` | CS2 integration: prefab cloning, mesh baking, assets, ECS systems | Core + game assemblies |
| `Unsquare/Api/` | UI-facing service (`IBuildingDeformService`) and its data types | Core + Integration |
| `Unsquare/UI/` | The mod's UI: `CageEditSession` (edit model), hotkeys, cage overlay, options page | Core + Api + game input/render/tool systems |
| `Unsquare.Tests/` | NUnit tests; compiles `Core/**` by source link, no game DLLs at runtime | Core |

UI code gets the service via `UnsquareApi.GetDeformService(World)`; cage data is in building space (`BuildingSpace`).
Inputs (hotkeys, later mouse tool and panels) only call `CageEditSession` commands; views only read the session.

Dependencies only point downwards (UI → Api → Integration → Core); `UI/` never references `Integration/`.
Never reference `Game`, `Colossal.*`, `UnityEngine` or `Unity.Entities` from `Core/`; the test project would stop
building. In-game testing: `Unsquare/Testing.md`.

## Commands

```bash
dotnet build Unsquare.sln
dotnet test Unsquare.Tests
```

The mod project imports `Mod.props`/`Mod.targets` from `%CSII_TOOLPATH%` (net48, C# 9). Game DLLs are resolved
from `%CSII_MANAGEDPATH%`. Builds deploy to `Mods\Unsquare`; with the game running the deploy fails (locked DLLs).

## Versioning

Semantic version in `<Version>` of `Unsquare/Unsquare.csproj`; bump `ModVersion` in
`Unsquare/Properties/PublishConfiguration.xml` together (the build fails on a mismatch). `BuildInfo` logs version,
commit, configuration and build time on load.

## C# conventions (C# 9, .NET Framework 4.8)

- File-scoped namespaces are not available (C# 10); use block namespaces.
- `records` and `init` need `IsExternalInit`; prefer `readonly struct` / sealed classes with constructors.
- Private fields `_camelCase`, everything else PascalCase; constants PascalCase.
- Prefer immutable value types for data passed between layers; validate at the boundary, trust inside.
- `var` when the type is obvious; target-typed `new()`, switch expressions and pattern matching are welcome.
- Make classes `sealed` and members `private`/`internal` unless they are part of the Api.
- No static mutable state except the mod logger; pass collaborators via constructors.
- Throw `ArgumentException` for programmer errors; return `bool Try…(out …)` for expected failures.
- Small, single-purpose types; one public type per file, file name = type name.

## Unity / CS2 / ECS rules

- Every `SystemBase`/`GameSystemBase` subclass must be `partial` (Entities source generator).
- Systems that only work on demand set `Enabled = false` when idle.
- `Unity.Mathematics` (`float3`, `quaternion`, `math.*`) in Core; convert to `Vector3` only at the Mesh boundary.
- `UnityEngine.Object` has overloaded `==`: never use `?.`, `??` or `is null` on Unity objects.
- Never edit shared meshes; `Object.Instantiate` first and `Object.Destroy` every temporary mesh.
- Mesh and prefab APIs are main-thread only; keep hot loops allocation-free (no LINQ, no closures).
- Wrap each game-API step (clone, AddPrefab, asset I/O, reflection) in try/catch, log, and degrade by not swapping.
- Use `Mod.Log` (`Colossal.Logging.ILog`) for logging, never `Debug.Log`.
- Prefab names, deform keys, asset names and GUID seeds are **permanent**: change them only with a version bump
  and a migration, otherwise existing saves break.

## Comments, tests, commits

- Comments: max 2 lines inside code; XML doc summaries max 3 lines. Explain *why*, not *what*.
- Add NUnit tests for all Core logic; name them `Method_Condition_Expectation`.
- Commit messages: one header line, one blank line, 1–3 summary lines. Keep commits well scoped.

## Other mods

The FlexIt mod inspired this one. Never copy code from other mods or the game, and never commit decompiled code.
