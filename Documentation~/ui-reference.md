# UI Reference

A complete map of every window, tab, menu item, and dialog in Asset Router. Each section starts
with what the window is for, then lists its elements in the order they appear on screen.

New to the plugin? Read [Getting Started](getting-started.md) first. This page is a reference,
not a tutorial.

## Where everything lives

| Menu item | What it does |
|-----------|--------------|
| **Tools > Asset Router > Settings** | Opens the [main window](#main-window) with rules and tabs |
| **Tools > Asset Router > Documentation** | Opens the online documentation on GitHub |
| **Tools > Asset Router > Report Issue** | Opens the GitHub issue tracker |
| **Tools > Asset Router > Clear Rule Statistics** | Resets the `(N✓)` match counters of all rules, after a confirmation |
| **Assets > Create > Asset Router > Settings Database** | Creates a new database asset in the current Project folder |
| **Assets > Create > Asset Router > New Action...** | Opens the [action scaffolding wizard](#action-scaffolding-wizard-assets--create--asset-router--new-action) |

The [Welcome Window](#welcome-window) opens on its own, once per session, when the project has no
database yet.

---

## Main window

Open via **Tools > Asset Router > Settings**. This is where you edit rules, preview what they
would do, and undo what they did. The window has a toolbar that is always visible and four tabs:
**Settings**, **Dry Run**, **History**, **Validate**.

### Toolbar

Elements left to right:

| Element | What it does | When you need it |
|---------|--------------|------------------|
| **Database** field | Shows the database the window is editing. Pick a different `ImporterSettingsDatabase` asset to switch. | Only when the project has more than one database, e.g. a production and an experimental one. |
| **Create New** | Creates a new database asset, pre-filled with the six default rules, at a location you choose. | First setup, or when you want a separate database to experiment with. |
| **Export JSON** | Saves the current database to a human-readable `.json` file. | Version control with readable diffs, or sharing the full rule set. See [JSON export and import](DOCUMENTATION_EN.md#json-export-and-import). |
| **Import JSON** | Replaces the contents of the current database with a `.json` file. | Restoring a shared rule set on another machine. |

### Notices below the toolbar

Two help boxes can appear here:

- **No database selected.** Shown with a **Create New Database** button when the window has no
  database to edit. Create one or pick an existing one in the toolbar.
- **Multi-database warning.** Live auto-import always uses the first database Unity finds in the
  project, no matter which one the window has open. When those two differ, a yellow warning names
  both. Switch the picker to the live database, or delete the extra one, so your edits actually
  take effect.

---

### Settings tab

Everything about what rules exist and how they behave. The tab stacks four blocks: general
settings, file filters, the rules list, and the details of the selected rule. A bold
**Save / Apply** button sits at the bottom; click it to write your changes to disk.

#### General Settings

| Element | What it does | When you need it |
|---------|--------------|------------------|
| **Enable Auto Import** | Master switch. When off, nothing routes automatically on import; Dry Run still works. | Turn off when you want manual, batch-only routing via the Dry Run tab. |
| **Popup for Unknown Files** | When on, imported files that match no rule trigger a dialog (keep / delete). | Turn off if the dialog gets noisy on projects with many unconventional file names. |

#### File Filter Settings (collapsed by default)

Click the foldout to expand.

| Element | What it does | When you need it |
|---------|--------------|------------------|
| **Monitored Extensions** | The file types Asset Router watches. Files with any other extension are ignored entirely. Each entry needs the dot, e.g. `.png`. | Add an entry when a new file type (e.g. `.svg`) should be routed. |
| **Ignored Folders** | Paths the router never touches, no matter what the file is named. Each entry starts with `Assets/`, e.g. `Assets/Plugins/`. | Add third-party or generated folders that rules must never move files out of. |

#### Conflict banner

A yellow banner appears above the rules list only when the conflict detector finds problems. It
counts **duplicates** (two rules with identical pattern, mode, and scope) and **overlaps** (two
rules that match the same sample files). Affected rules get a `⚠` mark in the list. Overlap
detection is heuristic, so uncommon patterns can slip past it. Fix conflicts by reordering rules
(first match wins) or tightening the patterns.

#### Import Rules list

The ordered list of rules. Order is priority: on import, the first matching rule from the top
wins.

| Element | What it does | When you need it |
|---------|--------------|------------------|
| **Paste from Clipboard** (in the list header) | Adds a rule previously copied as JSON with **Copy Rule to Clipboard**, even from another project. | Receiving a rule from a teammate. See [Rule Sharing](rule-sharing.md). |
| Row checkbox | Enables or disables the rule. A disabled rule is skipped during matching but keeps all its settings. | Temporarily switching a rule off without deleting it. |
| Row label | Shows `name [pattern] -> target folder`. A `⚠` prefix marks a conflict; a `(N✓)` suffix shows how many import batches the rule has matched. | Reading the whole rule set at a glance. |
| Drag handle | Reorders rules. | Putting specific rules above general catch-alls like `T_*`. |
| **+** (below the list) | Adds a new empty rule and selects it. | Creating a rule. |
| **−** (below the list) | Removes the selected rule. Action assets used only by this rule are deleted with it. | Deleting a rule. |

#### Rule details

Appears below the list when a rule is selected. Sections top to bottom:

| Element | What it does | When you need it |
|---------|--------------|------------------|
| **Rule Name** | Display name used in the list, logs, and statistics. | Always; give rules names your team recognizes. |
| **Mode** | Switches the pattern between **Glob** and **Regex**. | Glob covers most cases; Regex adds named groups and full expression power. |
| **Pattern** | The pattern matched against the file name. Glob example: `T_*_D.png`. | The core of every rule. See [pattern syntax](DOCUMENTATION_EN.md#pattern-syntax). |
| **Match Full Path** | Matches the pattern against the full asset path (`Assets/Raw/T_Rock.png`) instead of the file name. | Required for path-based patterns such as `Assets/Raw/**`. |
| **Scope Folder** | Restricts the rule to assets already inside this folder. | Routing the same file name differently depending on where it was dropped. |
| Live preview (line under the pattern fields) | Shows up to 3 real project files the pattern matches, green. With `{tokens}` in the target it shows `file -> resolved path`. Red text means the pattern or a token is invalid. Updates 300 ms after you stop typing. | Checking a pattern without importing anything. |
| **Target Folder** | Where matched assets move. Must start with `Assets/`. Supports `{1}`, `{name}`, `{{`/`}}` tokens that expand from pattern captures. | Always. For tokens see [Path Templating](api/path-templating.md). |
| **Import Preset** | Unity Preset applied to the importer before import. Its type must match the asset type. | Enforcing import settings (compression, sprite mode, etc.) per rule. See [Presets](presets.md). |
| **Post-Import Actions** | Ordered list of actions that run after the move, top to bottom. **+** opens a menu of every available action type, built-in and custom. **−** removes the selected action and deletes its sub-asset when no other rule uses it. Drag to reorder. Click an entry and edit its fields in the Inspector. | Automating what you would otherwise do by hand after import. See [Built-in Actions](actions/README.md). |
| **Copy Rule to Clipboard** | Serializes this rule to JSON in the system clipboard, for **Paste from Clipboard** in any project. | Sharing one tuned rule without exporting the whole database. |

#### Save / Apply

Writes the database asset to disk. Field edits take effect in the window immediately, but click
this button before closing Unity or committing, so nothing is lost.

---

### Dry Run tab

A safe preview: shows what would happen to every monitored file in the project before anything
moves. Use it to test rules on an existing project and to run batch moves under your control.

Toolbar, left to right:

| Element | What it does | When you need it |
|---------|--------------|------------------|
| **Scan Project** | Scans all monitored assets and fills the table. A progress bar shows progress and can be cancelled. | Always the first click on this tab. |
| **Select All** / **None** | Checks or unchecks every matched row. | Bulk selection before applying. |
| **Show unmatched** | Also lists files that match no rule (they are hidden by default). | Auditing what the rules miss. |
| **Force re-import** | Makes files that are already in the right folder selectable, so applying re-runs their preset and actions. | After changing a preset or action on files that no longer need moving. |

Below the toolbar a summary line reports the scan: how many files would move, how many are
already in place, and how many match no rule.

Table columns: a selection checkbox, **File**, **Current Folder**, **Target Folder** (shows
`(in place)` when the file is already there), and **Rule**.

Buttons at the bottom:

| Element | What it does | When you need it |
|---------|--------------|------------------|
| **Apply Selected (N)** | Moves every checked file, applies presets, runs actions. One History session records the batch. | The moment you trust the preview. |
| **Force Re-import In-Place** | Skips the table entirely: re-applies preset and actions to every matched file that is already in its correct folder. Moves nothing. | Refreshing all assets after a preset change, without reviewing rows one by one. |

---

### History tab

Every batch of moves, from auto-import or Dry Run, is recorded as a session. This tab lists the
sessions and can undo any of them.

| Element | What it does | When you need it |
|---------|--------------|------------------|
| **Refresh** | Loads the session list from disk. | First click on this tab, and after new imports. |
| **Undo Selected Session** | Moves every file in the session back to its original path, in reverse order. Files no longer at the recorded path are skipped with a warning; a summary dialog appears when anything failed. | A rule misfired and files went to the wrong place. |
| **Clear History** | Permanently deletes all sessions after a confirmation. | Housekeeping; this cannot be undone. |
| **Sessions** panel (left) | One row per session: timestamp, source (`AutoImport` or `BatchMover`), and the number of moves. Newest at the top. Click to select. | Finding the batch you want to inspect or undo. |
| **Entries** panel (right) | The individual `from -> to` moves of the selected session. | Verifying what a session actually did before undoing it. |

Undo reverses file moves only. Import settings applied by presets stay as they are, and assets
created by post-import actions (generated prefabs, materials, tiles) are not deleted. The log lives
in `Library/AssetRouter/`, so it is per-machine and survives Unity restarts but not a fresh clone.

---

### Validate tab

A read-only audit: lists every monitored asset that matches no rule. Nothing is moved. Use it to
measure how much of the project follows the naming convention.

| Element | What it does | When you need it |
|---------|--------------|------------------|
| **Scan Project** | Runs the scan and fills the table. | Always the first click on this tab. |
| **Copy to Clipboard** | Copies the unmatched asset paths as plain text, one per line. | Pasting the offender list into a ticket or spreadsheet. |
| Table (**File**, **Current Folder**) | The unmatched files. When everything matches, the tab says so instead. | Deciding whether to rename files, add rules, or leave them alone. |

---

## Welcome Window

Opens by itself on Editor load when the project contains no `ImporterSettingsDatabase`, at most
once per session, and never in batch mode. It offers to create the default database.

| Element | What it does | When you need it |
|---------|--------------|------------------|
| **Create** | Creates `Assets/AssetRouter/ImporterSettingsDatabase.asset` with the six default rules and opens the main window. | The recommended first step after installing. |
| **Not now** | Closes the window with no side effects. | You want to set things up later via **Tools > Asset Router > Settings**. |

---

## Action scaffolding wizard (Assets > Create > Asset Router > New Action...)

A code generator for custom actions. Pick a template, choose where to save the `.cs` file, and
the wizard writes a ready-to-compile action class named after the file. After Unity compiles it,
the new action appears in the **+** menu of every rule's Post-Import Actions list.

| Template | What the generated code demonstrates |
|----------|--------------------------------------|
| **Basic Action** | The minimal skeleton: `CanRunOn` returns true, `Execute` is empty. |
| **Texture Filter Action** | Reading a `TextureImporter`, changing a setting, re-importing. |
| **Sprite Factory Action** | Loading the imported sprite and creating a new asset from it. |
| **Prefab Factory Action** | Instantiating a template prefab, configuring it, saving it as a new prefab. |

See [Writing Your Own Action](api/extension-points.md) for the contract behind the templates.

---

## Dialogs

| Dialog | When it appears | Choices |
|--------|-----------------|---------|
| **Unknown Files** | Monitored files matched no rule and **Popup for Unknown Files** is on. One dialog lists the whole batch. | **Import all as-is** keeps the files and logs a warning per file. **Cancel** (also Esc) keeps them silently. **Delete all…** asks for a second confirmation, then deletes them. |
| **Clear History confirmation** | After clicking **Clear History**. | **Clear** deletes all sessions permanently; **Cancel** aborts. |
| **Undo summary** | After an undo where at least one file could not be restored. | Read-only summary of reverted and failed counts. |
| **Export / Import failed** | A JSON export or import threw an error. | Read-only message with the reason. |
