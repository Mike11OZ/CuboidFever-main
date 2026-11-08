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
3. **Lighting is derived data, so it is never saved, but it is not all rebuilt for free.** `Column.ComputeSkyLight` and
   `LightMap.Stitch` rebuild sky light. Lamp and torch light (channels 1-3) is only ever set when a block is placed
   (`LightMap.OnBlockChanged`), and generated terrain has no lamps, so a loaded column needs `ComputeBlockLight`
   (added in Phase 3) or every lamp a player placed would go dark after a reload.
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

Saving follows the Minecraft Java model: the save is kept continuously up to date, with no "save?" prompts.

- [x] `WorldSave`: one object per world on disk (folder, `level.json`, palette, column store, writer). `World` only calls `TryLoadColumn` and `SaveColumn`, which keeps the client/server split easy later
- [x] `World` takes an optional `WorldSave`. `GenerateJob` loads a saved column when one exists, otherwise generates as today
- [x] Loaded columns run `ComputeSkyLight` and the new `ComputeBlockLight`, then go through `Stitch` / `TryActivate` unchanged
- [x] `Column.NeedsSave` is separate from `Modified`; without a `WorldSave` the old "never unload modified columns" rule still applies
- [x] Edited columns are saved when they unload, every 30 seconds (two per frame), and on pause
- [x] Full flush on Exit World, Exit Game, Quit and window close (`Game1` hooks the `Exiting` event)
- [x] `ColumnWriteQueue`: background writer, newest-bytes-win, queued data stays readable, retries then a final retry on flush
- [x] Water and sand are re-woken in loaded columns (`FindBlocksToWake`, `BlockTickScheduler.Wake`) so flow resumes
- [x] Damaged column files are copied to `<world>/corrupt/` and regenerated instead of crashing or being overwritten
- [x] Interim menu wiring until Phase 5: New World makes "World N", Load World opens the most recently played
- [x] Time of day is saved and restored
- [ ] In-game check on the real game (see the checklist in the Phase 3 hand-off notes)

## Phase 4: Player and session state

- [x] Time of day (done in Phase 3)
- [x] Save position, look direction, flying flag, selected hotbar slot into `level.json` (on pause, Exit World and exit, and only once the player has spawned)
- [x] `SpawnSequence` uses the saved position when one exists, loads the world around it, and falls back to the ground at the same X/Z if the spot is now blocked
- [x] Loaded worlds take their seed from `level.json`. New worlds still default to the `WorldSeed` constant until Phase 5 adds a seed field
- [ ] Remember the player's position more often than pause/exit if a crash-proof position matters (a crash currently loses movement since the last pause)

## Phase 5: Game flow and UI

- [x] World list screen: Load World shows a scrolling list (mouse wheel and draggable scrollbar), most recently played first, one row per world with its thumbnail, name and last-played time (`WorldListScreen`)
- [x] Delete a world from the list: a Delete button on each row, then a "This cannot be undone" confirmation (`WorldDeletion` refuses anything that is not a world folder directly inside the saves folder)
- [x] On-screen message (fades after a few seconds) when a world cannot be opened, created or deleted. A failed create now stays on the New World screen instead of starting a world that would never be saved
- [x] New World screen: name box (blank = "World N") and seed box (blank = random, a number is used as is, any other text is turned into a repeatable number), Create World / Cancel; Tab switches boxes, Enter creates, Escape cancels
- [x] Pause menu "Save and Quit to Title" (the old Exit World, which already saved, renamed)
- [ ] Brief autosave indicator in the HUD
- [x] World thumbnail: `thumbnail.png` in the world folder, taken from the paused frame (before the HUD is drawn) and shown in the list. Closing the window mid-play only takes one if the world has none yet
- [ ] Make the thumbnail also refresh periodically during play (autosave), so it is current after a crash

## Phase 6: Hardening

- [x] Quarantine unreadable column files instead of crashing (done in Phase 3)
- [ ] Keep a `level.json.bak` in case `level.json` itself is damaged
- [x] Lock so two game instances cannot open the same world (`WorldLock`: an exclusive file handle on `session.lock`, released by the operating system even after a crash). Deleting a world that is open elsewhere is refused before anything is removed
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
