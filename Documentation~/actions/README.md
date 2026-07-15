# Built-in Actions

Asset Router ships with 11 built-in actions covering a range of automation patterns.
Each action is a ScriptableObject stored as a sub-asset inside your settings database.

## Action index

| Action | Applies to | What it does | Tier |
|--------|-----------|--------------|------|
| [SetPivotAction](SetPivotAction.md) | Sprite textures | Sets sprite pivot and re-imports | A |
| [TrimAudioSilenceAction](TrimAudioSilenceAction.md) | WAV files | Trims leading and trailing silence from 16-bit PCM WAV | A |
| [AppendToCatalogAction](AppendToCatalogAction.md) | Any asset | Adds asset reference to an AssetCatalog ScriptableObject | B |
| [RegisterAddressableAction](RegisterAddressableAction.md) | Any asset | Registers asset in an Addressables group | C |
| [EmitUnityEventAction](EmitUnityEventAction.md) | Any asset | Fires a serialized UnityEvent configured in the Inspector | D |
| [CreatePrefabFromTemplateAction](CreatePrefabFromTemplateAction.md) | Any asset | Instantiates a template prefab, calls setup callback, saves as new prefab | E |
| [CreateScriptableObjectFromTemplateAction](CreateScriptableObjectFromTemplateAction.md) | Any asset | Clones a template SO, calls setup callback, saves as new .asset | E |
| [CreateMaterialFromTextureAction](CreateMaterialFromTextureAction.md) | Texture2D | Creates a material from a base material and assigns the texture | E |
| [GenerateSpritePhysicsShapeAction](GenerateSpritePhysicsShapeAction.md) | Sprite textures | Derives physics outline from pixel alpha and applies it | F |
| [GenerateNineSliceBordersAction](GenerateNineSliceBordersAction.md) | Sprite textures | Detects transparent borders and sets spriteBorder | F |
| [CreateTilePaletteEntryAction](CreateTilePaletteEntryAction.md) | Sprite textures | Creates a Tile asset for use in Tilemap | G |

Two additional actions are available in the **Legacy Actions** sample:
`GenerateMeshColliderAction` and `RunMenuItemAction`. See [Legacy Samples](LegacySamples.md).

## Architectural tiers

The tiers show what each action demonstrates architecturally, not a quality ranking.

| Tier | Pattern |
|------|---------|
| A | Modify the importer or file directly, then re-import |
| B | Write a reference to a cross-asset registry |
| C | Optional external package dependency via compile-time define |
| D | No-code Inspector hook using UnityEvent |
| E | Factory pattern: create a derived asset and call a user-defined callback |
| F | Content inference: read pixel or audio data, derive settings |
| G | Integration with a Unity sub-feature (Tilemap, Addressables, etc.) |

When writing your own action, find the tier closest to your use case and use the matching
built-in as a starting point. See [Writing Your Own Action](../api/extension-points.md).
