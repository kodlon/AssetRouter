# CreatePrefabFromTemplateAction

Instantiates a template prefab, calls `IAssetRouterPrefabSetup.SetupAssetRouter` on all
implementing components, then saves the result as a new prefab asset.

**Applies to:** Any asset type, but the callback pattern is most useful when `importedAsset` is a
Texture2D, Mesh, or AudioClip that the prefab needs a reference to.

**Tier:** E — factory pattern with user-defined callback interface.

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Template Prefab | GameObject | Prefab to instantiate. Required; action is skipped when null. | None |
| Output Folder | string | Where to save the new prefab. Falls back to the imported asset's folder when empty. | "" |
| Name Pattern | string | File name without extension. `{assetName}` is replaced with the imported file name (no extension). | `{assetName}_Prefab` |
| Overwrite Existing | bool | When false, the action is skipped if a prefab already exists at the output path. | false |

## How it works

`CanRunOn` checks that `templatePrefab != null`.

`Execute` resolves the output path from `outputFolder` and `namePattern`. When `overwriteExisting`
is false and a prefab already exists at that path, the action returns without doing anything.

Otherwise it calls `PrefabUtility.InstantiatePrefab(templatePrefab)` to create an instance in
the scene, then calls `GetComponentInChildren<IAssetRouterPrefabSetup>()` and invokes
`SetupAssetRouter(importedAsset, ctx.AssetPath)` on the first matching component.
After the callback, it calls `PathUtility.EnsureFolderExists(folder)` to create the output folder
if needed, then `PrefabUtility.SaveAsPrefabAsset(instance, prefabPath)`.
The instance is destroyed in the `finally` block regardless of whether an exception occurred.

## The IAssetRouterPrefabSetup callback

`IAssetRouterPrefabSetup` is in the Runtime assembly (`Kodlon.AssetRouter`). Implement it on any
`MonoBehaviour` without taking an Editor assembly dependency:

```csharp
public class CharacterPrefabSetup : MonoBehaviour, IAssetRouterPrefabSetup
{
    [SerializeField] private SpriteRenderer _renderer;

    public void SetupAssetRouter(Object importedAsset, string assetPath)
    {
        if (importedAsset is Sprite sprite)
            _renderer.sprite = sprite;
    }
}
```

Only the first component found by `GetComponentInChildren` is called. If multiple components
implement the interface, add a coordinator component that delegates to the others.

## Idempotency

When `overwriteExisting` is false (the default), the action skips if the output prefab exists.
The result of `SetupAssetRouter` depends on the implementation, which you control.

## Edge cases

**Template has no IAssetRouterPrefabSetup component:** the action still runs. The prefab is
instantiated and saved without any setup callback. The output prefab is a plain copy of the template.

**Output folder does not exist:** `EnsureFolderExists` creates it. The folder is created before
`SaveAsPrefabAsset` is called, so the save does not fail.

**SaveAsPrefabAsset throws:** the instance is destroyed in the `finally` block. The exception
is caught by the action pipeline and logged; subsequent actions in the rule still run.

## Example

A rule matches `Char_*` textures. The template prefab has a `SpriteRenderer` and a
`CharacterPrefabSetup` component. On import of `Char_Hero.png`, the action creates
`Char_Hero_Prefab.prefab` in the output folder with the sprite already assigned.
Artists get a ready-to-use prefab without touching Unity after exporting from Photoshop.

## See also

[IAssetRouterPrefabSetup](../api/extension-points.md)
[CreateScriptableObjectFromTemplateAction](CreateScriptableObjectFromTemplateAction.md) for the same pattern applied to ScriptableObjects.
