# GenerateNineSliceBordersAction

Scans the sprite texture for transparent border regions and writes the result to
`TextureImporter.spriteBorder`. Triggers a re-import after the border is set.

**Applies to:** Sprite-type textures with Read/Write enabled.

**Tier:** F — content inference (reads pixel data to derive settings).

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Alpha Threshold | float (0..1) | Pixels with alpha below this value are treated as transparent. The action scans from each edge inward until it finds a pixel at or above this threshold. | 0.1 |

## Requirements

**Read/Write must be enabled on the texture.** Enable it in the texture importer settings or in
the preset used by the rule.

The texture must be Sprite type. `CanRunOn` checks both `t.isReadable` and `textureType == Sprite`.

## How it works

`Execute` reads all pixels with `texture.GetPixels()` and scans from each of the four edges
inward until it finds a pixel with `alpha >= alphaThreshold`. The distances from each edge to
the first non-transparent pixel become the four border values:
`Vector4(left, bottom, rightFromRight, topFromTop)`.

The result is written to `TextureImporter.spriteBorder`. If the computed border equals the current
value, the re-import is skipped (idempotent). Otherwise, `AssetDatabase.ImportAsset(path, ForceUpdate)`
triggers a re-import with the new border.

## Idempotency

Yes. The action compares the computed border to the current `importer.spriteBorder` and skips the
re-import when they are already equal.

## Edge cases

**Fully opaque texture (no transparent edges):** all four border values are 0. The sprite is saved
with no 9-slice borders. Unity's 9-slice rendering still works (the center region fills the full
sprite), it just has no fixed borders.

**Asymmetric transparency:** the action scans each edge independently. A texture with transparent
on the left and opaque on the right produces a non-zero left border and zero right border.

**Small sprites:** on a 16x16 sprite with 1 px transparent border on all sides, the result is
`Vector4(1, 1, 1, 1)`, which leaves a 14x14 center region. Works correctly.

## Example

A UI system uses panel sprites named `Panel_*`. Panels have 4 px of transparent padding on all
sides for anti-aliasing. Adding `GenerateNineSliceBordersAction` with threshold 0.05 automatically
sets `spriteBorder = (4, 4, 4, 4)` on every panel sprite on import. UI artists export and are done.

## See also

[GenerateSpritePhysicsShapeAction](GenerateSpritePhysicsShapeAction.md) for a similar pixel-scan approach applied to physics shapes.
