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

`Execute` reads the current `TextureImporterSettings`, compares `spritePivot` to the configured
value, and returns early if they already match. When a change is needed, it writes the new settings
back and calls `AssetDatabase.ImportAsset(path, ForceUpdate)` to apply them.

## Idempotency

Yes. The action checks the current pivot before writing. Running it twice on the same asset with
the same configuration produces no second re-import.

## Edge cases

The action only checks that `textureType == Sprite`. If the texture is Sprite type but sprite mode
is Multiple and sub-sprites have individual pivots, those sub-sprite pivots are not affected.
This action sets only the top-level `spritePivot` field.

If you set a pivot outside the (0, 0) to (1, 1) range, Unity accepts it (custom pivot modes allow
this) but the behavior in physics and animation depends on your project setup.

## Example

A rule named "UI Textures" matches `UI_*`, target folder `Assets/Art/UI/`, preset `TextureImporter_UI`.
Adding `SetPivotAction` with pivot (0.5, 0.5) ensures every UI sprite has center pivot on import,
even when artists export them with a different default.
