# CuboidFever save system: roadmap

Status key: `[x]` done, `[ ]` not started. Work happens on `Test-Branch`.

## What the codebase told us

These findings shape the whole plan.

1. **Terrain is fully deterministic from the seed.** Terrain, caves and placed features (trees, boulders, flowers) are all
   derived from `(seed, column x, column z)`. So a save only needs the columns the player has *changed*, plus a small
   amount of world metadata. Saves stay tiny, and unedited terrain is simply regenerated.
2. **Block IDs are not stable.** `BlockRegistry` assigns IDs alphabetically by block name when it loads the JSON files,
   with air forced to 0. Adding or removing any block file shifts the IDs of everything after it, which would silently
   corrupt every existing save. Saves must store block *names* (a palette), never raw runtime IDs.
3. **Lighting is derived data.** `Column.ComputeSkyLight` and `LightMap.Stitch` rebuild it, so it is never saved.
4. **`Column.Modified` already exists** and is set in `World.SetBlock`, but today it means "never unload this column,
   there is no saving yet" (`World.UnloadFar`). Once columns can be written to disk, that rule goes away.
5. **The block tick queue (water flow, falling sand) lives only in memory** (`BlockTickScheduler`). After a load, reactive
   blocks near edits need to be re-woken or flowing water will freeze mid-flow.
6. **`Game1` already has the hooks.** `OnNewWorldRequested` and `OnLoadWorldRequested` are stubbed with TODOs, the world
   seed is a hard-coded constant (`WorldSeed = 706944`), and `ExitWorld`/`Exit` tear the world down without saving.
7. **Threading.** Column generation runs on `WorkerPool` threads (`World.GenerateJob`), which is also the right place to
   load a column from disk. Saving must snapshot block data on the main thread, then write on a worker.
8. **Build output is disposable.** `bin/` is wiped by rebuilds, so saves live in the user's app-data folder, not next to
   the executable (unlike screenshots today).

## Phase 0: Foundations (started)

- [x] `GamePaths.SavesRoot`: per-user saves folder (`%APPDATA%/CuboidFever/saves`, `~/.config/CuboidFever/saves` on Linux)
- [x] `AtomicFile`: crash-safe writes (temp file, flush, replace), used for every file the save system writes
- [x] `BlockPalette`: stable name-based block IDs per world; new blocks are appended, removed blocks keep their slot
- [x] `WorldInfo` (`level.json`): name, seed, format version, timestamps, time of day, player state, block palette
- [ ] Wire `GamePaths.SavesRoot` / `WorldInfo` into `Game1` (waits for Phase 3 so nothing half-works in between)

## Phase 1: Column serialization

- [x] Binary column format (`ColumnSerializer`): magic, format version, column coordinates, payload size, checksum, then the chunks
- [x] Reuse the chunk's own compact storage (uniform chunk, uniform layer, full 256-byte layer), written through the palette tables
- [x] Deflate the payload (`System.IO.Compression`, no new dependency)
- [x] `Chunk.WriteBlocks` / `Chunk.ReadBlocks` (its storage fields are private)
- [x] Round-trip tests (`CuboidFever.Tests`, run with `dotnet test CuboidFever.Tests`)
- [x] Reader rejects bad magic, wrong column, newer format versions, truncated and damaged data with `InvalidDataException`
- [ ] Palette round trip against the real `BlockRegistry` (needs a graphics device, so it waits for an in-game check in Phase 3)

## Phase 2: Storage layout

- [x] `IColumnStore` interface: `Read`, `Write`, `Delete`, `Exists`, `ListColumns` (bytes in, bytes out; knows nothing about blocks)
- [x] `FileColumnStore`: one file per column, `<world>/columns/c.<cx>.<cz>.cfc`, written atomically, readable while being replaced
- [x] Only edited columns are ever written, so file counts stay small. Region files can replace this later behind the interface
- [x] Tests for the store (`FileColumnStoreTests`), including negative coordinates and stray files

## Phase 3: World integration

- [ ] `World` takes an optional store. `GenerateJob` loads a saved column when one exists, otherwise generates as today
- [ ] Loaded columns still run `ComputeSkyLight`, and go through `Stitch` / `TryActivate` unchanged
- [ ] Separate "needs saving" from "was ever modified" (a `DirtySinceSave` flag next to `Modified`)
- [ ] Save dirty columns when they unload, then drop the "never unload modified columns" rule
- [ ] Autosave on a timer, plus a full flush in `Dispose` / `ExitWorld` / game exit (override `OnExiting`)
- [ ] Background writer queue so saving never stalls a frame
- [ ] Re-wake water and gravity blocks in loaded columns so flow resumes

## Phase 4: Player and session state

- [ ] Save position, look direction, flying flag, selected hotbar slot, time of day into `level.json`
- [ ] `SpawnSequence` uses the saved position when one exists instead of the fixed spawn point
- [ ] Remove the hard-coded `WorldSeed`; the seed comes from the world

## Phase 5: Game flow and UI

- [ ] World list screen, sorted by last played, with delete
- [ ] New World: name and seed (blank = random)
- [ ] Pause menu "Save and Quit to Title"
- [ ] Brief autosave indicator in the HUD
- [ ] World thumbnail (the panoramic screenshot code can produce one)

## Phase 6: Hardening

- [ ] Keep a `level.json.bak` and quarantine unreadable column files instead of crashing
- [ ] Lock file so two game instances cannot open the same world
- [ ] Format-version migration path
- [ ] Clear in-game message when a save mentions blocks this build does not have
- [ ] Add a test project (xUnit) covering palette, column round-trip and level.json

## Phase 7: Later

- Region files and compression tuning if column counts grow
- World import/export, per-world settings, world backups
- Entities and block data (chests, signs) once the game has them

## Known risks

- A block removed from `data/` and later saved back becomes air, because its saved slot has nowhere to go. The palette
  keeps the slot reserved so IDs never shift, and the loader reports which blocks were missing.
- The palette is capped at 256 entries (IDs are bytes). A world that has outlived many removed blocks could hit this.
- Changing terrain generation changes unedited terrain next to saved edits. A `generatorVersion` field in `level.json`
  is worth adding before the first public build.
