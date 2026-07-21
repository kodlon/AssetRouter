using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Tests
{
    // Regression: folder fallback must use ctx.AssetPath, not rule.targetFolder.
    public class ActionFolderFallbackTests
    {
        private const string TestRoot = "Assets/TempActionsTests";
        private const string SubFolder = TestRoot + "/Characters/Hero";
        private const string FakeAsset = SubFolder + "/T_Char_Hero_D.png";
        private const string TokenTarget = "Assets/Art/Characters/{1}/";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "TempActionsTests");
            AssetDatabase.CreateFolder(TestRoot, "Characters");
            AssetDatabase.CreateFolder(TestRoot + "/Characters", "Hero");
        }

        [Test]
        public void CreateMaterialFromTexture_EmptyOutputFolder_OutputInAssetPathFolder()
        {
            var rule = new ImportRule
            {
                targetFolder = TokenTarget
            };

            var ctx = new AssetImportContext(FakeAsset, rule, null);
            var baseMat = new Material(Shader.Find("Sprites/Default"));
            var texture = new Texture2D(4, 4);
            var action = ScriptableObject.CreateInstance<CreateMaterialFromTextureAction>();
            action.baseMaterial = baseMat;
            action.outputFolder = "";
            action.namePattern = "{assetName}_Mat";
            action.overwriteExisting = true;

            try
            {
                action.Execute(texture, ctx);

                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(SubFolder + "/T_Char_Hero_D_Mat.mat"),
                    "Output must be in the resolved subfolder, not in a token-named folder.");
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(baseMat);
                Object.DestroyImmediate(action);
            }
        }

        [Test]
        public void CreateMaterialFromTexture_RelativeOutputFolder_OutputInSubfolderOfAsset()
        {
            AssetDatabase.CreateFolder(SubFolder, "Materials");

            var rule = new ImportRule
            {
                targetFolder = TokenTarget
            };

            var ctx = new AssetImportContext(FakeAsset, rule, null);
            var baseMat = new Material(Shader.Find("Sprites/Default"));
            var texture = new Texture2D(4, 4);
            var action = ScriptableObject.CreateInstance<CreateMaterialFromTextureAction>();
            action.baseMaterial = baseMat;
            action.outputFolder = "Materials";
            action.namePattern = "{assetName}_Mat";
            action.overwriteExisting = true;

            try
            {
                action.Execute(texture, ctx);

                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(SubFolder + "/Materials/T_Char_Hero_D_Mat.mat"),
                    "Relative outputFolder must resolve as a subfolder of the imported texture's folder.");
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(baseMat);
                Object.DestroyImmediate(action);
            }
        }

        [Test]
        public void CreatePrefabFromTemplate_EmptyOutputFolder_OutputInAssetPathFolder()
        {
            var go = new GameObject("TemplatePrefab");
            PrefabUtility.SaveAsPrefabAsset(go, TestRoot + "/TemplatePrefab.prefab");
            Object.DestroyImmediate(go);
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/TemplatePrefab.prefab");

            var rule = new ImportRule
            {
                targetFolder = TokenTarget
            };

            var ctx = new AssetImportContext(FakeAsset, rule, null);
            var action = ScriptableObject.CreateInstance<CreatePrefabFromTemplateAction>();
            action.templatePrefab = prefabAsset;
            action.outputFolder = "";
            action.namePattern = "{assetName}_Prefab";
            action.overwriteExisting = true;

            try
            {
                action.Execute(prefabAsset, ctx);

                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(SubFolder + "/T_Char_Hero_D_Prefab.prefab"),
                    "Output must be in the resolved subfolder, not in a token-named folder.");
            }
            finally
            {
                Object.DestroyImmediate(action);
            }
        }

        [Test]
        public void CreateScriptableObjectFromTemplate_EmptyOutputFolder_OutputInAssetPathFolder()
        {
            var rule = new ImportRule
            {
                targetFolder = TokenTarget
            };

            var ctx = new AssetImportContext(FakeAsset, rule, null);
            var template = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            var action = ScriptableObject.CreateInstance<CreateScriptableObjectFromTemplateAction>();
            action.template = template;
            action.outputFolder = "";
            action.namePattern = "{assetName}_Data";
            action.overwriteExisting = true;

            try
            {
                action.Execute(template, ctx);

                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<ScriptableObject>(SubFolder + "/T_Char_Hero_D_Data.asset"),
                    "Output must be in the resolved subfolder, not in a token-named folder.");
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(action);
            }
        }

        [Test]
        public void CreateTilePaletteEntry_EmptyOutputFolder_NoSpriteAtPath_DoesNotCreateTokenFolder()
        {
            var rule = new ImportRule
            {
                targetFolder = TokenTarget
            };

            var ctx = new AssetImportContext(FakeAsset, rule, null);
            var action = ScriptableObject.CreateInstance<CreateTilePaletteEntryAction>();
            action.outputFolder = "";

            action.Execute(null, ctx);

            Assert.IsFalse(AssetDatabase.IsValidFolder("Assets/Art/Characters/{1}"),
                "Literal token folder must not be created.");

            Object.DestroyImmediate(action);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
                AssetDatabase.DeleteAsset(TestRoot);
        }
    }
}