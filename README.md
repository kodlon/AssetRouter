# AssetRouter

Unity Editor plugin that automatically applies import presets and moves assets to target folders based on naming rules (prefix, suffix, extension). Zero setup — works out of the box.

**Namespace:** `Kodlon.AssetRouter`
**Unity:** 2022.3 LTS+

---

## Installation

### Via Unity Package Manager (recommended)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL**
3. Paste:
```
https://github.com/kodlon/AssetRouter.git
```

### Manual

Copy the repo contents into your project's `Assets/` folder.

---

## Quick start

1. On first Editor load the plugin auto-creates `Assets/AssetRouter/ImporterSettingsDatabase.asset` with four default rules.
2. Open **Tools → Asset Router Settings** to view and edit rules.
3. Drop an asset into your project — the plugin applies the matching preset and moves the file to the target folder automatically.

---

## How it works

Each rule defines:
- **Prefix / Suffix / Extension** — matching conditions (all must pass)
- **Target Folder** — where to move the file
- **Import Preset** — Unity `.preset` file with importer settings

First matching rule wins. Rules are reorderable via drag-and-drop.

---

## Default rules

| Prefix | Target folder | Preset |
|--------|--------------|--------|
| `UI_` | `Assets/Art/UI/` | TextureImporter_UI |
| `T_` | `Assets/Art/Textures/` | TextureImporter |
| `SFX_` | `Assets/Audio/SFX/` | AudioImporter |
| `Mus_` | `Assets/Audio/Music/` | AudioImporter_Music |

---

## Structure

```
├── Editor/
│   ├── Data/       # BaseImportRule, ImportRule, ImporterSettingsDatabase
│   ├── Logic/      # RuleValidator, DatabaseLocator, Postprocessor, Initializer
│   └── View/       # AssetRouterWindow (EditorWindow)
├── Presets/        # Bundled .preset files
├── Tests/          # NUnit edit-mode tests
├── Documentation~/
└── package.json
```

## License

MIT
