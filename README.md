# Asset Router

Unity Editor plugin that automatically moves imported assets to configured folders and applies
import presets based on naming rules. Drop a file into the project and it lands in the right place.

**Namespace:** `Kodlon.AssetRouter`
**Unity:** 2022.3 LTS or later
**License:** MIT

---

## Installation

### Via Unity Package Manager (recommended)

1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL**
3. Paste:
```
https://github.com/kodlon/AssetRouter.git
```

### Manual

Copy the package folder into your project's `Packages/` directory.

---

## Quick start

1. On first Editor load, Asset Router creates `Assets/AssetRouter/ImporterSettingsDatabase.asset`
   with six default rules.
2. Open **Tools > Asset Router > Settings** to view and edit rules.
3. Drop an asset into the project. If the file name matches a rule pattern, the preset is applied
   and the file is moved to the target folder automatically.

---

## How it works

Each rule defines:
- **Pattern** (glob or regex) — matched against the file name or full asset path
- **Target Folder** — where to move the file. Supports `{1}`, `{name}` tokens that expand to capture group values (see [Path Templating](Documentation~/api/path-templating.md)).
- **Import Preset** — Unity `.preset` file applied on import
- **Post-Import Actions** — optional chain of actions that run after the move

First matching rule wins. Rules are reorderable via drag-and-drop.

---

## Default rules

| Pattern | Mode | Target folder | Preset | Post-import |
|---------|------|--------------|--------|-------------|
| `UI_*` | Glob | `Assets/Art/UI/` | TextureImporter_UI | — |
| `T_Char_*_*` | Glob | `Assets/Art/Characters/{1}/` | TextureImporter | — |
| `^T_Loc_(?<loc>\w+)_.*` | Regex | `Assets/Art/Locations/{loc}/` | TextureImporter | — |
| `T_*` | Glob | `Assets/Art/Textures/` | TextureImporter | Create Material From Texture |
| `SFX_*` | Glob | `Assets/Audio/SFX/` | AudioImporter | — |
| `Mus_*` | Glob | `Assets/Audio/Music/` | AudioImporter_Music | — |

---

## Built-in actions

11 built-in actions cover a range of automation patterns:

| Action | What it does |
|--------|-------------|
| SetPivotAction | Sets sprite pivot on import |
| TrimAudioSilenceAction | Trims silence from 16-bit PCM WAV files |
| AppendToCatalogAction | Adds asset to an AssetCatalog ScriptableObject |
| RegisterAddressableAction | Registers asset in an Addressables group |
| EmitUnityEventAction | Fires a UnityEvent configured in the Inspector |
| CreatePrefabFromTemplateAction | Creates a prefab from a template with a setup callback |
| CreateScriptableObjectFromTemplateAction | Creates a ScriptableObject from a template |
| CreateMaterialFromTextureAction | Creates a material and assigns the texture |
| GenerateSpritePhysicsShapeAction | Derives physics shape from pixel alpha |
| GenerateNineSliceBordersAction | Sets 9-slice borders from transparent edges |
| CreateTilePaletteEntryAction | Creates a Tile asset for Tilemap |

---

## Structure

```
├── Editor/
│   ├── Actions/    # IAssetImportAction, AssetImportActionAsset, 11 built-in actions
│   ├── Data/       # BaseImportRule, ImportRule, ImporterSettingsDatabase
│   ├── Logic/      # PatternMatcher, RuleValidator, Postprocessor, Initializer, DryRunPlanner
│   ├── View/       # AssetRouterWindow, DryRunView, HistoryView, NamingValidatorView, WelcomeWindow
│   └── Wizard/     # ActionScaffoldingWizard (code generator)
├── Runtime/        # AssetCatalog, IAssetRouterPrefabSetup, IAssetRouterDataSetup
├── Presets/        # 10 bundled .preset files
├── Tests/          # NUnit edit-mode tests
├── Samples~/
│   ├── QuickStart/ # Written walkthrough of the auto-import flow
│   └── LegacyActions/ # GenerateMeshColliderAction, RunMenuItemAction
└── Documentation~/
```

## Documentation

Full documentation is in [`Documentation~/DOCUMENTATION_EN.md`](Documentation~/DOCUMENTATION_EN.md).

- [Getting Started](Documentation~/getting-started.md) — install, first import, first rule in 10 minutes
- [UI Reference](Documentation~/ui-reference.md) — every window, tab, and button
- [Pattern Cookbook](Documentation~/patterns.md) — Glob and Regex recipes, common mistakes, how to test
- [Presets](Documentation~/presets.md) — creating your own import presets
- [Rule Sharing](Documentation~/rule-sharing.md) — copy a single rule between projects via clipboard
- [Feature Catalog](Documentation~/features.md) — every feature with description and example
- [Full Reference](Documentation~/DOCUMENTATION_EN.md)
- [Built-in Actions Reference](Documentation~/actions/README.md)
- [Writing Your Own Action](Documentation~/api/extension-points.md)
- [Changelog](CHANGELOG.md)
