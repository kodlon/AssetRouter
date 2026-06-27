# CreateMaterialFromTextureAction

Creates a new Material from a base material, assigns the imported texture to a shader property,
and saves the result as a `.mat` file.

**Applies to:** Texture2D assets only. Non-texture assets are skipped by `CanRunOn`.

**Tier:** E — factory pattern (creates a derived asset from a source asset).

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Base Material | Material | Material to copy shader and all property values from. Required. | None |
| Texture Property | string | Shader property name to assign the texture to. | `_MainTex` |
| Output Folder | string | Where to save the .mat file. Falls back to the rule's target folder when empty. | "" |
| Name Pattern | string | File name without extension. `{assetName}` is replaced with the texture file name (no extension). | `{assetName}_Mat` |
| Overwrite Existing | bool | When false, the action is skipped if a material already exists at the output path. | false |

## How it works

`CanRunOn` checks that `importedAsset is Texture2D` and `baseMaterial != null`.

`Execute` creates `new Material(baseMaterial)`, which copies the shader and all property values.
It then calls `mat.SetTexture(textureProperty, texture)` and saves the material with
`AssetDatabase.CreateAsset(mat, matPath)`.

## Shader property names

Common property names by shader:

| Shader | Property name |
|--------|--------------|
| Standard (Built-in) | `_MainTex` |
| URP Lit | `_BaseMap` |
| URP Unlit | `_BaseMap` |
| HDRP Lit | `_BaseColorMap` |
| Custom | Check the shader source or the Inspector |

If the property name does not exist in the shader, `SetTexture` silently does nothing. The material
is still created and saved, but without the texture assigned.

## Idempotency

When `overwriteExisting` is false (the default), the action is skipped if the material already
exists. With `overwriteExisting` true, the material is recreated on every import.

## Edge cases

**Wrong property name:** the material is created but the texture is not assigned. No error is logged.
Verify the property name in the shader or use the material Inspector's debug mode to see property names.

**Multiple textures:** this action assigns one texture to one property. To assign textures to
multiple properties, add multiple `CreateMaterialFromTextureAction` instances with different
property names, or write a custom action.

## Example

An environment team imports tiling textures named `Env_*`. A rule routes them to `Assets/Art/Environment/`
and runs `CreateMaterialFromTextureAction` with base material `EnvTilingBase` (URP Lit, tiling=4,
metallic=0) and property `_BaseMap`. Every new texture gets a ready-to-use material that inherits
all tiling and rendering settings from the base.
