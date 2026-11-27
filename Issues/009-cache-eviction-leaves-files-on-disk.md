# 009 – Cache eviction leaves the geometry files on disk

| | |
|---|---|
| Status | Open, confirmed |
| Found by | In-game test CACHE-001, slow runs of 2026-11-27, then a direct probe |
| Severity | Medium: the geometry cache size option has no effect on disk; the folder only grows |
| Requirement | CACHE-001 (old checklist row 200) |

## Summary

Housekeeping at the first preload plans the eviction correctly and logs it as done, but no file leaves
`ModsData\Unsquare`. With the cache set to 0.5 GB the folder keeps all 1.1 GB.

## Steps

1. Close the game. Back up the geometry assets (`ModsData\Unsquare\UQ_*.Geometry*`): 3572 assets, 1120 MB.
2. Start the game, set **Geometry cache size** to 0.5 GB, restart the game.
3. Load `UnsquareTest-Fixture` (housekeeping runs at the first preload).
4. Count the `UQ_*.Geometry` files again.

## Expected

About 1638 asset files are deleted; about 0.5 GB of assets is left (CACHE-001).

## Actual

```
Housekeeping: 0 legacy variants unused for 14 days pruned (…); evicted 1638 assets (610 MB), cache 510 of 512 MB,
legacy-claimed 0 MB, 1638 stale bounds dropped.
```

On disk afterwards: still 3572 assets and 1120 MB; none of them is gone and none was rewritten. No warning in
`Unsquare.Mod.log` (`Cache eviction: … could not be deleted` is not logged) and nothing about the deletion in
`Player.log`.

## Analysis

- `GeometryAssetStore.Delete` finds the assets in `AssetDatabase.user` and calls `asset.Delete()`, which returns
  without an exception, so every name counts as deleted.
- In the game, `AssetData.Delete` goes to its database's `DeleteAsset(guid)`. That only acts when the database
  still holds the asset (`TryRemove`), and the data source only deletes the file when its file index has an entry for
  the guid. If either lookup misses, the call does nothing and reports nothing. The files are probably not indexed
  (or not loaded) in that database at preload time.
- Side effect: the evicted assets' bounds are dropped from `bounds.json`, so they cost a re-measure on their next
  use, and their last-use time falls back to the file time.
- Next steps: check after each `Delete()` that the file is gone (and count only those). If it is still there, delete
  the `.Geometry` and `.cid` files directly at preload, before any prefab uses them. Rerun CACHE-001.
