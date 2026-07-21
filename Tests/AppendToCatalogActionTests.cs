using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using NUnit.Framework;
using UnityEngine;

namespace Kodlon.AssetRouter.Tests
{
    [TestFixture]
    public class AppendToCatalogActionTests
    {
        // The action under test. Created fresh for each test.
        private AppendToCatalogAction _action;

        // Supporting objects the action depends on.
        private AssetCatalog _catalog;
        private ImporterSettingsDatabase _database;

        [SetUp]
        public void SetUp()
        {
            // ScriptableObject.CreateInstance is the correct way to create action instances in tests.
            // Do NOT use new() — ScriptableObjects require Unity's lifecycle.
            _action   = ScriptableObject.CreateInstance<AppendToCatalogAction>();
            _catalog  = ScriptableObject.CreateInstance<AssetCatalog>();
            _database = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
        }

        [TearDown]
        public void TearDown()
        {
            // Always destroy ScriptableObject instances created in tests to avoid leaks.
            Object.DestroyImmediate(_action);
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_database);
        }

        // --- CanRunOn tests ---

        [Test]
        public void CanRunOn_ReturnsFalse_WhenCatalogIsNull()
        {
            _action.catalog = null;

            var result = _action.CanRunOn(Texture2D.whiteTexture, MakeContext());

            Assert.IsFalse(result);
        }

        [Test]
        public void CanRunOn_ReturnsFalse_WhenAssetIsNull()
        {
            _action.catalog = _catalog;

            var result = _action.CanRunOn(null, MakeContext());

            Assert.IsFalse(result);
        }

        [Test]
        public void CanRunOn_ReturnsTrue_WhenCatalogAndAssetArePresent()
        {
            _action.catalog = _catalog;

            var result = _action.CanRunOn(Texture2D.whiteTexture, MakeContext());

            Assert.IsTrue(result);
        }

        // --- Execute tests ---

        [Test]
        public void Execute_AddsAssetToCatalog()
        {
            _action.catalog = _catalog;
            var asset = Texture2D.whiteTexture;

            _action.Execute(asset, MakeContext());

            Assert.AreEqual(1, _catalog.entries.Count);
            Assert.AreSame(asset, _catalog.entries[0]);
        }

        [Test]
        public void Execute_DoesNotAddDuplicate()
        {
            // Idempotency test: calling Execute twice with the same asset should add it only once.
            _action.catalog = _catalog;
            var asset = Texture2D.whiteTexture;

            _action.Execute(asset, MakeContext());
            _action.Execute(asset, MakeContext());

            Assert.AreEqual(1, _catalog.entries.Count);
        }

        [Test]
        public void Execute_DoesNotThrow_WhenCatalogIsNull()
        {
            // Execute should not throw even when catalog is null.
            // CanRunOn is supposed to gate this, but defensive Execute is good practice.
            _action.catalog = null;

            Assert.DoesNotThrow(() => _action.Execute(Texture2D.whiteTexture, MakeContext()));
        }

        // --- Helper ---

        // Build a minimal context for tests. Provide a custom path when the action uses ctx.AssetPath.
        private AssetImportContext MakeContext(string assetPath = "Assets/Art/T_Rock.png")
            => new AssetImportContext(
                assetPath: assetPath,
                rule: new ImportRule { ruleName = "Test Rule", targetFolder = "Assets/Art/" },
                database: _database,
                logger: Debug.unityLogger
            );
    }
}
