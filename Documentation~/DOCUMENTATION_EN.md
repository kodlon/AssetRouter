# Asset Router

Asset Router is a Unity Editor plugin that routes imported assets to configured folders and applies
Unity Presets automatically. When you drop a file into your project, Asset Router checks each rule
in order, applies the first match, and moves the file to the target folder. If no rule matches,
it shows a dialog or leaves the file in place, depending on your settings.

**Namespace:** `Kodlon.AssetRouter`
**Unity:** 2022.3 LTS or later
**Package ID:** `com.kodlon.assetrouter`

New to Asset Router? Start with the step-by-step [Getting Started](getting-started.md) guide.
For a complete map of every window, tab, and button, see the [UI Reference](ui-reference.md).

---

## How assets move through the system

1. Unity fires `OnPreprocessAsset` for each imported file.
2. Asset Router checks `ShouldProcess`: the file extension must be in `monitoredExtensions` and the
   path must not be under any `ignoredFolders` entry. If either check fails, the file is skipped.
3. `FindMatchingRule` walks the rules list top-to-bottom and returns the first rule where the pattern
   matches and `isEnabled` is true. If `scopeFolder` is set on the rule, the file must also be inside
   that folder.
4. When a rule matches, the preset is applied to the importer.
5. Unity fires `OnPostprocessAllAssets` after all importers finish.
6. Asset Router resolves the target folder (expanding `{token}` values), creates it when missing,
   and calls `MoveAsset` to move the file there. All moves in one import batch run inside a single
   `StartAssetEditing / StopAssetEditing` block and are recorded to History.
7. The rule's `postImportActions` run after the import batch finishes, deferred via
   `EditorApplication.delayCall`, because Unity forbids creating assets from inside
   `OnPostprocessAllAssets`. Actions execute in list order; a failure in one does not stop the rest.
8. If no rule matches and `showPopupForUnknownFiles` is true, a dialog asks what to do.

`PipelineOutputGuard` prevents routing loops: an asset created by an action (e.g. a generated
prefab or material) is not routed again when it comes back through the postprocessor, and an asset
whose own action chain is still running is skipped when one of its actions triggers a re-import.

---

## Getting started

On the first Editor load, if no `ImporterSettingsDatabase` exists in your project, a **Welcome Window**
appears. Click **Create** to generate `Assets/AssetRouter/ImporterSettingsDatabase.asset` with six
default rules and open the Settings window automatically. Click **Not now** to skip — the window will
not reappear in the same Editor session. Open **Tools > Asset Router > Settings** to create a database
later at any time.

Drop a file into your project. If the file name matches one of the default patterns (e.g. `T_Rock.png`
matches `T_*`), Asset Router moves it to the corresponding folder and applies the preset.

To change a rule, click it in the rules list, edit the fields in the detail panel on the right, and
click **Save / Apply**.

---

## The Settings window

Open the window via **Tools > Asset Router > Settings**.

### Toolbar

- **Database picker** (top-left object field): select which `ImporterSettingsDatabase` to use.
- **Create New**: creates a new database at a location you choose.
- **Export JSON**: writes the current database to a JSON file for version control or sharing.
- **Import JSON**: reads a JSON file and replaces the current database contents.

### Four tabs

- **Settings**: rules list, rule detail panel, general settings.
- **Dry Run**: preview which files would move without actually moving them.
- **History**: list of past import sessions with per-session undo.
- **Validate**: list of project assets that match no rule.

---

## Rules

### Rules list

The left panel shows all rules. Drag to reorder. The order matters: first matching rule wins.

Rules marked with `(N✓)` have matched N import batches since stats were last cleared. Reset the
counters via **Tools > Asset Router > Clear Rule Statistics**. Rules marked with `⚠`
have a conflict with another rule (duplicate pattern or detected overlap).

### Rule fields

#### Identification

| Field | Description |
|-------|-------------|
| Rule Name | Display name in the list and in logs. |
| Enabled | When unchecked, the rule is skipped during matching. |

#### Pattern

| Field | Description |
|-------|-------------|
| Pattern Mode | Glob or Regex. See [Pattern syntax](#pattern-syntax) below. |
| Pattern | The pattern to match against. |
| Match Full Path | When checked, the pattern is matched against the full asset path instead of just the file name. Required for path-based patterns like `Assets/Raw/**`. |
| Scope Folder | When non-empty, the rule only applies to assets already inside this folder. |

#### Target

| Field | Description |
|-------|-------------|
| Target Folder | Destination folder. Assets matched by this rule are moved here on import. Must start with `Assets/`. |

#### Settings

| Field | Description |
|-------|-------------|
| Preset | Unity Preset applied in `OnPreprocessAsset`. The preset type must match the asset type (e.g. a TextureImporter preset for textures). |

#### Post-Import Actions

Ordered list of actions that run after the file is moved. Drag to reorder. Click `+` to add a new
action from the dropdown. Click the `x` button to remove one.

Each action is a ScriptableObject stored as a sub-asset inside the database. The same action
instance can be shared across multiple rules.

---

## Pattern syntax

Copy-paste recipes for common naming conventions, in both modes, are collected in the
[Pattern Cookbook](patterns.md).

### Glob mode

| Wildcard | Matches |
|----------|---------|
| `*` | Any characters except `/` |
| `?` | Exactly one character except `/` |
| `**` | Any characters including `/` (crosses folder boundaries) |

Examples:
- `T_*` matches `T_Rock.png`, `T_Wall_D.tga`, but not `T_Rock/sub.png`
- `T_*_D.png` matches `T_Rock_D.png` but not `T_Rock.png`
- `Assets/Raw/**` matches any file under `Assets/Raw/` at any depth (requires Match Full Path)

Matching is case-insensitive. The glob is compiled to a .NET `Regex` internally.

**Note:** `Assets/**` also matches direct children like `Assets/x.png` because `**` translates to `.*`.

### Regex mode

The pattern is a standard .NET regular expression. Case-insensitive. Matching has a 50 ms timeout
to guard against catastrophic backtracking. A pattern that causes a timeout is treated as non-matching.

Examples:
- `^UI_.*\.png$` matches `UI_Button.png`
- `(T_|Tex_).*` matches `T_Rock.png` and `Tex_Ground.png`

### Live preview

While editing a pattern, the detail panel shows up to 3 matching file names from your project.
For Regex mode, it shows a red error message when the pattern is invalid. The preview updates
300 ms after you stop typing.

---

## Path Templating

Path Templating lets a single rule route assets to different subdirectories based on captured
values from the pattern. Place `{1}`, `{2}`, or `{name}` tokens in the **Target Folder** field.
At import time, each token is replaced by the corresponding capture group value.

**Example:** pattern `T_Char_*_*` (Glob), target `Assets/Art/Characters/{1}/`

| Imported file | Resolved target |
|---------------|-----------------|
| `T_Char_Hero_D.png` | `Assets/Art/Characters/Hero/` |
| `T_Char_Enemy_N.png` | `Assets/Art/Characters/Enemy/` |

### Token types

| Token | Meaning |
|-------|---------|
| `{1}`, `{2}`, … | Positional capture. Glob `*` and `**` produce groups, numbered left-to-right. |
| `{name}` | Named capture. Regex mode only, via `(?<name>...)`. |
| `{{` | Literal `{`. |
| `}}` | Literal `}`. |

### Capture groups in Glob mode

`*` → group; `**` → group; `?` → no group.

Pattern `T_*_*`, file `T_Hero_Diffuse.png`: `{1}` = `Hero`, `{2}` = `Diffuse.png`.

### Missing tokens

A token whose group does not exist is kept literally in the output. Template `Assets/{3}/` with
only two groups produces `Assets/{3}/` unchanged.

### Backward compatibility

Target folders without `{` are unaffected. Existing rules work with zero overhead.

For the full token grammar, escape rules, and sanitization details, see
[api/path-templating.md](api/path-templating.md).

---

## Dry Run tab

Dry Run scans the project and shows which files would be moved without doing anything.

1. Click **Scan Project**. A progress bar appears while all monitored assets are checked.
2. The table shows: file name, current folder, target folder, and matched rule.
3. Check the rows you want to move, then click **Apply Selected**.
4. Use **Select All** and **None** to bulk-select.

**Force re-import**: when checked, assets already in the correct folder are also included and
re-imported with the preset and actions applied. Useful when rules changed after the file was
already in place.

**Force Re-import In-Place**: skips the table and re-applies the preset and actions to every
matched asset that is already in its correct folder. No files are moved.

---

## History tab and Undo

Every batch of moves is recorded with a timestamp and source (auto-import or dry run).
The History tab lists these sessions.

Click a session to see the individual moves in the right panel.

Click **Undo Selected Session** to move all files back to their original paths in reverse order.
The undo is best-effort: files that are no longer at the recorded path are skipped with a warning.
A summary dialog shows how many files were reverted and how many were skipped.

**Important:** undo reverses file moves only. Import settings applied by presets are not rolled back:
after undo, the files are back in their original folders but the importer settings remain as set
by the last preset application. Assets created by post-import actions (generated prefabs, materials,
ScriptableObjects, tiles) are not deleted either; remove them manually if they are no longer wanted.

Click **Clear History** to delete all recorded sessions.

---

## Validate tab

Scans all monitored project assets and lists those that match no rule. This is a read-only view:
no files are moved.

Use this to find assets that fall outside your naming convention.

Click **Copy to Clipboard** to export the list of non-matching paths as newline-separated text.

---

## JSON export and import

The database is a Unity YAML ScriptableObject by default. JSON export and import are an additional
workflow for teams that want human-readable diffs or need to share a database across projects.

To share a single rule instead of the whole database, use the clipboard workflow described in
[Rule Sharing](rule-sharing.md).

### Exporting

Click **Export JSON** in the toolbar. Choose a save location. The JSON file contains all rules,
settings, and extension lists. Presets are referenced by GUID. Post-import actions are referenced
by GUID and local file ID.

### Importing

Click **Import JSON** in the toolbar. Choose the file. The current database is replaced with the
contents of the JSON file. The original ScriptableObject asset is kept; only its contents change.

### File format

The export is one JSON object. Top-level fields mirror the database settings; each rule is an
object in the `rules` array:

```json
{
  "$schema": 1,
  "schemaVersion": 2,
  "enableAutoImport": true,
  "showPopupForUnknownFiles": true,
  "monitoredExtensions": [".png", ".wav"],
  "ignoredFolders": ["Assets/Plugins/"],
  "rules": [
    {
      "$type": "ImportRule",
      "ruleName": "UI Textures",
      "isEnabled": true,
      "pattern": "UI_*",
      "patternMode": "Glob",
      "matchAgainstFullPath": false,
      "scopeFolder": "",
      "targetFolder": "Assets/Art/UI/",
      "preset": "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
      "postImportActions": [
        { "guid": "0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a", "fileId": "-8163672490871529621" }
      ]
    }
  ]
}
```

Notes on the fields:

- `$schema` marks the file as a full database export. It is a different marker from the
  `$assetRouterRule` used by single-rule clipboard payloads; pasting one where the other is
  expected produces a clear error.
- `schemaVersion` is the rule schema version. Older files are migrated automatically on import.
- `patternMode` is the string `"Glob"` or `"Regex"`. `$type` names the rule class, so future
  rule types can round-trip.
- `preset` is the preset asset's GUID as a plain string, or `null`. The preset file itself is
  not embedded.
- `postImportActions` entries reference action sub-assets by `guid` and `fileId`. These are
  project-specific; see the portability note below.

### Portability note

Post-import action references use Unity local file IDs, which are project-specific. A database
JSON exported from project A and imported into project B will not resolve action sub-assets
correctly. Rule settings, patterns, and target folders transfer correctly; action bindings do not.
For cross-project transfer of a rule together with its action settings, use
[Rule Sharing](rule-sharing.md), which serializes action fields by value.

---

## General settings

These settings are stored per-database.

| Field | Default | Description |
|-------|---------|-------------|
| Enable Auto Import | On | When off, the postprocessor does not route files on import. Dry Run still works. |
| Show Popup For Unknown Files | On | When on, a dialog appears for imported files that match no rule. |

### Monitored extensions

The list of file extensions that Asset Router watches. Only files with a matching extension pass
through the rule-matching logic. All other files are ignored regardless of name.

Default extensions: `.fbx .obj .png .jpg .jpeg .tga .psd .tiff .exr .hdr .wav .mp3 .ogg .aif .aiff`

Each entry must include the dot.

### Ignored folders

Asset paths that Asset Router never processes. Any file under a listed folder is skipped before
rule matching.

Default ignored: `Assets/Editor/`, `Assets/Plugins/`, `Assets/StreamingAssets/`, `Assets/AssetRouter/`, `Packages/`

Each entry must start with `Assets/`.

---

## Default rules

The database created on first load contains six rules. Character Textures and Location Textures
use Path Templating (see [Path Templating](#path-templating)) and demonstrate routing to per-asset
subfolders. General Textures carries a `Create Material From Texture` post-import action as a
working example of the actions pipeline — dropping a `T_*.png` produces a `Materials/T_*_Mat.mat`
next to the texture automatically.

| Rule Name | Pattern | Mode | Target Folder | Preset | Post-Import Action |
|-----------|---------|------|---------------|--------|-------------------|
| UI Textures | `UI_*` | Glob | `Assets/Art/UI/` | TextureImporter_UI | — |
| Character Textures | `T_Char_*_*` | Glob | `Assets/Art/Characters/{1}/` | TextureImporter | — |
| Location Textures | `^T_Loc_(?<loc>\w+)_.*` | Regex | `Assets/Art/Locations/{loc}/` | TextureImporter | — |
| General Textures | `T_*` | Glob | `Assets/Art/Textures/` | TextureImporter | Create Material From Texture |
| Sound Effects | `SFX_*` | Glob | `Assets/Audio/SFX/` | AudioImporter | — |
| Music | `Mus_*` | Glob | `Assets/Audio/Music/` | AudioImporter_Music | — |

These are starting points. Rename, reorder, or delete them as needed.

---

## Bundled presets

The package ships with 10 presets in the `Presets/` folder. To create your own preset and assign
it to a rule, see [Presets](presets.md).

| Preset | For |
|--------|-----|
| `TextureImporter.preset` | General textures |
| `TextureImporter_UI.preset` | UI textures |
| `TextureImporter_Sprite.preset` | Sprites (Single mode, tight mesh) |
| `TextureImporter_NormalMap.preset` | Normal maps (linear, no sRGB) |
| `TextureImporter_Lightmap.preset` | Lightmaps (HDR, 4096 px max) |
| `AudioImporter.preset` | General audio |
| `AudioImporter_Music.preset` | Music (streaming) |
| `AudioImporter_Voice.preset` | Voice (mono, 22050 Hz, Vorbis) |
| `ModelImporter_Static.preset` | Static props (no rig, no animation) |
| `ModelImporter_Character.preset` | Characters (Humanoid rig, import animations) |

---

## Troubleshooting

**My file did not move.**
Open the **History** tab. If there is no entry for the file, its extension is not in the
Monitored Extensions list or its path is under an Ignored Folder. If the file also does not
appear in the **Validate** tab as an unmatched asset, the filter settings are excluding it —
check **Settings tab > File Filter Settings**. If it does appear in Validate, no rule pattern
matches its name; adjust the pattern and re-check the live preview in the rule detail panel.

**The rule does not match even though the pattern looks right.**
Check the live preview in the rule detail panel. If the preview shows 0 matches, the pattern is
probably too specific. If the asset is in a subfolder and your pattern includes a path separator,
enable Match Full Path on the rule.

**The preset was not applied.**
The preset type must match the asset type. A TextureImporter preset on an audio file does nothing.
Check the preset asset in the Inspector to confirm its type matches.

**Actions did not run.**
During live auto-import, actions run for every file the rule matched, including files already in
the target folder. In a Dry Run batch, files already in place run their preset and actions only
when **Force re-import** is enabled. Check the **History** tab — a moved entry means the action
chain was scheduled to run right after the import batch. Also check that the action's own
conditions hold (`CanRunOn`), e.g. `Set Pivot` skips textures that are not Sprite type.

**Undo did not restore a file.**
Undo moves files back to their recorded original paths. If a file was manually moved or deleted
after the import session, undo skips it. The summary dialog after undo lists exactly which files
could not be restored.

**The conflict warning appears on a rule.**
A warning (⚠) means either two rules have identical patterns, or the overlap heuristic detected
that a sample set of project assets matches both rules. The first rule in the list wins. Reorder
or adjust the patterns to remove the conflict. The heuristic can produce false positives; check
the Validate tab to confirm actual routing.

---

## Related guides

- [Getting Started](getting-started.md) — step-by-step first run in 10 minutes
- [UI Reference](ui-reference.md) — every window, tab, and button
- [Pattern Cookbook](patterns.md) — Glob and Regex recipes with common mistakes
- [Presets](presets.md) — creating your own import presets
- [Rule Sharing](rule-sharing.md) — copy a single rule between projects via clipboard
- Use cases: [solo developer](use-cases/solo-developer.md), [mobile team](use-cases/mobile-team.md), [legacy project cleanup](use-cases/legacy-cleanup.md)
