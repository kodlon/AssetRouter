# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- **`CreateMaterialFromTextureAction` — pipeline-default fallback.** When `baseMaterial` is null the action now resolves to the active SRP's `RenderPipelineAsset.defaultMaterial` (URP/HDRP) or the Built-in RP `Default-Material`, so the default `General Textures` rule produces a `.mat` out of the box without manual configuration. Texture assignment also falls back from the configured `textureProperty` to `Material.mainTexture` when the property isn't on the shader — makes `_MainTex` (Standard) and `_BaseMap` (URP Lit) work with the same default.
- **`CreateMaterialFromTextureAction.outputFolder` accepts relative paths.** Absolute (`Assets/...`) still works as before; a relative value like `Materials` resolves as a subfolder of the imported texture's folder. Default is now `Materials`, so materials group under `TextureFolder/Materials/` instead of being scattered next to textures.
- **Default database — `General Textures` (`T_*`) now runs `CreateMaterialFromTextureAction`.** Dropping a `T_*.png` into the project creates a `Materials/T_*_Mat.mat` next to it automatically.
- Undo cleans up assets and folders created by import actions (materials, prefabs, SOs, tile-palette entries). New `IArtifactSink` + `ArtifactCollector`; the 4 built-in Create actions and `PathUtility.EnsureFolderExists` feed into it. Log schema bumped to v2 with `createdAssets` / `createdFolders`; v1 files still load.
- Undo now writes itself to the operation log with `source = "Undo"` (audit trail + de-facto redo when clicked again).
- Recycle folder `Assets/_AssetRouterRecycle` — Undo of root-drop imports moves assets here instead of dumping them back at `Assets/` root. Files originally in a subfolder still restore in place.
- History timestamps show in local time.
- **Clear Rule Statistics menu item.** `Tools > Asset Router > Clear Rule Statistics` resets the
  per-rule match counters shown as `(N✓)` in the rules list, after a confirmation dialog. Previously
  the counters could not be cleared from the UI (`Library/AssetRouter/stats.json` had to be deleted
  manually).
- **Beginner documentation.** New `Documentation~/getting-started.md` (step-by-step first run),
  `ui-reference.md` (every window, tab, and button), `patterns.md` (Glob and Regex cookbook), and
  `presets.md` (creating and assigning presets). Stale references fixed across existing docs: old
  menu path `Tools > Asset Router Settings`, removed `AssetsBeingMoved` guard description, built-in
  action count (11, not 10), full-database JSON format documented.
- **Welcome Window on first launch.** When no `ImporterSettingsDatabase` exists in the project and the
  Editor is running interactively, a small utility window opens once per session and asks whether to
  create a default database. Clicking **Create** writes the database to `Assets/AssetRouter/` and opens
  the Asset Router Settings window; clicking **Not now** dismisses without any side-effects. The window
  is suppressed in batch/CI mode (`Application.isBatchMode`).
- **ConflictDetector sample-path caching.** `ConflictDetector.Detect` now re-uses a cached set of
  sample paths across repeated calls and invalidates it on `projectChanged` and
  `importPackageCompleted`. Eliminates repeated `AssetDatabase.FindAssets` calls during the same Editor
  session. `BuildSamplePaths` also sorts the raw GUID list before truncation for deterministic overlap
  detection across machines.

### Fixed
- `HandleUnknownAssets` no longer opens a modal dialog in batch/CI mode (`Application.isBatchMode` guard).
- `DatabaseLocator` no longer silently picks the first DB when the project has more than one — auto-import is disabled with a `LogWarning` until only one `ImporterSettingsDatabase` remains. Warning fires once per domain reload (from the postprocessor path only); the migrator / initializer stay silent to avoid `projectChanged` spam.
- One log line per import batch instead of one per file. `[AssetRouter] Moved N asset(s): a, b, c, …`. Also drops the per-preset "Preset applied" info line (kept the warning on type mismatch).
- `BuildPatternPreview` scopes its `AssetDatabase.FindAssets` to `rule.scopeFolder` when set — was walking all of `Assets/` for 3 samples.
- `AssetCatalog.Contains` / `AppendToCatalogAction` are O(1) now: `HashSet<Object>` cache rebuilt via `ISerializationCallbackReceiver`. Serialized list is unchanged; existing catalogs load without any migration.

### Removed
- Diagnostic Window — duplicated History, only worked while open, cleared on every assembly reload. A proper debug view is deferred.
- **Legacy model formats from default monitored extensions.** `.3ds` and `.dae` were dropped; `.fbx` and `.obj` remain. The removed formats are effectively dead in modern Unity pipelines (`.3ds` is 30+ years old; `.dae`/Collada silently drops animation data). Users who need them can still add the extension back via the Settings window.
- **`SetPivotAction` from default `UI Textures` rule.** Pivot on a full-image sprite is a no-op unless the sprite is set to `Custom` alignment first — kept the action in the codebase for users who need it, but out of defaults so a fresh database doesn't ship with a rule that silently does nothing.

---

## [0.9.2] — 2026-07-01

### Fixed
- `TargetResolver.Resolve` fast path incorrectly skipped `}}` escape processing when the
  template contained no `{` character. Templates like `Assets/folder}}/` now correctly resolve
  to `Assets/folder}/`. The fast path now bails out only when the template contains neither
  `{` nor `}`.
- Test `Match_GlobWithDoubleStar_CapturesPath` expectation updated: `**` in glob translates
  to greedy `(.*)`, so the trailing `/` before the next literal segment is captured. Downstream
  `TargetResolver.Resolve` collapses any resulting `//` in the resolved target, so this is
  safe in practice.
- Test `Resolve_EmptyCapture_GroupNotParticipated_KeepsTokenLiterally` corrected for .NET
  regex group numbering: named groups are numbered AFTER unnamed groups, so in
  `^(?<x>foo)?(Hello)$` the `(Hello)` group is `{1}` and `(?<x>foo)?` is `{2}` / `{x}`.

### Changed
- `package.json` version bumped to `0.9.2`.

---

## [0.9.1] — 2026-06-30

### Added
- **Path Templating.**
  Capture group tokens in `targetFolder` let a single rule route assets to different
  subdirectories based on their name without writing N×M rules.
  - **Token syntax in `targetFolder`:**
    - `{1}`, `{2}`, … — positional captures (Glob: each `*` or `**` becomes capture group 1, 2, …)
    - `{name}` — named captures (Regex: `(?<name>…)` syntax)
    - `{{` / `}}` — literal brace escapes (mirrors .NET `string.Format`)
  - `GlobToRegex` updated: `*` → `([^/]*)`, `**` → `(.*)` (both now capture groups). `?` unchanged.
  - `PatternMatcher.Match(rule, path)` — new method returning the `System.Text.RegularExpressions.Match`
    object for downstream token resolution. `Matches` is now a thin wrapper (`Match != null`).
  - `RuleValidator.FindMatchingRule` return type changed from `BaseImportRule` to `RuleMatch?`
    (internal readonly struct `{ BaseImportRule Rule; Match Match }`). All call sites updated.
  - `Editor/Logic/TargetResolver.cs` — new class. `Resolve(template, match)` performs a single
    left-to-right pass substituting positional and named tokens, handling `{{`/`}}` escapes.
    Captured values are sanitised: `..` and `.` path segments are rejected (warning + literal token
    fallback); backslashes are normalised to forward slashes.
  - `AssetRouterPostprocessor`: `MoveToTargetFolder` and the `alreadyInPlace` check now resolve the
    target folder through `TargetResolver` before computing paths.
  - `DryRunPlanner.Scan` resolves `targetFolder` via `TargetResolver` at scan time so the Dry Run
    preview shows real resolved paths.
  - `BatchMover.Move` uses the pre-resolved `DryRunEntry.TargetPath` instead of recomputing from
    `MatchedRule.targetFolder`.
  - `AssetMoveCandidate` gains a `Match` field to carry the regex match through to `MoveToTargetFolder`.
  - **Two new default rules** inserted before "General Textures" in `DefaultDatabaseFactory`:
    - *Character Textures* (Glob `T_Char_*_*`, target `Assets/Art/Characters/{1}/`) — positional
      capture demo: `T_Char_Hero_Diffuse.png` → `Assets/Art/Characters/Hero/`.
    - *Location Textures* (Regex `^T_Loc_(?<loc>\w+)_.*`, target `Assets/Art/Locations/{loc}/`) —
      named capture demo: `T_Loc_Forest_Rock.png` → `Assets/Art/Locations/Forest/`.
    New databases (or Inspector Reset) get these rules; existing databases are unaffected.
  - **UI:** Target Folder field gains a multi-line tooltip explaining the token syntax with an example.
  - **Live Preview** in Rule Detail panel: when `targetFolder` contains tokens, the preview shows
    resolved paths (`T_Char_Hero_D.png  →  Assets/Art/Characters/Hero/`) instead of bare filenames.
  - **Tests:**
    - `PatternMatcherTests` +5 tests: `Match_GlobWithStar_CapturesPositionalValue`,
      `Match_GlobWithDoubleStar_CapturesPath`, `Match_RegexNamedGroup_CapturesByName`,
      `Match_NoMatch_ReturnsNull`, `Matches_LegacyWrapper_StillReturnsBool`.
    - `TargetResolverTests` (new) — 10 tests: no-tokens fast path, positional/named substitution,
      missing token literal fallback, `{{`/`}}` escapes, empty capture, `.` traversal sanitisation,
      backslash normalisation, multi-token substitution, null match.
    - `DefaultDatabaseFactoryTests` (new) — 3 tests verifying presence and insertion order of new rules.
    - `RuleValidatorTests` updated: `FindMatchingRule` result assertions now use `?.Value.Rule`.
    - `PatternMatcherTests`: `GlobToRegex_Star_ProducesNonSlashWildcard` and
      `GlobToRegex_DoubleStar_ProducesAnyWildcard` updated to expect capturing groups.

### Changed
- `package.json` version bumped to `0.9.1`.
- `DefaultDatabaseFactory.CreateDefaultRules` now returns 6 rules (up from 4); only fresh databases
  and Inspector Reset receive the two new rules.
- `BatchMover` now reads the pre-resolved `DryRunEntry.TargetPath` for moves (no behaviour change for
  rules without tokens; required for templated rules).

---

## [0.9.0] — 2026-06-27

### Added
- **Documentation overhaul (English + per-action docs).**
  - `DOCUMENTATION_EN.md` — new primary English documentation (~500 lines): flow explanation,
    Settings window, all four tabs, pattern syntax (glob/regex with tables), JSON export/import,
    troubleshooting.
  - `Documentation~/index.md` — UPM entry point linking all sections.
  - `README.md` rewritten: updated from prefix/suffix to glob patterns, current action table (10
    actions), links to documentation.
  - `Samples~/QuickStart/README.md` updated: current action table, three use-case sections with links.
  - Per-action documentation (12 pages in `Documentation~/actions/`): `README.md` index, one page
    per built-in action, `LegacySamples.md`.
  - `Documentation~/api/extension-points.md` — full guide for extension authors.
  - `Documentation~/migrations/v1-to-v2-schema.md` — before/after table for the v1 → v2 schema migration.
  - `Documentation~/use-cases/` — three use-case guides: `mobile-team.md`, `legacy-cleanup.md`,
    `solo-developer.md`.
  - `Documentation~/testing-your-actions.md` — testing guide with asmdef setup and NUnit template.
  - XMLDoc added to all public types: `IAssetImportAction`, `AssetImportActionAsset`,
    `AssetImportContext`, `PatternMode`, `BaseImportRule`, `ImportRule`, `ImporterSettingsDatabase`,
    `AssetCatalog`, `IAssetRouterPrefabSetup`, `IAssetRouterDataSetup`, and all 10 built-in actions.

### Changed
- `package.json` version bumped to `0.9.0`.

---

## [0.8.0] — 2026-06-26

### Added
- **Quality-of-life & observability.**
  - **Per-folder rule scope.** `BaseImportRule` gains a `scopeFolder` field (default `""`).
    When non-empty, `RuleValidator.FindMatchingRule` skips the rule if the asset is not inside that
    folder (via `PathUtility.IsUnderFolder`). Allows the same file-name pattern to route differently
    depending on which source folder an asset was dropped into. UI: "Scope Folder" field in the Pattern
    section of the rule detail panel. Three new tests in `RuleValidatorTests`.
  - **Diagnostic Window.** `Tools > Asset Router > Diagnostic Window` — a live table of every
    monitored asset processed by the postprocessor. Columns: timestamp, filename, matched rule, and
    action (no match / in place / moved / queued). The window registers with `DiagnosticLog.IsEnabled`
    on open and unregisters on close, so zero overhead when unused.
    - `Editor/Logic/DiagnosticLog.cs` — in-memory ring buffer (max 500 entries), cleared on assembly
      reload via `AssemblyReloadEvents.beforeAssemblyReload`.
    - `Editor/View/DiagnosticWindow.cs` — `EditorWindow` with auto-scroll toggle and Clear button.
  - **Per-rule statistics.** How many times each rule matched since first use.
    - `Editor/Logic/RuleStatsStore.cs` — persists counts to `Library/AssetRouter/stats.json` (atomic
      write via `File.Replace`). `IncrementBatch(List<string>)` does a single read + single write per
      import batch regardless of how many assets matched. `ReadAll()` returns a
      `Dictionary<string, int>`.
    - `BaseImportRule._sessionMatchCount` (`[NonSerialized]`) — in-memory per-session counter
      incremented in `OnPostprocessAllAssets`.
    - Asset Router window rule list shows `(N✓)` next to rules that have matched at least once.
    - Four new tests in `RuleStatsStoreTests.cs`.
  - **Naming convention validator.** New "Validate" tab in the Asset Router window.
    - Scans all monitored project assets via `DryRunPlanner.Scan` and lists those with no matching
      rule — read-only, no asset moves performed.
    - "Copy to Clipboard" button exports the violation list as newline-separated paths.
    - `Editor/View/NamingValidatorView.cs`.
  - **Double-apply protection.** `OnPreprocessAsset` now checks `AssetsBeingMoved.Contains(assetPath)`
    and returns early if the asset is currently being moved by the postprocessor. Prevents the preset
    from being applied a second time on the re-import that Unity triggers after `MoveAsset`.

### Changed
- `AssetRouterPostprocessor.OnPostprocessAllAssets`: collected rule-name list is flushed to
  `RuleStatsStore.IncrementBatch` once per import batch (not per asset). Emits to `DiagnosticLog`
  when the window is open. *(DiagnosticLog removed in a later release — see [Unreleased].)*
- Asset Router window: `TabLabels` extended from 3 to 4 entries ("Validate" added). Stats cache
  loaded from `RuleStatsStore` when a database is loaded.
- `package.json` version bumped to `0.8.0`.

---

## [0.7.0] — 2026-06-24

### Added
- **Action library showcase spectrum.**
  - `Runtime/AssetRouter.Runtime.asmdef` — new Runtime assembly (no Editor constraint, `autoReferenced: true`). User MonoBehaviours and ScriptableObjects can now implement callback interfaces from outside the Editor assembly.
  - `IAssetRouterPrefabSetup` (Runtime) — `void SetupAssetRouter(Object importedAsset, string assetPath)` callback invoked by `CreatePrefabFromTemplateAction` after the prefab instance is created.
  - `IAssetRouterDataSetup` (Runtime) — same pattern for `CreateScriptableObjectFromTemplateAction`.
  - `Editor/AssetRouter.Editor.asmdef` now references `AssetRouter.Runtime`.
  - **Seven new built-in actions** covering architectural tiers D–G:
    - `EmitUnityEventAction` (Tier D) — fires a serialized `UnityEvent<Object>` (Inspector-configurable, no code required).
    - `CreatePrefabFromTemplateAction` (Tier E ⭐) — instantiates a template prefab, calls `IAssetRouterPrefabSetup.SetupAssetRouter` on any component that implements it, then saves the result as a new prefab asset.
    - `CreateScriptableObjectFromTemplateAction` (Tier E) — clones a template ScriptableObject, calls `IAssetRouterDataSetup.SetupAssetRouter`, then saves as a new `.asset`.
    - `CreateMaterialFromTextureAction` (Tier E) — creates a new Material from a base material, assigns the imported texture to a configurable property, saves as `.mat`.
    - `GenerateNineSliceBordersAction` (Tier F) — scans transparent borders of the sprite texture and writes `TextureImporter.spriteBorder` automatically. Requires Read/Write enabled.
    - `GenerateSpritePhysicsShapeAction` (Tier F) — derives a tight bounding polygon from pixel alpha and applies it via `Sprite.OverridePhysicsShape`. Requires Read/Write enabled.
    - `CreateTilePaletteEntryAction` (Tier G) — creates a `UnityEngine.Tilemaps.Tile` asset from an imported sprite and saves it to the configured output folder.
  - `Editor/Wizard/ActionScaffoldingWizard.cs` — four `Assets/Create/Asset Router/New Action.../` menu items that generate ready-to-compile action templates (Basic, Texture Filter, Sprite Factory, Prefab Factory) using `EditorUtility.SaveFilePanelInProject`.
  - `Samples~/LegacyActions/` — new sample containing `GenerateMeshColliderAction` and `RunMenuItemAction`, removed from the core package (see below). Includes `README.md` explaining the rationale and a standalone `LegacyActions.asmdef`.
  - `NewActionsTests.cs` — 15 new edit-mode tests: `CanRunOn` null-guard tests for all five new CanRunOn-testable actions + six pixel-analysis unit tests for `GenerateNineSliceBordersAction.ComputeBorder` (extracted as `internal static` for testability).
  - `package.json` `samples` array updated with the new "Legacy Actions" entry.
- **Test coverage closure.**
  - `PathUtilityTests.cs` — 9 tests: `NormalizeAssetPath` (null, backslashes, trailing slash), `IsUnderFolder` (prefix-collision regression for `Plugins` vs `PluginsCustom`, case-insensitive), `ToAbsolute` (double-"Assets" regression).
  - `TrimAudioSilenceActionTests.cs` — 14 tests: leading silence, trailing silence, both ends, all-silence, no-silence, malformed RIFF, RIFX big-endian rejection, `short.MinValue` overflow guard, output RIFF integrity.
  - `RuleValidatorTests.cs` — 7 additional tests: `ShouldProcess` with null db, null path, empty path, no extension, `PluginsCustom` prefix-collision; `FindMatchingRule` with null list, empty list, null entry in list.

### Removed
- `GenerateMeshColliderAction` — moved to `Samples~/LegacyActions/` (3D-only; replaced by the Tier E factory actions for the target 2D use case).
- `RunMenuItemAction` — moved to `Samples~/LegacyActions/` (superseded by `EmitUnityEventAction`).

---

## [0.6.0] — 2026-06-21

### Changed
- `PathUtility`: added `EnsureFolderExists` as the single canonical implementation. Removed the three
  local copies that previously existed in `BatchMover`, `UndoEngine`, and `AssetRouterPostprocessor`.
- `OperationLog.LogPath`: converted from a side-effecting property (called `Directory.CreateDirectory`
  on every read) to a pure computed property. `Directory.CreateDirectory` is now called only in
  `WriteLogFile`, where it is actually needed.
- `AssetRouterWindow` split into two `partial` files: `AssetRouterWindow.cs` (coordinator — fields,
  tabs, toolbar, rules list, settings) and `AssetRouterWindow.RuleDetail.cs` (rule detail panel,
  actions list, pattern preview). No behaviour changes.
- Removed unnecessary comments across the entire codebase. Names self-document; only genuinely
  non-obvious Unity quirks and ordering constraints were kept.

### Fixed
- **Critical bugs & production hardening.**
  - `AssetRouterPostprocessor`: `AssemblyReloadEvents.beforeAssemblyReload` lambda accumulated new
    subscriptions on every reload when Domain Reload is disabled. Replaced with named static
    `ClearGuards()` method and unsubscribe-first pattern.
  - `AssetRouterPostprocessor`: moves were executed one-by-one without `StartAssetEditing /
    StopAssetEditing`, causing O(N²) reimports on large imports. Replaced with `ExecuteMovesBatched`
    that wraps all moves in a single editing block.
  - `AssetRouterPostprocessor`: only the target path was added to `AssetsBeingMoved`; source path was
    unprotected. Both paths are now added before `MoveAsset`. Source paths are removed after the
    editing block so they do not accumulate with Domain Reload disabled.
  - `AssetRouterInitializer`: added `EditorApplication.projectChanged` subscription so the migrator
    also runs when a database is created or modified while the editor is open.
  - `OperationLog`: write was `File.Delete` + `File.Move` (data-loss window on crash). Replaced with
    `File.Replace` (atomic on NTFS). Added 500-session cap, `Clear()` method, and corruption recovery
    (saves `.corrupt` backup and logs a warning instead of silently discarding history).
  - `JsonExporter`: same non-atomic write pattern fixed with `File.Replace`.
  - `JsonImporter`: `RuleMigrator.MigrateIfNeeded` now called at the end of `Import()` so
    legacy-schema JSON is upgraded immediately. Null/empty extension strings are filtered out.
    Portability note added to XMLDoc (sub-asset `fileId` values are project-local).
  - `TrimAudioSilenceAction`: `(short)(threshold * short.MaxValue)` + `Math.Abs(sample)` overflowed
    for `short.MinValue`. Replaced with `int thresholdSample` and `Math.Abs((int)sample)`. RIFF
    bounds check now uses `(long)` cast to avoid int overflow for large malformed chunks. Added
    re-entrancy guard (`HashSet<string> _processing`) to prevent infinite reimport loops. WAV write
    is now atomic (`File.Replace`).
  - `RegisterAddressableAction`: removed `AssetDatabase.SaveAssets()` after each asset — batch
    catastrophe. Only `EditorUtility.SetDirty(settings)` is called; save is deferred.
  - `AssetRouterWindow` (rule removal): rule removal now captures `postImportActions` first, removes
    the rule from the array and calls `ApplyModifiedProperties`, then destroys orphan sub-assets.
    Previously `IsReferencedByOtherRule` was called before the rule was removed, so it always
    returned `true` and no sub-assets were ever cleaned up.
  - `AssetRouterWindow` (action removal): `DestroyImmediate` on a sub-asset now checks whether any
    other rule in the database references it before destroying, preventing silent null references.
  - `AssetRouterWindow`: `AssetDatabase.SaveAssets()` was called after every action addition. Now
    deferred to the "Save / Apply" button; only `EditorUtility.SetDirty` is called immediately.
  - `AssetRouterWindow.BuildPatternPreview`: `AssetDatabase.FindAssets` was called synchronously on
    every keystroke. Added 300 ms debounce via `EditorApplication.timeSinceStartup`.
  - `UndoEngine`: `EnsureFolderExists` was called inside `StartAssetEditing`, causing `MoveAsset` to
    fail (folder creation is deferred until `StopAssetEditing`). Moved to a pre-pass. Added
    `DisplayCancelableProgressBar` and a post-revert summary dialog.
  - `UndoEngine`: source-path guard removed after fix — folder creation pre-pass refactored cleanly.
  - `BatchMover`: `EnsureFolderExists` moved before `StartAssetEditing` (same fix as UndoEngine).
    Separate `Reimported` counter added to `BatchResult` (was folded into `Moved`). Progress
    increment is now consistent across all branches.
  - `DryRunPlanner`: added `DisplayCancelableProgressBar` with cancel support and `try/finally`
    cleanup.
  - `ConflictDetector`: overlap heuristic now uses up to 100 real project assets in addition to 14
    fixed sample paths. "Heuristic — false negatives are possible" caveat added to UI banner and
    XMLDoc.

### Added
- `HistoryView`: "Clear History" button with confirmation dialog. Calls `OperationLog.Clear()` and
  resets the session list.
- `PatternMatcherTests`: `Glob_DoubleStar_Alone_MatchesDirectChildToo` — documents that `Assets/**`
  with `matchAgainstFullPath=true` matches direct children such as `Assets/x.png`.
- UI tooltips on the Pattern and Match Full Path fields explain that path-based patterns
  (e.g. `Assets/**`) require `matchAgainstFullPath` to be enabled.

---

## [0.5.0]

### Added
- **Git-friendly JSON export/import.**
  - `Export JSON` and `Import JSON` buttons added to the Asset Router window toolbar.
  - `JsonExporter.ExportToFile` serialises the full database (settings, extensions, ignored folders,
    all rules with their pattern, target, preset GUID, and postImportActions sub-asset refs) to an
    indented JSON file. Atomic write via `.tmp` + rename prevents corruption on crash.
  - `JsonImporter.ImportFromFile` parses the JSON and restores the database in-place, resolving preset
    and postImportAction GUIDs back to Unity assets via `AssetDatabase`.
  - Depends on `com.unity.nuget.newtonsoft-json 3.2.1` (added to `package.json` dependencies).
  - New tests: `JsonRoundTripTests` (7 cases covering general settings, extensions, rule fields,
    ordering, and Regex mode preservation).
- **Bundled presets + sample.**
  - 6 new import presets (minimal, only override differentiating properties):
    - `TextureImporter_Sprite` — Sprite type, Single mode, tight mesh, alpha transparency
    - `TextureImporter_Lightmap` — Lightmap type, linear, 4096 px max
    - `TextureImporter_NormalMap` — NormalMap type, linear
    - `ModelImporter_Static` — no rig, no animation, no auto-collider
    - `ModelImporter_Character` — Humanoid rig, import animations, optimise game objects
    - `AudioImporter_Voice` — CompressedInMemory, mono, 22 050 Hz, Vorbis
  - `Samples~/QuickStart/README.md` updated with a complete step-by-step tutorial covering
    drag-and-drop routing, Dry Run, History/Undo, JSON export, and all 10 bundled presets.

---

## [0.4.0]

### Added
- **Dry-run preview.** New "Dry Run" tab in the Asset Router window.
  - `DryRunPlanner` scans the project and builds a list of routing candidates without moving anything.
  - Table shows: file name, current folder, target folder, matched rule. Entries are pre-checked for actionable moves.
  - "Apply Selected" moves checked entries in a single `StartAssetEditing` batch.
  - "Select All / None" toggles for bulk selection.
  - "Show unmatched" toggle includes files with no matching rule in the table.
  - "Force re-import" toggle re-applies preset + actions to assets already in the correct folder.
- **Batch re-import.** "Re-import All Matched" button in the Dry Run tab scans and applies all
  matched assets in one click without showing the table first.
  - Cancellable progress bar via `EditorUtility.DisplayCancelableProgressBar`.
  - All moves executed inside `StartAssetEditing / StopAssetEditing` for up to 100× speedup on large sets.
  - Console summary: `Moved: X, Skipped: Y, Errored: Z`.
- **Operation Log + Undo.** New "History" tab in the Asset Router window.
  - Every batch of moves (auto-import and batch re-import) is recorded to `Library/AssetRouter/log.json`
    (JSON, versioned `{"v":1,...}`, atomic write via `.tmp` + rename).
  - History tab lists past sessions (timestamp, source, move count); click a session to see individual moves.
  - "Undo Selected Session" calls `UndoEngine.Revert`, which moves assets back in reverse order inside
    a single `StartAssetEditing` batch. Best-effort: assets no longer at the recorded path are skipped
    with a warning.
- New tests: `DryRunPlannerTests` (7 cases), `OperationLogTests` (5 cases).

---

## [0.3.0]

### Added
- **Pluggable Import Actions.** Each `ImportRule` now has a `List<AssetImportActionAsset> postImportActions`
  that runs after the asset is moved and reimported at its target path.
  - `IAssetImportAction` interface + `AssetImportActionAsset` abstract ScriptableObject base class.
  - `AssetImportContext` readonly struct passed to every action (`AssetPath`, `Rule`, `Database`, `Logger`).
  - `ActionPipeline` internal executor: iterates actions, calls `CanRunOn` → `Execute`, wraps each in
    `try/catch` so a failing action never blocks the rest of the chain.
  - Six built-in actions in `Editor/Actions/BuiltIn/`:
    - `SetPivotAction` — sets sprite pivot via `TextureImporter` and reimports (idempotent).
    - `GenerateMeshColliderAction` — enables `ModelImporter.addCollider` and reimports (idempotent).
    - `TrimAudioSilenceAction` — trims leading/trailing silence from 16-bit PCM WAV files via a
      custom RIFF parser; writes atomically and reimports (idempotent).
    - `RegisterAddressableAction` — registers the asset in an Addressables group
      (`#if UNITY_ADDRESSABLES`; compiled only when `com.unity.addressables >= 1.19.0` is installed).
    - `AppendToCatalogAction` — appends the imported asset to an `AssetCatalog` SO (idempotent).
    - `RunMenuItemAction` — calls `EditorApplication.ExecuteMenuItem` with a configurable path.
  - `AssetCatalog` ScriptableObject (`Create > Asset Router > Asset Catalog`).
  - Actions ReorderableList in the rule detail panel: drag-to-reorder, `+` dropdown (TypeCache),
    remove button that also cleans up embedded sub-assets.
  - `AssetRouter.Editor.asmdef` `versionDefines` entry for `com.unity.addressables` → `UNITY_ADDRESSABLES`.
- New tests: `ActionPipelineTests` (6 cases covering execute, skip, error isolation, null safety).

---

## [0.2.0]

### Added
- **Pattern matching.** `BaseImportRule` now has a single `pattern` field (glob or regex)
  and a `PatternMode` enum, replacing the old `prefix` / `suffix` / `extensionFilter` triplet.
  - `PatternMatcher` static class: glob→regex compiler, per-rule compiled `Regex` cache,
    50 ms timeout guard against ReDoS.
  - `RuleMigrator`: one-shot migration from schema v1 (prefix/suffix/extension) to v2 (pattern).
    Runs automatically on Editor startup via `AssetRouterInitializer`.
  - `schemaVersion` field on `ImporterSettingsDatabase` (`LatestSchemaVersion = 2`).
  - Legacy fields (`_legacyPrefix`, `_legacySuffix`, `_legacyExtensionFilter`) preserved with
    `[FormerlySerializedAs]` so old `.asset` files survive the upgrade without data loss.
  - Live pattern preview in the rule detail panel: shows up to 3 matching filenames from the
    project, or a red error message for invalid regex syntax.
- **Conflict detection.** `ConflictDetector` finds duplicate and overlapping rules.
  - Duplicate: identical `pattern` + `patternMode` + `matchAgainstFullPath`.
  - Overlap: heuristic using a fixed set of representative asset paths.
  - Warning banner in the editor window when conflicts exist.
  - `⚠` prefix on conflicting rules in the `ReorderableList`.
- New tests: `PatternMatcherTests`, `RuleMigratorTests`, `ConflictDetectorTests`.

### Changed
- `RuleValidator.FindMatchingRule` simplified: delegates matching to `PatternMatcher`.
- Default rules in `DefaultDatabaseFactory` use the new `pattern` field (`"T_*"`, `"UI_*"`, …).
- `RuleValidatorTests` updated to use `pattern` instead of the removed `prefix`/`suffix`/`extensionFilter`.

---

## [0.1.0]

### Added
- `PathUtility` static class with `NormalizeAssetPath`, `ToAbsolute`, and `IsUnderFolder` helpers — centralises all path handling.
- `DefaultDatabaseFactory` static class — single source of truth for default rules, monitored extensions and ignored folders. Eliminates the previous duplication between `ImporterSettingsDatabase.Reset()` and `AssetRouterInitializer`.
- `[InitializeOnLoadMethod]` / `[InitializeOnEnterPlayMode]` hooks on `AssetRouterPostprocessor` and `DatabaseLocator` to clear static state when domain reload is disabled.
- `EditorApplication.projectChanged` subscription in `AssetRouterWindow` so the window reacts when a database is created externally while the window is open.

### Fixed
- **Bug:** `HandleUnknownAsset` used `Application.dataPath.Replace("Assets", "")` which would corrupt any path that contained the word "Assets" more than once. Now uses `Path.GetDirectoryName(Application.dataPath)` via `PathUtility.ToAbsolute`.
- **Bug:** `CreateNewDatabase()` in `AssetRouterWindow` created a blank `ImporterSettingsDatabase` without calling `Reset()`, leaving all lists empty. Now calls `DefaultDatabaseFactory.PopulateDefaults(db)` explicitly.
- **Bug:** Default `monitoredExtensions` list differed between `Reset()` and `AssetRouterInitializer` (e.g. `.dae`, `.tga`, `.psd` were missing from the initialiser). Both now use `DefaultDatabaseFactory` with a single canonical list.

### Changed
- `AssetRouterPostprocessor`, `AssetRouterInitializer`, `AssetRouterWindow`: access changed from `public` to `internal sealed` / `internal static` — not part of the public API.
- `RuleValidator`, `DatabaseLocator`: access changed from `public static` to `internal static`.
- Path comparisons in `RuleValidator.ShouldProcess` now go through `PathUtility.IsUnderFolder`, which normalises separators before comparison — fixes silent mismatches on Windows when paths contain back-slashes.

---

## [0.0.1] — 2026-06-20

### Added
- Initial release.
- `AssetPostprocessor`-based auto-routing: applies Unity Preset and moves imported assets to the target folder based on configurable naming rules (prefix / suffix / extension filter).
- `ImporterSettingsDatabase` ScriptableObject for storing rules, monitored extensions, and ignored folders.
- Four default rules: UI Textures (`UI_`), General Textures (`T_`), Sound Effects (`SFX_`), Music (`Mus_`).
- Bundled import presets: `TextureImporter`, `TextureImporter_UI`, `AudioImporter`, `AudioImporter_Music`.
- Editor window via `Tools > Asset Router Settings` — reorderable rules list, detail panel, Save/Apply button.
- Dialog for files that match no rule ("Import as-is" / "Delete file").
- NUnit edit-mode tests for `RuleValidator`.
