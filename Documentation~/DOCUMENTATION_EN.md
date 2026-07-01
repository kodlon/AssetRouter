# Asset Router

Asset Router is a Unity Editor plugin that routes imported assets to configured folders and applies
Unity Presets automatically. When you drop a file into your project, Asset Router checks each rule
in order, applies the first match, and moves the file to the target folder. If no rule matches,
it shows a dialog or leaves the file in place, depending on your settings.

**Namespace:** `Kodlon.AssetRouter`
**Unity:** 2022.3 LTS or later
**Package ID:** `com.kodlon.assetrouter`

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
6. Asset Router calls `MoveAsset` to move the file to `targetFolder`, then runs each action in
   `postImportActions` in order.
7. If no rule matches and `showPopupForUnknownFiles` is true, a dialog asks what to do.

The `AssetsBeingMoved` guard prevents the re-import that Unity triggers after `MoveAsset` from
running the postprocessor a second time on the same file.

---

## Getting started

On the first Editor load, Asset Router creates `Assets/AssetRouter/ImporterSettingsDatabase.asset`
with six default rules. Open **Tools > Asset Router Settings** to see them.

Drop a file into your project. If the file name matches one of the default patterns (e.g. `T_Rock.png`
matches `T_*`), Asset Router moves it to the corresponding folder and applies the preset.

To change a rule, click it in the rules list, edit the fields in the detail panel on the right, and
click **Save / Apply**.

---

## The Settings window

Open the window via **Tools > Asset Router Settings**.

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

Rules marked with `(N)` have matched N times since stats were last cleared. Rules marked with `⚠`
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
4. Use **Select All** and **Select None** to bulk-select.

**Force re-import**: when checked, assets already in the correct folder are also included and
re-imported with the preset and actions applied. Useful when rules changed after the file was
already in place.

**Re-import All Matched**: skips the table and immediately moves all matching assets.

---

## History tab and Undo

Every batch of moves is recorded with a timestamp and source (auto-import or dry run).
The History tab lists these sessions.

Click a session to see the individual moves in the right panel.

Click **Undo Selected Session** to move all files back to their original paths in reverse order.
The undo is best-effort: files that are no longer at the recorded path are skipped with a warning.
A summary dialog shows how many files were reverted and how many were skipped.

**Important:** undo reverses file moves only. Import settings applied by presets are not rolled back.
After undo, the files are back in their original folders but the importer settings remain as set
by the last preset application.

Click **Clear History** to delete all recorded sessions.

---

## Validate tab

Scans all monitored project assets and lists those that match no rule. This is a read-only view:
no files are moved.

Use this to find assets that fall outside your naming convention.

Click **Copy to Clipboard** to export the list of non-matching paths as newline-separated text.

---

## Diagnostic Window

Open via **Tools > Asset Router > Diagnostic Window**.

Shows every monitored asset processed by the postprocessor in real time, with columns for
timestamp, file name, matched rule, and outcome (no match / already in place / moved / queued).

The window starts recording when it is open and stops when closed, so it adds no overhead when
not in use. The buffer holds the last 500 entries and is cleared on assembly reload.

Use this when a file is not moving and you cannot tell why. The window shows exactly what the
postprocessor saw and decided.

---

## JSON export and import

The database is a Unity YAML ScriptableObject by default. JSON export and import are an additional
workflow for teams that want human-readable diffs or need to share a database across projects.

### Exporting

Click **Export JSON** in the toolbar. Choose a save location. The JSON file contains all rules,
settings, and extension lists. Presets are referenced by GUID. Post-import actions are referenced
by GUID and local file ID.

### Importing

Click **Import JSON** in the toolbar. Choose the file. The current database is replaced with the
contents of the JSON file. The original ScriptableObject asset is kept; only its contents change.

### Portability note

Post-import action references use Unity local file IDs, which are project-specific. A database
JSON exported from project A and imported into project B will not resolve action sub-assets
correctly. Rule settings, patterns, and target folders transfer correctly; action bindings do not.

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

Default extensions: `.fbx .obj .dae .3ds .png .jpg .jpeg .tga .psd .tiff .exr .hdr .wav .mp3 .ogg .aif .aiff`

Each entry must include the dot.

### Ignored folders

Asset paths that Asset Router never processes. Any file under a listed folder is skipped before
rule matching.

Default ignored: `Assets/Editor/`, `Assets/Plugins/`, `Assets/StreamingAssets/`, `Assets/AssetRouter/`, `Packages/`

Each entry must start with `Assets/`.

---

## Default rules

The database created on first load contains six rules. The first two use Path Templating
(see [Path Templating](#path-templating)) and demonstrate routing to per-asset subfolders.

| Rule Name | Pattern | Mode | Target Folder | Preset |
|-----------|---------|------|---------------|--------|
| UI Textures | `UI_*` | Glob | `Assets/Art/UI/` | TextureImporter_UI |
| Character Textures | `T_Char_*_*` | Glob | `Assets/Art/Characters/{1}/` | TextureImporter |
| Location Textures | `^T_Loc_(?<loc>\w+)_.*` | Regex | `Assets/Art/Locations/{loc}/` | TextureImporter |
| General Textures | `T_*` | Glob | `Assets/Art/Textures/` | TextureImporter |
| Sound Effects | `SFX_*` | Glob | `Assets/Audio/SFX/` | AudioImporter |
| Music | `Mus_*` | Glob | `Assets/Audio/Music/` | AudioImporter_Music |

These are starting points. Rename, reorder, or delete them as needed.

---

## Bundled presets

The package ships with 10 presets in the `Presets/` folder:

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
Open the Diagnostic Window (Tools > Asset Router > Diagnostic Window). If the file does not appear
there, its extension is not in the Monitored Extensions list or its path is under an Ignored Folder.
If it appears with outcome "no match", no rule matched the file name. Check the pattern in Settings
or open the Validate tab to find all unmatched files.

**The rule does not match even though the pattern looks right.**
Check the live preview in the rule detail panel. If the preview shows 0 matches, the pattern is
probably too specific. If the asset is in a subfolder and your pattern includes a path separator,
enable Match Full Path on the rule.

**The preset was not applied.**
The preset type must match the asset type. A TextureImporter preset on an audio file does nothing.
Check the preset asset in the Inspector to confirm its type matches.

**Actions did not run.**
Actions only run after the asset is moved. If the file was already in the target folder, the move
is skipped and actions do not run unless Force Re-import is enabled in Dry Run.
Open the Diagnostic Window and check the outcome column.

**Undo did not restore a file.**
Undo moves files back to their recorded original paths. If a file was manually moved or deleted
after the import session, undo skips it. The summary dialog after undo lists exactly which files
could not be restored.

**The conflict warning appears on a rule.**
A warning (⚠) means either two rules have identical patterns, or the overlap heuristic detected
that a sample set of project assets matches both rules. The first rule in the list wins. Reorder
or adjust the patterns to remove the conflict. The heuristic can produce false positives; check
the Validate tab to confirm actual routing.
