# AppendToCatalogAction

Adds the imported asset to an `AssetCatalog` ScriptableObject. Duplicate entries are skipped.

**Applies to:** Any asset type.

**Tier:** B — writing a reference to a cross-asset registry.

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Catalog | AssetCatalog | The catalog ScriptableObject to append to. | None (required) |

## How it works

`CanRunOn` checks that `catalog != null` and `importedAsset != null`.

`Execute` checks `catalog.entries.Contains(importedAsset)` and returns early if the asset is
already in the list. Otherwise it adds the asset, calls `EditorUtility.SetDirty(catalog)`,
and saves the catalog immediately with `AssetDatabase.SaveAssetIfDirty`.

## Idempotency

Yes. The action skips the asset when it is already in `entries`.

## Setup

Create a catalog via **Create > Asset Router > Asset Catalog**. Assign it to the `Catalog` field
on the action. The catalog asset can live anywhere in your project.

At runtime, load the catalog with `AssetDatabase.LoadAssetAtPath<AssetCatalog>` (Editor) or
include it as a serialized field on a MonoBehaviour (runtime).

## Edge cases

**Performance:** `List.Contains` runs in O(N). On catalogs with 10 000 or more entries, each import
adds a linear scan. For large catalogs, consider a custom action using a `HashSet` lookup.

**Multiple catalogs:** you can add multiple `AppendToCatalogAction` instances to the same rule,
each pointing to a different catalog, to register the asset in several places at once.

## Example

A "Sprite Catalog" rule matches `Icon_*`, routes sprites to `Assets/Art/Icons/`, and runs
`AppendToCatalogAction` pointing to `Assets/Data/IconCatalog.asset`. A UI manager at runtime
holds a reference to `IconCatalog` and iterates `entries` to populate a sprite picker.

## See also

[AssetCatalog](../api/extension-points.md)
