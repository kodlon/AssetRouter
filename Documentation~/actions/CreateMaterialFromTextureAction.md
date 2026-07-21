# CreateMaterialFromTextureAction

Creates a new Material from a base material, assigns the imported texture to a shader property,
and saves the result as a `.mat` file.

**Applies to:** Texture2D assets only. Non-texture assets are skipped by `CanRunOn`.

**Tier:** E — factory pattern (creates a derived asset from a source asset).

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Base Material | Material | Material to copy shader and all property values from. When null, resolves to the active render pipeline's default material — URP/HDRP `RenderPipelineAsset.defaultMaterial` or the Built-in RP `Default-Material`. | None |
| Texture Property | string | Shader property name to assign the texture to. Falls back to `Material.mainTexture` when the shader has no such property. | `_MainTex` |
| Output Folder | string | Absolute (`Assets/...`) — used as-is. Relative (`Materials`) — resolved as a subfolder of the imported texture's folder. Empty — the texture's folder directly. | `Materials` |
| Name Pattern | string | File name without extension. `{assetName}` is replaced with the texture file name (no extension). | `{assetName}_Mat` |
| Overwrite Existing | bool | When false, the action is skipped if a material already exists at the output path. | false |

## How it works

`CanRunOn` returns true when `importedAsset is Texture2D` and a base material can be resolved
(either the explicit `baseMaterial` or an SRP/Built-in default).

`Execute` creates `new Material(source)` where `source` is the resolved base material, which copies
the shader and all property values. It then assigns the texture to `textureProperty` if the shader
has that property, otherwise falls back to `Material.mainTexture`. Finally the material is saved
with `AssetDatabase.CreateAsset(mat, matPath)`.

## Shader property names

Common property names by shader:

| Shader | Property name |
|--------|--------------|
| Standard (Built-in) | `_MainTex` |
| URP Lit | `_BaseMap` |
| URP Unlit | `_BaseMap` |
| HDRP Lit | `_BaseColorMap` |
| Custom | Check the shader source or the Inspector |

If the property name does not exist on the shader, the action falls back to `Material.mainTexture`.
This respects the shader's `[MainTexture]` attribute — for example, URP Lit routes `mainTexture`
to `_BaseMap` — so the default `_MainTex` value works out of the box on Standard, URP Lit, and
any shader that annotates a main texture slot.

## Idempotency

When `overwriteExisting` is false (the default), the action is skipped if the material already
exists. With `overwriteExisting` true, the material is recreated on every import.

## Edge cases

**Wrong property name:** the action falls back to `Material.mainTexture` and the texture still lands
on the shader's main slot when one is annotated. If the shader has no `mainTexture` mapping either,
the material is created but the texture is not assigned — verify the property name in the shader or
in the material Inspector's debug mode.

**No base material and no pipeline default:** the action logs a warning and returns without creating
anything. This only happens in exotic setups where `GraphicsSettings.currentRenderPipeline` is unset
and `Default-Material.mat` is unavailable.

**Multiple textures:** this action assigns one texture to one property. To assign textures to
multiple properties, add multiple `CreateMaterialFromTextureAction` instances with different
property names, or write a custom action.

## Example

An environment team imports tiling textures named `Env_*`. A rule routes them to `Assets/Art/Environment/`
and runs `CreateMaterialFromTextureAction` with base material `EnvTilingBase` (URP Lit, tiling=4,
metallic=0) and property `_BaseMap`. Every new texture gets a ready-to-use material that inherits
all tiling and rendering settings from the base.
