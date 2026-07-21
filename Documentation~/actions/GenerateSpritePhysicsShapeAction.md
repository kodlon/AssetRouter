# GenerateSpritePhysicsShapeAction

Derives a bounding rectangle from each sprite's opaque pixels and persists it as the physics shape
via `ISpritePhysicsOutlineDataProvider`. The shape survives reimport and travels with the asset.

**Applies to:** Sprite-type textures with Read/Write enabled. Supports both Single and Multiple
sprite import modes — Multiple-mode sheets get one shape per sprite rect.

**Tier:** F — content inference (reads pixel data to derive settings).

## Requirements

- The **2D Sprite** package (`com.unity.2d.sprite`) must be installed. The action is compiled
  behind `#if UNITY_2D_SPRITE`; without the package `CanRunOn` returns false and `Execute` only
  logs a warning.
- **Read/Write must be enabled on the texture.** Without it, `texture.GetPixels()` throws.
  Enable it in the texture importer settings or in the preset used by the rule.

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Alpha Threshold | float (0..1) | Pixels with alpha below this value are treated as transparent. | 0.1 |

## How it works

For each sprite rect returned by the sprite's data provider, `Execute` scans the pixel data from
each edge (left, right, top, bottom) to find the first pixel with `alpha >= alphaThreshold`.
These four positions form a bounding box in texture-pixel space.

The box is written as a 4-vertex polygon through `ISpritePhysicsOutlineDataProvider.SetOutlines`,
in coordinates **relative to the center of the sprite rect and in texture pixels** — not divided
by pixels-per-unit and not relative to the sprite pivot. This matches Unity's own Sprite Editor
"Custom Physics Shape" data and persists into the `.meta` file.

Once at least one outline changes, the action calls `dataProvider.Apply()` and
`importer.SaveAndReimport()`.

## Shape output

The output is always a rectangle (4 vertices). It is not a tight outline around the sprite shape.
For a tight outline, use the Unity importer's own outline generation or a custom action based on
a marching-squares algorithm.

## Idempotency

Before writing, the action compares the computed outline against the existing one for the same
sprite. If they match, the sprite is skipped, and `SaveAndReimport` is not called. Running the
action twice in a row on the same asset produces no second reimport.

Without this guard the `SaveAndReimport` call would cause the postprocessor to fire again, the rule
to match again, and the action to run again — an infinite reimport loop.

## Edge cases

**Fully transparent texture:** `FindLeft > FindRight` or `FindBottom > FindTop`; the sprite is
skipped without applying any shape.

**Fully opaque texture:** the bounding box covers the full sprite rect, and the physics shape equals
the sprite's full rectangle.

**Sprite sheet with Multiple mode:** each sprite rect gets its own bounding box computed inside
its own subregion of the texture, so a spritesheet does not end up with a single box that spans
the whole atlas.

**2D Sprite package not installed:** `CanRunOn` returns false, so a rule that references this
action simply skips it. Calling `Execute` directly (e.g. from a custom pipeline) logs a warning
and returns without changes.

## Example

A platformer uses pixel-art characters named `Char_*`. Read/Write is enabled in the
`TextureImporter_Sprite` preset. Adding `GenerateSpritePhysicsShapeAction` with threshold 0.05
automatically sets a per-sprite bounding-box physics shape on every character sprite on import,
including Multiple-mode atlases. Artists never need to touch the Sprite Editor.

## See also

[GenerateNineSliceBordersAction](GenerateNineSliceBordersAction.md) for a similar pixel-scan approach applied to UI sprites.
