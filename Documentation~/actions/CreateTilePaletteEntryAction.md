# CreateTilePaletteEntryAction

Creates a `UnityEngine.Tilemaps.Tile` asset from the first Sprite sub-asset of the imported texture.

**Applies to:** Sprite-type textures.

**Tier:** G — integration with a Unity sub-feature (Tilemap).

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Output Folder | string | Where to save the .asset tile. Falls back to the imported asset's folder when empty. | "" |
| Name Pattern | string | File name without extension. `{assetName}` is replaced with the imported file name (no extension). | `{assetName}_Tile` |
| Overwrite Existing | bool | When false, the action is skipped if a tile asset already exists at the output path. | false |
| Tile Color | Color | Tint color applied to the tile. White means no tint. | White |

## How it works

`CanRunOn` checks that `textureType == Sprite`. It does not check for Sprite sub-assets;
that check happens in `Execute`.

`Execute` calls `AssetDatabase.LoadAssetAtPath<Sprite>(ctx.AssetPath)` to get the first Sprite
sub-asset. If none is found (for example, the sprite was not yet generated because importer settings
are still applying), a warning is logged and the action skips.

When a Sprite is found, the action creates a `Tile` instance, assigns `tile.sprite` and `tile.color`,
creates the output folder if needed, and saves the tile with `AssetDatabase.CreateAsset`.

## Requirements

No extra packages. `UnityEngine.Tilemaps` is part of Unity 2017.2 and later, available via
the 2D Tilemap Editor package (included by default in 2D project templates).

## Idempotency

When `overwriteExisting` is false (the default), the action skips if the tile asset exists.
When `overwriteExisting` is true, the tile is recreated on every import.

## Edge cases

**No Sprite sub-asset:** `CanRunOn` passes (texture is Sprite type) but `Execute` logs a warning
and does nothing. This can happen when the texture importer is still applying settings and the
Sprite has not been generated yet. Asset Router runs actions after the import is complete, so this
is rare in practice but possible with certain importer preset sequences.

**Multiple sprites (sprite sheet):** `LoadAssetAtPath<Sprite>` returns the first Sprite in the
file. For a sprite sheet with many sub-sprites, only the first one becomes the tile. For multiple
tiles from one sheet, write a custom action that iterates `AssetDatabase.LoadAllAssetsAtPath`.

## Example

A 2D game uses a tileset where each tile is a separate PNG named `Tile_*`. A rule routes them to
`Assets/Art/Tiles/Sprites/` and runs `CreateTilePaletteEntryAction` with output folder
`Assets/Art/Tiles/`. On import of `Tile_Grass.png`, the action creates `Tile_Grass_Tile.asset`
ready to drag into a Tile Palette. Artists generate the whole tileset in one import batch.
