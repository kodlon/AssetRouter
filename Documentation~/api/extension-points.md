# Writing Your Own Action

Asset Router actions are ScriptableObjects that implement `IAssetImportAction`. This page covers
the contract, when each method is called, and how to write a working action from scratch.

## The two types you need

```csharp
// The interface (in Kodlon.AssetRouter.Actions)
public interface IAssetImportAction
{
    bool CanRunOn(Object importedAsset, AssetImportContext ctx);
    void Execute(Object importedAsset, AssetImportContext ctx);
}

// The base class to extend (in Kodlon.AssetRouter.Actions)
public abstract class AssetImportActionAsset : ScriptableObject, IAssetImportAction
{
    public abstract bool CanRunOn(Object importedAsset, AssetImportContext ctx);
    public abstract void Execute(Object importedAsset, AssetImportContext ctx);
}
```

Extend `AssetImportActionAsset`, not the interface directly. `ScriptableObject` gives you:
- An Inspector for configuring the action without code on the caller side.
- The ability to share one action instance across multiple rules.
- Persistence inside the database `.asset` file as a sub-asset.

## The AssetImportContext

Every method receives an `AssetImportContext`:

```csharp
public readonly struct AssetImportContext
{
    public readonly string AssetPath;              // e.g. "Assets/Art/T_Rock_D.png"
    public readonly BaseImportRule Rule;           // the rule that triggered the action
    public readonly ImporterSettingsDatabase Database; // the full database
    public readonly ILogger Logger;               // use for log output
}
```

`AssetPath` uses forward slashes (Unity convention) and is the path after the move. When your
action runs, the asset is already in `targetFolder`.

## CanRunOn

Return false to skip the action for a specific asset. The pipeline does not call `Execute` for
that asset. No log entry is created for a skip.

Use `CanRunOn` to:
- Check the asset type: `importedAsset is Texture2D`
- Check importer state: `AssetImporter.GetAtPath(ctx.AssetPath) is TextureImporter ti && ti.textureType == Sprite`
- Guard against missing configuration: `myField != null`
- Prevent re-entry if your action triggers a re-import

## Execute

Runs only when `CanRunOn` returned true. Exceptions thrown here are caught by the pipeline,
logged with `Debug.LogException`, and do not stop subsequent actions in the chain.

When your action triggers a re-import (e.g. `AssetDatabase.ImportAsset(path, ForceUpdate)`),
add a re-entry guard:

```csharp
private static readonly HashSet<string> _processing = new(StringComparer.OrdinalIgnoreCase);

public override void Execute(Object importedAsset, AssetImportContext ctx)
{
    if (!_processing.Add(ctx.AssetPath)) return;
    try
    {
        // do work, then re-import
        AssetDatabase.ImportAsset(ctx.AssetPath, ImportAssetOptions.ForceUpdate);
    }
    finally
    {
        _processing.Remove(ctx.AssetPath);
    }
}
```

## Minimal example

```csharp
using Kodlon.AssetRouter.Actions;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Asset Router/Actions/Log Asset Name")]
public sealed class LogAssetNameAction : AssetImportActionAsset
{
    public override bool CanRunOn(Object asset, AssetImportContext ctx)
        => asset != null;

    public override void Execute(Object asset, AssetImportContext ctx)
        => ctx.Logger.Log($"[MyPlugin] Imported: {ctx.AssetPath}");
}
```

After compiling, create an instance via **Create > Asset Router > Actions > Log Asset Name**,
then add it to a rule in the Settings window via the `+` button in the Post-Import Actions list.

## Assembly setup

Your action script must be in an Editor-only assembly that references `AssetRouter.Editor`.
In your `.asmdef`:

```json
{
    "name": "MyPlugin.Actions",
    "includePlatforms": ["Editor"],
    "references": ["AssetRouter.Editor"]
}
```

If your action uses the `IAssetRouterPrefabSetup` or `IAssetRouterDataSetup` callback interfaces,
also reference `AssetRouter.Runtime`.

## Using the scaffolding wizard

For common action patterns, Asset Router provides a code generator at
**Assets > Create > Asset Router > New Action...**:

| Template | Use it for |
|----------|-----------|
| Basic Action | Any action with a simple type check |
| Texture Filter Action | Actions that modify texture importer settings |
| Sprite Factory Action | Actions that create a new asset from a sprite |
| Prefab Factory Action | Actions that instantiate a template prefab |

The wizard generates a ready-to-compile `.cs` file with `{{ACTION_NAME}}` replaced by your chosen
name and `{{NAMESPACE}}` replaced by your product name.

## Error isolation

Exceptions in `Execute` are per-action: one broken action does not stop the rest of the chain.
The pipeline logs the exception with `Debug.LogException` and moves to the next action.
Design actions to be self-contained and do not rely on side effects of other actions in the chain.

## Testing your action

See [Testing Your Actions](../testing-your-actions.md) for a complete guide with examples.
