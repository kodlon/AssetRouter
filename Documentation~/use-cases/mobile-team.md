# Mobile team setup

This page describes a configuration for a small mobile 2D team: 1 tech artist, 2 artists, and
1 programmer. The project uses Unity 2022.3 LTS with URP.

## The problem this solves

Artists export files from Photoshop and Spine and drop them into `Assets/Import/`. Without routing,
the programmer sorts them manually before each build. With Asset Router, files land in the right
folder with the right import settings the moment they hit the project.

## Rule setup

### UI sprites

Pattern `UI_*`, target `Assets/Art/UI/`, preset `TextureImporter_Sprite` (Sprite type, tight mesh,
alpha transparency).

Add `SetPivotAction` with pivot (0.5, 0.5). All UI sprites get center pivot on import.

Add `RegisterAddressableAction` with group name `"UI"` if the team uses Addressables for
UI atlas loading.

### Character sprites

Pattern `Char_*`, target `Assets/Art/Characters/`, preset `TextureImporter_Sprite`.

Add `GenerateSpritePhysicsShapeAction` with threshold 0.05.

### Environment textures

Pattern `Env_*`, target `Assets/Art/Environment/`, preset `TextureImporter` (General type, BC7 compression).

Add `CreateMaterialFromTextureAction` with base material `EnvTilingBase` and property `_BaseMap`.
Every environment texture gets a matching material automatically.

### Sound effects

Pattern `SFX_*`, target `Assets/Audio/SFX/`, preset `AudioImporter`.

Add `TrimAudioSilenceAction` with threshold 0.01. Sound designers export from a DAW that adds
200 ms of dead air at the start; the action strips it on every import.

### Music

Pattern `Mus_*`, target `Assets/Audio/Music/`, preset `AudioImporter_Music`.

### Normal maps

Pattern `NM_*`, target `Assets/Art/NormalMaps/`, preset `TextureImporter_NormalMap`.

## Scope folder for two artists

Artist A drops files into `Assets/Import/ArtistA/`, Artist B into `Assets/Import/ArtistB/`.
Both use the same naming convention but need files in different subfolders (they each own separate
level areas).

For environment textures:

- Rule "Env textures A": pattern `Env_*`, scopeFolder `Assets/Import/ArtistA/`, target `Assets/Art/Environment/LevelA/`
- Rule "Env textures B": pattern `Env_*`, scopeFolder `Assets/Import/ArtistB/`, target `Assets/Art/Environment/LevelB/`
- Rule "Env textures default": pattern `Env_*`, no scope, target `Assets/Art/Environment/`

Put the scoped rules above the fallback in the list.

## JSON export for version control

The team keeps `database.json` in git. Workflow:

1. Tech artist makes changes in the Settings window, clicks Export JSON, commits `database.json`.
2. Other team members pull, open Settings, click Import JSON.
3. The database `.asset` is not committed (add it to `.gitignore`).

This gives readable diffs when patterns or target folders change.

## Monitored extensions

Add `.spine` if the team exports Spine skeleton files that need routing.
The default list covers `.png`, `.jpg`, `.tga`, `.psd`, `.wav`, `.ogg`, `.mp3`.

## Ignored folders

Keep `Assets/Import/` out of ignored folders. If it was added there, Asset Router would skip
all dropped files. The ignored folders list is for folders that should never be touched:
`Assets/Plugins/`, `Assets/Editor/`, `Assets/AssetRouter/`.

## Path Templating as an alternative to Scope Folder

When file names encode the destination — for example the character name or level area as a
name segment — a single templated rule can replace a stack of scoped rules.

Example: if character sprites follow the convention `Char_Hero_Idle.png` (character name as
the second segment), one rule replaces three scoped ones:

- Pattern `Char_*_*` (Glob), target `Assets/Art/Characters/{1}/`

| Imported file | Resolved target |
|---------------|-----------------|
| `Char_Hero_Idle.png` | `Assets/Art/Characters/Hero/` |
| `Char_Boss_Attack.png` | `Assets/Art/Characters/Boss/` |

Use Scope Folder when the destination depends on *who dropped the file*, not on the file name.
Use Path Templating when the destination is already encoded in the file name.

See [api/path-templating.md](../api/path-templating.md) for the full token reference.
