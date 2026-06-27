# GenerateSpritePhysicsShapeAction

Derives a bounding rectangle from the sprite's opaque pixels and applies it as the physics shape
via `Sprite.OverridePhysicsShape`.

**Applies to:** Sprite-type textures with Read/Write enabled.

**Tier:** F — content inference (reads pixel data to derive settings).

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Alpha Threshold | float (0..1) | Pixels with alpha below this value are treated as transparent. | 0.1 |

## Requirements

**Read/Write must be enabled on the texture.** Without it, `texture.GetPixels()` throws an
exception and the action fails. Enable Read/Write in the texture importer settings or in the
preset used by the rule.

The texture must be Sprite type. `CanRunOn` checks both `t.isReadable` and
`textureType == Sprite`.

## How it works

`Execute` reads all pixels with `texture.GetPixels()` and scans from each edge (left, right, top,
bottom) to find the first pixel with `alpha >= alphaThreshold`. These four positions form the
bounding box of the opaque region. The bounding box is converted to Unity world-space coordinates
using `pixelsPerUnit` and `sprite.pivot`, then applied as a rectangular polygon via
`Sprite.OverridePhysicsShape`.

## Shape output

The output is always a rectangle (4 vertices). It is not a tight outline around the sprite shape.
For a tight outline, use the Unity importer's own sprite shape generation or a custom action that
uses a marching-squares algorithm.

## Idempotency

The action calls `EditorUtility.SetDirty(sprite)` and `AssetDatabase.SaveAssets()` after applying
the shape. Running it twice produces the same result, though it triggers a save both times.

## Edge cases

**Fully transparent texture:** if all pixels are below the threshold, `FindLeft > FindRight` or
`FindBottom > FindTop`, and the action returns without applying any shape.

**Fully opaque texture:** the bounding box covers the full texture, and the physics shape equals
the sprite's full rectangle.

**Sprite without pivot set:** if `sprite.pivot` is (0, 0), the shape coordinates are relative to
the bottom-left corner of the texture.

## Example

A platformer uses pixel-art characters named `Char_*`. Read/Write is enabled in the `TextureImporter_Sprite`
preset. Adding `GenerateSpritePhysicsShapeAction` with threshold 0.05 automatically sets a tight
bounding-box physics shape on every character sprite on import. Artists never need to touch the
Unity importer.

## See also

[GenerateNineSliceBordersAction](GenerateNineSliceBordersAction.md) for a similar pixel-scan approach applied to UI sprites.
