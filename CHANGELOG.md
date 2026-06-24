# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.7.0] — 2026-06-24

### Added
- **Epic 15 — Action library showcase spectrum.**
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
- **Epic 13 — Test coverage closure** (uncommitted from v0.6.0).
  - `PathUtilityTests.cs` — 9 tests: `NormalizeAssetPath` (null, backslashes, trailing slash), `IsUnderFolder` (prefix-collision regression for `Plugins` vs `PluginsCustom`, case-insensitive), `ToAbsolute` (double-"Assets" regression).
  - `TrimAudioSilenceActionTests.cs` — 14 tests: leading silence, trailing silence, both ends, all-silence, no-silence, malformed RIFF, RIFX big-endian rejection, `short.MinValue` overflow guard, output RIFF integrity.
  - `RuleValidatorTests.cs` — 7 additional tests: `ShouldProcess` with null db, null path, empty path, no extension, `PluginsCustom` prefix-collision; `FindMatchingRule` with null list, empty list, null entry in list.
- `Documentation~/TEST.md` — manual smoke-test checklist (~90 min) covering install, routing, dry-run, history/undo, JSON, and all new actions.

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
- **Epic 11 — Critical bugs & production hardening.**
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
- **Epic 7 — Git-friendly JSON export/import.**
  - `Export JSON` and `Import JSON` buttons added to the Asset Router window toolbar.
  - `JsonExporter.ExportToFile` serialises the full database (settings, extensions, ignored folders,
    all rules with their pattern, target, preset GUID, and postImportActions sub-asset refs) to an
    indented JSON file. Atomic write via `.tmp` + rename prevents corruption on crash.
  - `JsonImporter.ImportFromFile` parses the JSON and restores the database in-place, resolving preset
    and postImportAction GUIDs back to Unity assets via `AssetDatabase`.
  - Depends on `com.unity.nuget.newtonsoft-json 3.2.1` (added to `package.json` dependencies).
  - New tests: `JsonRoundTripTests` (7 cases covering general settings, extensions, rule fields,
    ordering, and Regex mode preservation).
- **Epic 8 — Bundled presets + sample.**
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
- **Epic 3 — Dry-run preview.** New "Dry Run" tab in the Asset Router window.
  - `DryRunPlanner` scans the project and builds a list of routing candidates without moving anything.
  - Table shows: file name, current folder, target folder, matched rule. Entries are pre-checked for actionable moves.
  - "Apply Selected" moves checked entries in a single `StartAssetEditing` batch.
  - "Select All / None" toggles for bulk selection.
  - "Show unmatched" toggle includes files with no matching rule in the table.
  - "Force re-import" toggle re-applies preset + actions to assets already in the correct folder.
- **Epic 4 — Batch re-import.** "Re-import All Matched" button in the Dry Run tab scans and applies all
  matched assets in one click without showing the table first.
  - Cancellable progress bar via `EditorUtility.DisplayCancelableProgressBar`.
  - All moves executed inside `StartAssetEditing / StopAssetEditing` for up to 100× speedup on large sets.
  - Console summary: `Moved: X, Skipped: Y, Errored: Z`.
- **Epic 6 — Operation Log + Undo.** New "History" tab in the Asset Router window.
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
- **Epic 2 — Pluggable Import Actions.** Each `ImportRule` now has a `List<AssetImportActionAsset> postImportActions`
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
- **Epic 1 — Pattern matching.** `BaseImportRule` now has a single `pattern` field (glob or regex)
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
- **Epic 5 — Conflict detection.** `ConflictDetector` finds duplicate and overlapping rules.
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
