# Asset Router — Feature Catalog

Every feature the plugin ships with, grouped by purpose. Each entry explains what the feature does and
why it exists, followed by a concrete before/after example from a real pipeline.

This document is the source for store copy and tech-lead presentations.
See [index.md](index.md) for all other documentation links.

---

## Routing

### Auto-routing on import

When you drop a file into the project Unity fires its asset postprocessor. Asset Router intercepts
every import in `OnPostprocessAllAssets`, checks each file against the active rule set, and — if a
rule matches — applies the preset and moves the file to the target folder automatically. The entire
sequence runs inside a single `StartAssetEditing / StopAssetEditing` batch so Unity triggers only one
reimport per file, not one per move.

**Example.** An artist drops `T_Rock_D.png` into `Assets/Raw/`. Asset Router matches the
"General Textures" rule (`T_*`), applies the `TextureImporter` preset, and moves the file to
`Assets/Art/Textures/T_Rock_D.png` — before the artist can even switch windows.

---

### Glob patterns

Rules use shell-style glob syntax in the **Pattern** field. `*` matches any sequence of characters
within a single path segment (no `/`). `**` matches across path segments (any number of folders).
`?` matches exactly one character. All other characters are literals. Patterns are
compiled to `Regex` with `Compiled | IgnoreCase | CultureInvariant` and cached per rule — no overhead
in the hot import path.

**Example.** Pattern `SFX_*_Loop.wav` matches `SFX_Wind_Loop.wav` and `SFX_Fire_Loop.wav` but
not `SFX_Click.wav`. Add the rule once; all looping sound effects land in `Assets/Audio/Loops/`
without touching each file manually.

---

### Regex patterns

Switch a rule's **Mode** dropdown to **Regex** and write any valid .NET regular expression. This gives
you named capture groups, character classes, anchors, and everything else the regex engine supports.
A 50 ms timeout guards against catastrophic backtracking; a malformed pattern shows an error label
in the rule detail panel instead of throwing silently.

**Example.** Pattern `^T_Loc_(?<loc>\w+)_.*` matches `T_Loc_Forest_Rock.png` and captures `Forest`
into the named group `loc`. The target `Assets/Art/Locations/{loc}/` expands to
`Assets/Art/Locations/Forest/` at move time — the correct subfolder without writing 50 rules, one
per location.

---

### Match Full Path

By default patterns are matched against the **file name only** (e.g. `T_Rock.png`). Enable
**Match Full Path** on a rule to match against the full asset path instead
(e.g. `Assets/Raw/Props/T_Rock.png`). Required for any pattern that includes path separators.

**Example.** A team stores vendor assets under `Assets/External/`. Rule pattern
`Assets/External/**` with **Match Full Path** on routes everything from that vendor folder to
`Assets/Vendor/` using a catch-all, while a second rule with **Match Full Path** off still handles
normal `T_*` textures by filename — the two rules coexist without conflict.

---

### Path Templating

The **Target Folder** field supports capture group tokens that expand at move time:

| Token | Source |
|-------|--------|
| `{1}`, `{2}`, … | Positional — Glob: each `*` and `**` becomes a numbered group (left to right) |
| `{name}` | Named — Regex: use `(?<name>…)` in the pattern |
| `{{` / `}}` | Literal brace escapes (like .NET `string.Format`) |

Captured values are sanitised before use: path-traversal segments (`..`, `.`), Windows-invalid
characters, and leading dots are stripped. An unknown token logs a warning and is kept literally,
producing a visible folder name so the misconfiguration is easy to spot in the Assets browser and
in the History tab.

**Example.** Pattern `T_Char_*_*` (Glob) + target `Assets/Art/Characters/{1}/`. Drop
`T_Char_Hero_Diffuse.png` → it moves to `Assets/Art/Characters/Hero/`. Drop
`T_Char_Boss_Normal.png` → `Assets/Art/Characters/Boss/`. One rule handles every character;
new characters get their folder automatically.

---

### Preset auto-apply

Each rule holds a reference to a Unity **Preset** asset. During `OnPreprocessAsset` Asset Router
calls `preset.ApplyTo(assetImporter)` for every file that matches the rule. Preset type mismatches
(e.g. a TextureImporter preset on an FBX file) produce a warning instead of an exception and the
file is still processed normally.

**Example.** The `TextureImporter_UI` preset enforces Sprite mode, RGBA 32-bit, no mipmaps, and
Filter Bilinear. Assign it to the "UI Textures" rule. Every `UI_*.png` dropped into the project
gets all four settings applied automatically — no one on the team needs to remember them.

---

### Scope folder

A rule's optional **Scope Folder** field restricts matching to assets that already live inside a
specific folder. Two rules with the same pattern but different scope folders can route the same file
name to different destinations depending on where the artist dropped it.

**Example.** Artist A works in `Assets/Raw/CharacterArt/`, artist B in `Assets/Raw/EnvironmentArt/`.
Rule 1: pattern `T_*`, scope `Assets/Raw/CharacterArt/`, target `Assets/Art/Characters/`.
Rule 2: pattern `T_*`, scope `Assets/Raw/EnvironmentArt/`, target `Assets/Art/Environment/`.
The same `T_Rock.png` from different drop locations lands in the right place for each artist.

---

### Monitored extensions

The **Monitored Extensions** list in General Settings defines which file types Asset Router watches.
Assets with any other extension are silently ignored — they pass through Unity's importer untouched.
Each entry must include the dot (e.g. `.png`). Defaults cover common texture, model, and audio
formats.

**Example.** A project also imports `.svg` icons. Add `.svg` to the list. From that point on the
SVG importer rule fires for every new icon, moving files to `Assets/Art/Icons/` and applying the
SVG preset — without touching scripts, prefabs, or any other asset type.

---

### Ignored folders

The **Ignored Folders** list defines paths that Asset Router never processes. Any asset inside a
listed folder is skipped before pattern matching even runs. Defaults exclude `Assets/Plugins/`,
`Assets/Editor/`, `Assets/StreamingAssets/`, and the router's own `Assets/AssetRouter/` folder.

**Example.** A third-party package lives in `Assets/ThirdParty/`. Add `Assets/ThirdParty/` to
Ignored Folders. Its assets are never touched by routing rules, even if their names happen to match
a pattern like `T_*`.

---

### Per-rule enable / disable

Each rule has an **isEnabled** toggle visible as a checkbox in the rules list. A disabled rule is
skipped entirely during matching; it stays in the list so it can be re-enabled later without
re-entering all its settings.

**Example.** During a crunch sprint the team temporarily disables the "Naming Validator" catch-all
rule that logs a warning for unknown files, to reduce noise. After crunch they re-enable it in one
click and any files imported during that window appear in the Validate tab as convention violations.

---

## Safety

### Dry Run preview

The **Dry Run** tab scans every monitored asset in the project and shows a table of what *would*
happen if you applied all rules now — without moving a single file. Each row shows the current
folder, the matched rule, and the resolved target path (including expanded `{token}` values). You
can toggle **Show unmatched** to see files that would get no rule, and select individual rows before
applying.

**Example.** Before running Batch Re-import on a 3 000-file project the lead opens Dry Run, scans,
and sorts by Target Folder. She spots 12 meshes that would move to `Assets/Art/Characters/Boss/`
when they should go to `Assets/Art/Environment/`. She fixes the rule, rescans, and only then clicks
**Apply Selected** — zero misfiled assets.

---

### Batch re-import

**Apply Selected** in the Dry Run tab moves all checked assets and runs their rule's preset and
actions in one batched operation (`StartAssetEditing / StopAssetEditing`). The optional
**Force Re-import In-Place** toggle re-applies the preset even for files already in the correct
folder — useful when a preset changes and you need to refresh all matching assets without moving them.
A cancellable progress bar shows the current file; the final log line reports `Moved / Reimported /
Skipped / Errored` counts.

**Example.** A preset's compression settings change project-wide. Enable **Force Re-import In-Place**
in Dry Run, scan, select all texture rows, click **Apply Selected**. Every texture re-imports with
the new compression without anyone touching individual assets.

---

### History and undo

Every batch move — from auto-import, Dry Run apply, or Batch Re-import — writes a session entry to
`Library/AssetRouter/log.json`. Each entry records `from`, `to`, and the rule name. The **History**
tab lists sessions newest-first with a timestamp and source label. Clicking **Undo Selected Session** reverses
all moves in that session in reverse order, inside a single `StartAssetEditing` block. A summary
dialog reports `Reverted / Failed` counts after completion. The log is capped at 500 sessions; write
is atomic (`File.Replace`) so a crash mid-write never corrupts it.

**Example.** A wrong glob pattern routes 200 textures to the wrong folder. The lead opens History,
finds the auto-import session from two minutes ago, clicks **Undo Selected Session**. All 200 files return to
their original paths. Unity preserves asset GUIDs across `MoveAsset`, so no scene references break.

---

### Conflict detection

`ConflictDetector` runs automatically whenever the rule list changes and reports two types of
problems. **Duplicate**: two rules share the same pattern, mode, match-full-path flag, and scope
folder — one of them is dead by definition. **Overlap**: a heuristic check runs both rules against
14 fixed sample paths plus up to 100 real asset paths from the project; if both match the same path,
they overlap. Conflicting rules are marked `⚠` in the rules list. A banner at the top of the
Settings tab shows the counts and a note that overlap detection is heuristic (false negatives are
possible for uncommon patterns). The sample path set is cached and invalidated on project or package
changes so the check stays cheap during editing.

**Example.** A dev adds a "Character Textures" rule (`T_Char_*_*`) after a general "Textures" rule
(`T_*`). The banner immediately shows "1 overlap(s)" and both rules get `⚠` icons. She drags
"Character Textures" above "Textures" to fix the priority — the banner clears.

---

### Unknown files dialog

When a monitored file matches no rule and **Popup for Unknown Files** is on, Asset Router collects
all such files from the import batch and shows a single dialog listing their names. Choices are:
**Import all as-is** (keep files where they are and log a warning per file), **Delete all…** (with
a second confirmation), or **Cancel** (leave files untouched with no log). Multiple files from one
drag-drop are grouped into one dialog instead of one popup per file.

**Example.** An artist accidentally drops a `Concept_Draft.psd` that follows no naming convention.
The dialog lists it and asks what to do. She clicks **Cancel**, renames the file to match the
`T_*` pattern, and re-imports — now it routes correctly.

---

### Multi-database warning

Asset Router's live auto-import always uses whichever `ImporterSettingsDatabase` asset Unity finds
first via `FindAssets`. When the Settings window has a *different* database selected (e.g. a test
database), a yellow help box warns: "You're editing X but live auto-import is driven by Y." This
prevents a silent split-brain where edits in the window have no effect on actual imports.

**Example.** A developer created a `ImporterSettingsDatabase_Test.asset` for experimentation and
forgot to delete it. When she edits the real database in the window the warning appears immediately,
so she either deletes the test asset or explicitly switches the picker to the live one.

---

## Diagnostics & Monitoring

### Per-rule statistics

Each rule accumulates a **match count** across sessions, stored in `Library/AssetRouter/stats.json`.
The count is incremented once per batch, not once per file, so busy rules do not dominate
disproportionately. In the rules list every rule displays its count as `(N✓)` next to the name. An
in-memory session counter (`_sessionMatchCount`) tracks hits within the current Editor session
independently of the persistent store. Reset the counters via
**Tools > Asset Router > Clear Rule Statistics**.

**Example.** After two weeks of importing, the "Music" rule shows `(0✓)`. Investigation reveals
`.mp3` and `.ogg` are in Monitored Extensions but the "Music" rule pattern `Mus_*` never matched
because all music files are actually named `BGM_*`. The statistic surfaces the dead rule before any
release.

---

### Naming Validator (Validate tab)

The **Validate** tab scans all monitored assets and lists every file that matches no rule — the same
scan as Dry Run, filtered to unmatched entries only. The table shows file name and current folder.
**Copy to Clipboard** exports the list as a line-separated path list for pasting into a ticket or
spreadsheet. No files are moved; this is a read-only audit.

**Example.** At the start of a naming-convention push, a tech artist opens the Validate tab and
scans. She finds 340 files out of convention. She copies the list to a Jira ticket, assigns it to
the team, and re-runs the scan weekly until the count reaches zero.

---

## Extensibility

### Post-import actions pipeline

Every `ImportRule` has an ordered list of **Post-Import Actions**. After a file is moved, Asset
Router runs each action's `Execute` method in sequence. A `try/catch` wraps each call so a failure
in one action logs an exception but does not stop the rest of the chain. `CanRunOn` is called first;
an action that returns false is skipped without an error. Actions that create new assets (prefabs,
materials, SOs) run after a `delayCall` defer so they are not nested inside Unity's import batch —
which Unity explicitly forbids.

**Example.** A "Character Textures" rule has three actions: `SetPivotAction` (center pivot),
`AppendToCatalogAction` (add to `CharacterCatalog.asset`), `CreatePrefabFromTemplateAction`
(instantiate `CharacterPrefab_Template` and save it). Drop `T_Char_Elf_Diffuse.png` — Asset Router
moves it, centers the pivot, registers it in the catalog, and saves `T_Char_Elf_Prefab.prefab` to
the output folder. Three steps, one drag-and-drop, zero manual work.

---

### Built-in actions

Eleven actions ship with the package, covering a spectrum of architectural patterns:

| Action | What it does |
|--------|-------------|
| **SetPivotAction** | Sets sprite pivot to a configurable (x, y) and re-imports. Tier A — simplest possible action. |
| **TrimAudioSilenceAction** | Trims leading and trailing silence from 16-bit PCM WAV files by rewriting the file and re-importing. |
| **AppendToCatalogAction** | Adds the imported asset to an `AssetCatalog` ScriptableObject. Idempotent. |
| **RegisterAddressableAction** | Adds the asset to an Addressables group. Requires the Addressables package; compiles to a no-op without it. |
| **EmitUnityEventAction** | Fires a serialized `UnityEvent<Object>` wired in the Inspector. No code required by the end user. |
| **CreatePrefabFromTemplateAction** | Instantiates a template prefab, calls `IAssetRouterPrefabSetup.SetupAssetRouter` on all implementing components, and saves the result as a new prefab. |
| **CreateScriptableObjectFromTemplateAction** | Same factory pattern for ScriptableObjects via `IAssetRouterDataSetup`. |
| **CreateMaterialFromTextureAction** | Creates a new Material from a base material template and assigns the imported texture to a named shader property. |
| **GenerateSpritePhysicsShapeAction** | Reads pixel data to compute an opaque bounding box and sets it as the sprite's physics shape. Requires Read/Write on the texture. |
| **GenerateNineSliceBordersAction** | Scans transparent edges to compute 9-slice border values and sets `spriteBorder` on the TextureImporter. |
| **CreateTilePaletteEntryAction** | Creates a `Tile` asset from the imported sprite and saves it to a configurable output folder. |

Per-action documentation with configuration tables, idempotency notes, and edge cases:
[actions/README.md](actions/README.md).

---

### Custom action authoring

Any `ScriptableObject` that inherits `AssetImportActionAsset` (which implements `IAssetImportAction`)
becomes a first-class action that appears in the `+` dropdown in every rule's action list. Two
methods to implement: `CanRunOn(Object importedAsset, AssetImportContext ctx) → bool` (gate check)
and `Execute(Object importedAsset, AssetImportContext ctx)` (the work). `AssetImportContext` carries
the asset path, the matched rule, the active database, and a logger. Decorate with
`[CreateAssetMenu]` so users can create instances in the Project window.

**Example.** A mobile studio needs to auto-generate a `TextureAtlas` entry when a UI sprite is
imported. A programmer writes `RegisterInAtlasAction : AssetImportActionAsset`, implements the two
methods, and saves the file. No plugin changes needed. The action appears in the `+` menu
immediately after compilation. See [api/extension-points.md](api/extension-points.md) for the full
guide and assembly setup.

---

### Action scaffolding wizard

**Assets > Create > Asset Router > New Action... > [template]** opens a Save File dialog, asks for
a file name, and generates a ready-to-compile `.cs` file with the correct inheritance, attributes,
and namespace filled in. Four templates cover the four main architectural tiers:

| Template | Demonstrates |
|----------|-------------|
| **Basic Action** | Minimal stub — `CanRunOn = true`, empty `Execute`. |
| **Texture Filter Action** | Reads `TextureImporter`, modifies a setting, calls `ImportAsset(ForceUpdate)`. |
| **Sprite Factory Action** | `LoadAssetAtPath<Sprite>` → transform → `CreateAsset`. |
| **Prefab Factory Action** | `InstantiatePrefab` → `IAssetRouterPrefabSetup` callback → `SaveAsPrefabAsset`. |

**Example.** A tech artist has never written a Unity Editor action before. She picks
**Texture Filter Action**, names it `SetCompressionQualityAction.cs`, and opens it. The file has
all the boilerplate filled in and four `// TODO` comments marking the lines she actually needs to
change. She has a working action compiling in under five minutes.

---

## Team Workflow

### JSON export / import

The **Export JSON** toolbar button saves the active database to a human-readable JSON file.
**Import JSON** replaces the database contents from a file. The format uses a `$type` discriminator
for polymorphic rules, and serialises `Preset` and action references as `{guid, path}` pairs. Import
runs the schema migrator after loading so legacy JSON files from v1 of the schema are automatically
upgraded. Note: GUID references are project-local — cross-project rule sharing works best with the
clipboard (see below) or by copying preset assets alongside the JSON.

**Example.** A team keeps `ImporterSettings.json` in source control. The `.asset` file is in
`.gitignore`. Each developer opens the project, clicks **Import JSON**, and gets the same rule set.
Diffs in pull requests show only the lines that actually changed, not the entire YAML blob Unity
would otherwise produce.

---

### Rule sharing via clipboard

**Copy Rule to Clipboard** (button in the rule detail panel) serialises a single rule to JSON and
puts it in the system clipboard. **Paste from Clipboard** (button in the rules list header) deserialises
it and adds it to the current database. Preset and action references are stored as `{guid, path}`
pairs; if the GUID is not found in the destination project the path is tried as a fallback and a
warning is logged for each unresolved reference.

**Example.** A lead built an elaborate "Character FBX" rule with three actions and a custom preset.
She clicks **Copy Rule to Clipboard**, pastes the JSON into a Slack message, and a teammate on
a different project clicks **Paste from Clipboard**. The rule appears instantly; the teammate only
needs to re-link the preset asset because it does not exist in their project yet.

---

### Multi-database support

The Settings window toolbar has an **object picker** that lets you switch between any number of
`ImporterSettingsDatabase` assets in the project. **Create New** generates a fresh database with the
default rule set at a location you choose. The live auto-import postprocessor always uses the *first*
database Unity finds (`FindAssets`), so a warning banner appears when the window's selected database
differs from the live one — preventing silent no-ops when editing the wrong database.

**Example.** A project has a `Main.asset` database for production rules and an
`Experimental.asset` for a new pipeline the team is prototyping. Artists see the production rules
in the main window; the tech lead switches the picker to `Experimental.asset`, edits freely, runs a
Dry Run to preview, and never touches production rules by accident.

---

### Welcome Window on first launch

On the first Editor load in a project that has no `ImporterSettingsDatabase`, Asset Router shows a
small utility window once per session. The window explains what the plugin does and offers two
buttons: **Create** (generates the database at `Assets/AssetRouter/` with six default rules and
opens the Settings window) or **Not now** (dismisses without any side-effect). The window is
suppressed in batch/CI mode (`Application.isBatchMode`). Subsequent Editor sessions where a database
already exists see nothing — the window does not re-appear.

**Example.** A new team member adds Asset Router to the project via Package Manager. On the next
Editor load she sees the Welcome Window, reads the two-sentence description, clicks **Create**, and
immediately has a working rule set open in the Settings window — without reading any documentation
first.
