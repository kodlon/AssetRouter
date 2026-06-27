# RegisterAddressableAction

Registers the imported asset in an Addressables group.

**Applies to:** Any asset type.

**Tier:** C — optional external package dependency behind a compile-time define.

## Requirements

`com.unity.addressables` version 1.19.0 or later must be installed. Without it, this action class
does not exist in the assembly. The `UNITY_ADDRESSABLES` define symbol is set automatically via
the `versionDefines` entry in `AssetRouter.Editor.asmdef` when the package is detected.

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Group Name | string | Name of the Addressables group to add the asset to. When empty or when no group with this name exists, the Default Group is used. | "" (uses Default Group) |

## How it works

`CanRunOn` returns false when `AddressableAssetSettingsDefaultObject.Settings` is null, which
happens when Addressables has not been initialized in the project.

`Execute` resolves the asset GUID via `AssetDatabase.AssetPathToGUID`, finds the group by name
(falling back to the Default Group), and calls `settings.CreateOrMoveEntry(guid, group)`.
Only `EditorUtility.SetDirty(settings)` is called afterward; the settings are not saved immediately.
Unity saves dirty assets at the end of the import batch, which avoids the performance cost of
saving after every individual asset.

## Idempotency

Yes. `CreateOrMoveEntry` is idempotent: if the asset is already in the group, it is moved to the
same group (a no-op). If it is in a different group, it is moved to the configured group.

## Edge cases

**No Default Group:** if `groupName` is empty and the project has no Default Group configured,
the action logs a warning and skips the asset.

**Save timing:** because the save is deferred, a Unity crash immediately after import could leave
the Addressables settings dirty but unsaved. The next Editor reload picks up the dirty state and
saves normally.

**Cross-machine JSON:** Addressables group assignments are stored in `AddressableAssetSettings`,
not in the Asset Router database. JSON export/import does not carry over group assignments.

## Example

A "UI Sprites" rule matches `UI_*`, routes to `Assets/Art/UI/`, and runs `RegisterAddressableAction`
with group name `"UI"`. Every UI sprite imported with the correct prefix lands in the UI Addressables
group automatically, ready for runtime loading via `Addressables.LoadAssetAsync`.
