# Quick Start — Asset Router

This sample demonstrates Asset Router's auto-import and routing flow.

## What's included

- Pre-configured `ImporterSettingsDatabase.asset` with 4 default rules
- `Raw/` folder with example files: `T_Rock_D.png`, `UI_Button.png`, `SFX_Click.wav`, `Mus_Loop.ogg`, `qwerty.png`
- Links to the 10 bundled presets in `Packages/com.kodlon.assetrouter/Presets/`

## How to import

1. Open **Window → Package Manager**
2. Select **Asset Router** in the list
3. Expand the **Samples** section
4. Click **Import** next to *Quick Start*

Unity copies the sample into `Assets/Samples/Asset Router/<version>/Quick Start/`.

---

## Step-by-step tutorial

### Step 1 — Open the settings window

Go to **Tools → Asset Router Settings**.  
The window shows the pre-configured database with four rules:

| Rule | Pattern | Target folder |
|------|---------|---------------|
| General Textures | `T_*` | `Assets/Art/Textures` |
| UI Textures | `UI_*` | `Assets/Art/UI` |
| Sound Effects | `SFX_*` | `Assets/Audio/SFX` |
| Music | `Mus_*` | `Assets/Audio/Music` |

### Step 2 — Drop a file

Drag any file from the sample's `Raw/` folder into the **Project window root** (`Assets/`).

- `T_Rock_D.png` → moved to `Assets/Art/Textures/`, `TextureImporter` preset applied
- `UI_Button.png` → moved to `Assets/Art/UI/`, `TextureImporter_UI` preset applied
- `SFX_Click.wav` → moved to `Assets/Audio/SFX/`, `AudioImporter` preset applied
- `Mus_Loop.ogg` → moved to `Assets/Audio/Music/`, `AudioImporter_Music` preset applied
- `qwerty.png` → no match → a dialog appears asking "Import as-is" or "Delete"

### Step 3 — Use Dry Run

Switch to the **Dry Run** tab and click **Scan Project**.  
A table shows which files will move and where.  
Select entries and click **Apply Selected** to batch-move them.

### Step 4 — Undo if needed

Switch to the **History** tab.  
Each batch of moves is recorded here.  
Select a session and click **Undo Selected Session** to revert all moves in that session.

### Step 5 — Export / Import as JSON

Open **Tools → Asset Router Settings**.  
Click **Export JSON** in the toolbar to save the database as a human-readable `.json` file.  
This file is git-diff-friendly and can be committed to version control.  
Use **Import JSON** on another machine to restore the same configuration.

---

## Bundled presets (v0.5.0)

All presets live in `Packages/com.kodlon.assetrouter/Presets/`. Assign them to rules in the
Settings tab via the **Import Preset** field.

| Preset | Best for |
|--------|----------|
| `TextureImporter` | General diffuse textures (2048 px, mipmaps, sRGB) |
| `TextureImporter_UI` | UI sprites (1024 px, no mipmaps, sRGB) |
| `TextureImporter_Sprite` | Gameplay sprites (Sprite mode, tight mesh, alpha transparency) |
| `TextureImporter_Lightmap` | Baked lightmaps (4096 px, linear, no sRGB) |
| `TextureImporter_NormalMap` | Normal maps (NormalMap type, linear) |
| `AudioImporter` | General SFX (Vorbis, decompress on load) |
| `AudioImporter_Music` | Background music (Vorbis, streaming) |
| `AudioImporter_Voice` | Voice-over (CompressedInMemory, mono, 22 050 Hz) |
| `ModelImporter_Static` | Env props (no rig, no animation) |
| `ModelImporter_Character` | Characters (Humanoid rig, animations, optimise game objects) |

---

## Adding post-import actions

Select a rule in the Settings tab and scroll to **Post-Import Actions**.  
Click **+** to choose a built-in action:

- **Set Pivot Action** — centers the sprite pivot after import
- **Generate Mesh Collider Action** — enables `addCollider` on FBX models
- **Trim Audio Silence Action** — strips leading/trailing silence from 16-bit PCM WAV files
- **Append To Catalog Action** — adds the asset to an `AssetCatalog` ScriptableObject
- **Run Menu Item Action** — calls any editor menu item after import

Actions run in order. A failing action never blocks the ones after it.
