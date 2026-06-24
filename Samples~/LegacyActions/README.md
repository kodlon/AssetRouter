# Legacy Actions

Actions that were removed from the core package in v0.7.0 and are provided here as reference examples.

| Action | Reason removed |
|---|---|
| `GenerateMeshColliderAction` | 3D-specific, outside the 2D focus of the core package |
| `RunMenuItemAction` | Replaced by `EmitUnityEventAction` (more flexible, Inspector-configurable) |

## How to use

Import this sample via **Window › Package Manager › Asset Router › Samples › Legacy Actions**.

After import the actions compile under the `AssetRouter.LegacyActions` assembly and appear in **Create › Asset Router › Actions**.

## Note on TrimAudioSilenceAction

`TrimAudioSilenceAction` remains in the core package. It was hardened with bug fixes (overflow guard, atomic write) and full unit test coverage.
