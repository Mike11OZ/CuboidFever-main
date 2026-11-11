# Resource packs

A resource pack replaces the game's block textures without touching the game files.

## Installing a pack

Packs live in the `resourcepacks` folder inside the game's app-data folder:

- Windows: `%APPDATA%\CuboidFever\resourcepacks`
- Linux: `~/.config/CuboidFever/resourcepacks`

The folder is created the first time the game starts. In game, **Settings > Resource Packs > Open Pack Folder**
opens it. Drop a pack in (a folder or a `.zip`), then open the Resource Packs page (or reopen it) and the pack is
listed. Click a pack to use it; the change shows immediately, even in a running world. The choice is remembered
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

## Not covered yet

Only block textures (the texture atlas) can be replaced. The menu panorama, menu background and the sun and moon
textures are still always the game's own.
