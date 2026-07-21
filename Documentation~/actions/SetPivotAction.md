# SetPivotAction

Sets the sprite pivot on a texture asset and re-imports it.

**Applies to:** PNG, JPG, TGA, PSD, and any texture file imported as Sprite type.

**Tier:** A — modifying an importer field and triggering a re-import.

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Pivot | Vector2 | Pivot point in normalized coordinates. (0.5, 0.5) is center. (0, 0) is bottom-left. (1, 1) is top-right. | (0.5, 0.5) |

## How it works

`CanRunOn` checks that the asset's `TextureImporter` exists and that `textureType == Sprite`.
If the importer is not a texture or the texture type is not Sprite, the action is skipped.

`Execute` first checks the sprite import mode. On `SpriteImportMode.Multiple` it logs a warning
and returns — per-sprite pivots must be set in the Sprite Editor, not on the top-level importer.

Otherwise it reads the current `TextureImporterSettings` and returns early when
`spriteAlignment == Custom` and `spritePivot` already matches the configured value. When a change
is needed, it sets `spriteAlignment = Custom` (a pivot only takes visual effect under Custom
alignment — writing it without this leaves the default Center alignment in place and the pivot is
silently ignored), writes `spritePivot`, and calls
`AssetDatabase.ImportAsset(path, ForceUpdate)` to apply the changes.

## Idempotency

Yes. The action checks the current pivot before writing. Running it twice on the same asset with
the same configuration produces no second re-import.

## Edge cases

**Multiple sprite mode:** the action logs a warning and skips the asset. Sheets with multiple
sub-sprites need per-sprite pivots set in the Sprite Editor, and there is no equivalent of the
top-level `spritePivot` field for Multiple mode.

**Custom pivot outside (0, 0)..(1, 1):** Unity accepts values outside the normalised range, but the
behavior in physics and animation depends on your project setup. Prefer values inside the range
unless you specifically need an out-of-bounds pivot.

## Example

A rule for gameplay sprites matches `Sprite_*`, target `Assets/Art/Sprites/`, preset
`TextureImporter_Sprite` (Sprite mode = Single). Adding `SetPivotAction` with pivot (0.5, 0f)
ensures every sprite exports with a bottom-center pivot on import, so characters "stand" on the
tile below them without artists remembering to set it manually.
