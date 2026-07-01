# CreateScriptableObjectFromTemplateAction

Clones a template ScriptableObject via `Instantiate`, calls `IAssetRouterDataSetup.SetupAssetRouter`
if the clone implements it, then saves the result as a new `.asset` file.

**Applies to:** Any asset type.

**Tier:** E — factory pattern with user-defined callback interface (same pattern as `CreatePrefabFromTemplateAction`, applied to ScriptableObjects).

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Template | ScriptableObject | SO to clone. All serialized fields are copied to the new instance. Required; action is skipped when null. | None |
| Output Folder | string | Where to save the new .asset. Falls back to the imported asset's folder when empty. | "" |
| Name Pattern | string | File name without extension. `{assetName}` is replaced with the imported file name (no extension). | `{assetName}_Data` |
| Overwrite Existing | bool | When false, the action is skipped if an asset already exists at the output path. | false |

## How it works

`CanRunOn` checks that `template != null`.

`Execute` resolves the output path. When `overwriteExisting` is false and an asset exists at that
path, the action returns. Otherwise it calls `Instantiate(template)` to create a clone with all
serialized field values copied from the template. If the clone also implements `IAssetRouterDataSetup`,
`SetupAssetRouter(importedAsset, ctx.AssetPath)` is called on it. Then `AssetDatabase.CreateAsset`
saves the clone to the output path.

## The IAssetRouterDataSetup callback

`IAssetRouterDataSetup` is in the Runtime assembly. Implement it on any ScriptableObject:

```csharp
[CreateAssetMenu(menuName = "Game/Item Data")]
public class ItemData : ScriptableObject, IAssetRouterDataSetup
{
    public Texture2D icon;
    public string itemName;

    public void SetupAssetRouter(Object importedAsset, string assetPath)
    {
        icon = importedAsset as Texture2D;
        itemName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
    }
}
```

## Idempotency

When `overwriteExisting` is false (the default), re-importing the source asset skips the action if
the output already exists. When `overwriteExisting` is true, the old asset is replaced on every import.

## Edge cases

**Template has default values:** the clone starts with the template's serialized field values,
then `SetupAssetRouter` overwrites specific fields. Fields not touched by the callback keep their
template defaults. This is useful for setting shared configuration once on the template.

**Nested SO references:** `Instantiate` performs a shallow copy. Serialized object references
inside the template point to the same objects in the clone, not to copies of them.

## Example

An item database uses one `ItemData` asset per item icon. A rule matches `Icon_*` textures.
The template `ItemData` has default values for rarity, stack size, and category. On import of
`Icon_Sword.png`, the action creates `Icon_Sword_Data.asset` with the icon field populated,
ready for the item database to reference.

## See also

[CreatePrefabFromTemplateAction](CreatePrefabFromTemplateAction.md) for the same pattern with prefabs.
[IAssetRouterDataSetup](../api/extension-points.md)
