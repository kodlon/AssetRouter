# Legacy Actions Sample

Two actions that were in the core package before v0.7.0 are now available as a separate sample:
`GenerateMeshColliderAction` and `RunMenuItemAction`.

## Why they were moved

`GenerateMeshColliderAction` targets 3D workflows. Asset Router's built-in action set focuses on 2D.
The action is provided as a reference implementation for teams that need it.

`RunMenuItemAction` called `EditorApplication.ExecuteMenuItem` with a configurable menu path.
`EmitUnityEventAction` covers the same use case with a more flexible Inspector-based approach.
`RunMenuItemAction` is kept as a sample for projects already using it.

## Installing the sample

1. Open **Window > Package Manager**.
2. Find **Asset Router** in the list.
3. Open the **Samples** section.
4. Click **Import** next to **Legacy Actions**.

The sample adds `Samples/Asset Router/Legacy Actions/` to your project with both action scripts
and a standalone assembly definition.

## GenerateMeshColliderAction

Sets `ModelImporter.addCollider = true` on FBX and other 3D model files and re-imports them.
Applies to any asset whose importer is a `ModelImporter`.

## RunMenuItemAction

Calls `EditorApplication.ExecuteMenuItem(menuItem)` after import. The menu item path is a
configurable string field. Has no `CanRunOn` guard beyond checking that the path is non-empty.

For new setups, prefer `EmitUnityEventAction`, which does not require knowing menu item paths
and works with any Inspector-wirable method.
