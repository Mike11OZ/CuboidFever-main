# Resource packs

A resource pack replaces the game's block textures without touching the game files.

## Installing a pack

Packs live in the `resourcepacks` folder inside the game's app-data folder:

- Windows: `%APPDATA%\CuboidFever\resourcepacks`
- Linux: `~/.config/CuboidFever/resourcepacks`

The folder is created the first time the game starts. In game, **Settings > Resource Packs > Open Pack Folder**
opens it. Drop a pack in (a folder or a `.zip`), then open the Resource Packs list (or reopen it) and the pack is
listed. Click a pack to use it (the one in use is outlined and tagged "Selected"); the change shows immediately, even in a running world. The choice is remembered
in `options.json` next to the `saves` folder.

## Making a pack

A pack is a folder, or a `.zip` with the same contents, that mirrors the game's own `assets` layout:

```
MyPack/
    assets/
        cuboidfever/
            textures/
                block/
                    stone.png
                    grass_top.png
                    grass_side.png
```

- The file names are the game's own block texture names (see `assets/cuboidfever/textures/block` in the game).
  The easiest start is to copy the textures you want to change and edit them.
- A pack only needs the textures it changes. Anything it leaves out keeps the default texture.
- Textures must be the same size as the game's, currently 16x16. A texture of another size is skipped (a warning
  is printed to the console) and the default one is used.
- In a `.zip`, `assets/` must be at the top of the zip, not inside an extra folder.
- The name shown in the list is the folder name, or the zip's file name without `.zip`.

### Pack picture

Put a `pack.png` at the top level of the pack (next to `assets/`, not inside it) and the Resource Packs list
shows it beside the pack's name, like a world's picture in the world list.

- Any shape works. It is fitted inside the list's 16:9 box without stretching, so a square picture gets empty
  space at the sides. 128x128 or 256x256 is plenty.
- It must be a PNG, at most 1024x1024 pixels and 1 MB. Anything else is ignored and the pack shows a dark
  placeholder, the same as a pack with no `pack.png`.

## Not covered yet

Only block textures (the texture atlas) can be replaced. The menu panorama, menu background and the sun and moon
textures are still always the game's own.
