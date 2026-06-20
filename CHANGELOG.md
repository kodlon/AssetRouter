# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased] — v0.2.0

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

## [Unreleased] — v0.1.0

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
