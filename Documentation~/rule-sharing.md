# Rule Sharing

Copy a single import rule to the clipboard and paste it into any project — without exporting the entire database.

## Why

The full JSON export (`Export JSON` toolbar button) transfers your whole database. Rule sharing is for the opposite case: you have one well-tuned rule (e.g. a sprite pipeline with pivot action and nine-slice detection) and want to drop it into another project, or send it to a teammate.

## How to use

### Copy a rule

1. Open **Tools → Asset Router Settings**.
2. Select the rule you want to share in the **Import Rules** list.
3. In the rule detail panel, click **Copy Rule to Clipboard**.

The rule JSON is now in your system clipboard.

### Paste a rule

1. Open **Tools → Asset Router Settings** in the target project.
2. In the **Import Rules** list header, click **Paste from Clipboard**.

The rule is added to the bottom of the list and selected. Click **Save / Apply** to persist.

---

## What transfers

| Data | Transfers? | Notes |
|---|---|---|
| Rule name, pattern, mode, target folder, scope folder, enabled flag | ✅ Always | Plain values, fully portable |
| Post-import action **types** | ✅ If the type exists in the target project | Built-in actions always transfer; custom actions transfer if the same assembly is present |
| Post-import action **value fields** (`pivot`, `silenceThreshold`, `alphaThreshold`, `namePattern`, etc.) | ✅ Always | Serialized by value |
| Post-import action **object reference fields** (`catalog`, `templatePrefab`, `baseMaterial`, …) | ⚠ Best-effort | Resolved by GUID first, then by path. If neither resolves, the field is `null` — re-link manually in the Inspector. A warning is logged for each unresolved reference. |
| Preset | ⚠ Best-effort | Resolved by GUID first, then by path. If neither resolves, `preset` is `null` — re-link manually. A warning is logged. |

### Which actions are fully portable out of the box

| Action | Portable? |
|---|---|
| SetPivotAction | ✅ (`pivot` is a Vector2 value) |
| TrimAudioSilenceAction | ✅ (`silenceThreshold` is a float) |
| GenerateSpritePhysicsShapeAction | ✅ (`alphaThreshold` is a float) |
| GenerateNineSliceBordersAction | ✅ (`alphaThreshold` is a float) |
| CreateTilePaletteEntryAction | ✅ (`outputFolder`, `namePattern`, `overwriteExisting`) |
| CreatePrefabFromTemplateAction | ⚠ `templatePrefab` becomes null if not found by GUID/path |
| CreateScriptableObjectFromTemplateAction | ⚠ template SO reference becomes null if not found |
| CreateMaterialFromTextureAction | ⚠ `baseMaterial` becomes null if not found; `textureProperty` transfers |
| AppendToCatalogAction | ⚠ `catalog` becomes null if not found |
| EmitUnityEventAction | ⚠ persistent listeners become null (object refs) |
| RegisterAddressableAction | ✅ (`groupName` is a string) |

---

## JSON format

The clipboard payload is a JSON object identified by `"$assetRouterRule": 1`:

```json
{
  "$assetRouterRule": 1,
  "$type": "ImportRule",
  "ruleName": "Sprites",
  "isEnabled": true,
  "pattern": "T_*_D.png",
  "patternMode": "Glob",
  "matchAgainstFullPath": false,
  "scopeFolder": "",
  "targetFolder": "Assets/Art/Textures/",
  "preset": { "guid": "abc123...", "path": "Assets/Presets/TextureSprite.preset" },
  "postImportActions": [
    {
      "$type": "Kodlon.AssetRouter.Actions.SetPivotAction",
      "name": "Set Pivot Action",
      "fields": {
        "pivot": { "x": 0.5, "y": 0.0 }
      }
    }
  ]
}
```

Object references (preset, action fields that are `UnityEngine.Object`) are encoded as `{ "guid": "...", "path": "..." }`. On import, GUID is tried first; path is the fallback for cross-machine scenarios where GUIDs differ but relative paths match.

The `"$assetRouterRule": 1` marker distinguishes a rule payload from a full database JSON. Pasting a database JSON produces a clear error dialog.

---

## Limitations

- **Preset files are not bundled.** The preset must already exist in the target project at the same path, or its GUID must match. If neither resolves, re-link manually via the `Import Preset` field in rule details.
- **Object-reference action fields are not bundled.** Same GUID/path resolution applies. Re-link catalog, template prefab, base material, etc. after paste.
- **Custom action types** must be present in the target project. If the type is not found, the action is skipped and a warning is logged.
- **EmitUnityEventAction** persistent listeners always become null after paste — they reference scene/project objects by instance that cannot transfer.
