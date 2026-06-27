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
   with four default rules.
2. Open **Tools > Asset Router Settings** to view and edit rules.
3. Drop an asset into the project. If the file name matches a rule pattern, the preset is applied
   and the file is moved to the target folder automatically.

---

## How it works

Each rule defines:
- **Pattern** (glob or regex) — matched against the file name or full asset path
- **Target Folder** — where to move the file
- **Import Preset** — Unity `.preset` file applied on import
- **Post-Import Actions** — optional chain of actions that run after the move

First matching rule wins. Rules are reorderable via drag-and-drop.

---

## Default rules

| Pattern (Glob) | Target folder | Preset |
|----------------|--------------|--------|
| `UI_*` | `Assets/Art/UI/` | TextureImporter_UI |
| `T_*` | `Assets/Art/Textures/` | TextureImporter |
| `SFX_*` | `Assets/Audio/SFX/` | AudioImporter |
| `Mus_*` | `Assets/Audio/Music/` | AudioImporter_Music |

---

## Built-in actions

10 built-in actions cover a range of automation patterns:

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
│   ├── Actions/    # IAssetImportAction, AssetImportActionAsset, 10 built-in actions
│   ├── Data/       # BaseImportRule, ImportRule, ImporterSettingsDatabase, AssetCatalog
│   ├── Logic/      # PatternMatcher, RuleValidator, Postprocessor, Initializer, DryRunPlanner
│   ├── View/       # AssetRouterWindow, DryRunView, HistoryView, NamingValidatorView, DiagnosticWindow
│   └── Wizard/     # ActionScaffoldingWizard (code generator)
├── Runtime/        # IAssetRouterPrefabSetup, IAssetRouterDataSetup
├── Presets/        # 10 bundled .preset files
├── Tests/          # NUnit edit-mode tests (12 test files)
├── Samples~/
│   ├── QuickStart/ # Tutorial sample with example files
│   └── LegacyActions/ # GenerateMeshColliderAction, RunMenuItemAction
└── Documentation~/
```

## Documentation

Full documentation is in [`Documentation~/DOCUMENTATION_EN.md`](Documentation~/DOCUMENTATION_EN.md).

- [Getting Started and Full Reference](Documentation~/DOCUMENTATION_EN.md)
- [Built-in Actions Reference](Documentation~/actions/README.md)
- [Writing Your Own Action](Documentation~/api/extension-points.md)
- [Changelog](CHANGELOG.md)
