# Testing Your Actions

This page shows how to write NUnit edit-mode tests for custom Asset Router actions.
The examples below use `AppendToCatalogAction` as the test subject.

## Assembly setup

Create a `.asmdef` for your tests in an `Editor` folder:

```json
{
    "name": "MyPlugin.Actions.Tests",
    "includePlatforms": ["Editor"],
    "references": [
        "AssetRouter.Editor",
        "AssetRouter.Runtime"
    ],
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "optionalUnityReferences": [
        "TestAssemblies"
    ]
}
```

Open **Window > General > Test Runner**, select **EditMode**, and your tests should appear.

## Creating an action instance

Actions are ScriptableObjects. Create them in tests with `ScriptableObject.CreateInstance<T>()`.
Always destroy them in `[TearDown]` to avoid leaks between test runs.

```csharp
[TestFixture]
public class MyActionTests
{
    private MyAction _action;
    private AssetCatalog _catalog;

    [SetUp]
    public void SetUp()
    {
        _action = ScriptableObject.CreateInstance<MyAction>();
        _catalog = ScriptableObject.CreateInstance<AssetCatalog>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_action);
        Object.DestroyImmediate(_catalog);
    }
}
```

## Building an AssetImportContext for a test

`AssetImportContext` is a struct with a constructor. You can pass a test path and a null rule
when the action does not use them:

```csharp
var ctx = new AssetImportContext(
    assetPath: "Assets/Art/T_Rock.png",
    rule: new ImportRule { ruleName = "Test Rule", targetFolder = "Assets/Art/" },
    database: ScriptableObject.CreateInstance<ImporterSettingsDatabase>(),
    logger: Debug.unityLogger
);
```

The `importedAsset` parameter to `CanRunOn` and `Execute` can be a mock object or a real asset
depending on what the action checks.

## Testing CanRunOn

```csharp
[Test]
public void CanRunOn_ReturnsFalse_WhenCatalogIsNull()
{
    _action.catalog = null;
    var ctx = MakeContext();

    var result = _action.CanRunOn(Texture2D.whiteTexture, ctx);

    Assert.IsFalse(result);
}

[Test]
public void CanRunOn_ReturnsTrue_WhenCatalogIsAssigned()
{
    _action.catalog = _catalog;
    var ctx = MakeContext();

    var result = _action.CanRunOn(Texture2D.whiteTexture, ctx);

    Assert.IsTrue(result);
}
```

## Testing Execute

```csharp
[Test]
public void Execute_AddsAssetToCatalog()
{
    _action.catalog = _catalog;
    var asset = Texture2D.whiteTexture;
    var ctx = MakeContext();

    _action.Execute(asset, ctx);

    Assert.AreEqual(1, _catalog.entries.Count);
    Assert.AreSame(asset, _catalog.entries[0]);
}

[Test]
public void Execute_DoesNotAddDuplicate()
{
    _action.catalog = _catalog;
    var asset = Texture2D.whiteTexture;
    var ctx = MakeContext();

    _action.Execute(asset, ctx);
    _action.Execute(asset, ctx);

    Assert.AreEqual(1, _catalog.entries.Count);
}
```

## Testing idempotency

Run `Execute` twice with the same input and verify the state is the same as after one run:

```csharp
[Test]
public void Execute_IsIdempotent()
{
    _action.catalog = _catalog;
    var asset = Texture2D.whiteTexture;
    var ctx = MakeContext();

    _action.Execute(asset, ctx);
    var countAfterFirst = _catalog.entries.Count;

    _action.Execute(asset, ctx);
    var countAfterSecond = _catalog.entries.Count;

    Assert.AreEqual(countAfterFirst, countAfterSecond);
}
```

## Testing error isolation

Verify that an exception in one action does not stop other actions. Use `ActionPipeline` directly:

```csharp
[Test]
public void Pipeline_ContinuesAfterActionException()
{
    var faultyAction = ScriptableObject.CreateInstance<AlwaysThrowsAction>();
    var goodAction = ScriptableObject.CreateInstance<CounterAction>();

    var rule = new ImportRule();
    rule.postImportActions.Add(faultyAction);
    rule.postImportActions.Add(goodAction);

    var db = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
    ActionPipeline.Execute(rule, Texture2D.whiteTexture, "Assets/Test.png", db);

    Assert.AreEqual(1, goodAction.ExecuteCount);

    Object.DestroyImmediate(faultyAction);
    Object.DestroyImmediate(goodAction);
    Object.DestroyImmediate(db);
}
```

## Helper method

A private helper keeps tests short:

```csharp
private AssetImportContext MakeContext(string path = "Assets/Art/T_Rock.png")
    => new AssetImportContext(
        assetPath: path,
        rule: new ImportRule { ruleName = "Test", targetFolder = "Assets/Art/" },
        database: ScriptableObject.CreateInstance<ImporterSettingsDatabase>(),
        logger: Debug.unityLogger
    );
```

## What to test

For every action you write, cover at minimum:

| Test | What it verifies |
|------|-----------------|
| `CanRunOn` returns false when required fields are null | Action does not crash on misconfiguration |
| `CanRunOn` returns true when properly configured | Action runs when it should |
| `Execute` produces the expected output | Core behavior works |
| `Execute` twice produces the same result | Idempotency (where applicable) |

For actions that modify files, check that the file contents after `Execute` match expectations.
For actions that call Unity APIs (importer settings, Addressables), consider whether you need
integration tests with real assets or unit tests with mock inputs.

## See also

[Writing Your Own Action](api/extension-points.md)
`Tests/Actions/_ExampleActionTest.cs` in the package for a full example.
