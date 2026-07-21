using NUnit.Framework;
using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Tests
{
    public class NewActionsTests
    {
        // ── EmitUnityEventAction ─────────────────────────────────────────────────

        [Test]
        public void EmitUnityEvent_CanRunOn_NoPersistentListeners_ReturnsFalse()
        {
            var action = ScriptableObject.CreateInstance<EmitUnityEventAction>();
            var ctx    = new AssetImportContext("Assets/T_Rock.png", null, null);

            Assert.IsFalse(action.CanRunOn(null, ctx));

            Object.DestroyImmediate(action);
        }

        // ── CreateMaterialFromTextureAction ──────────────────────────────────────

        [Test]
        public void CreateMaterialFromTexture_CanRunOn_NullAsset_ReturnsFalse()
        {
            var action  = ScriptableObject.CreateInstance<CreateMaterialFromTextureAction>();
            action.baseMaterial = new Material(Shader.Find("Sprites/Default"));
            var ctx = new AssetImportContext("Assets/T_Rock.png", null, null);

            Assert.IsFalse(action.CanRunOn(null, ctx));

            Object.DestroyImmediate(action.baseMaterial);
            Object.DestroyImmediate(action);
        }

        [Test]
        public void CreateMaterialFromTexture_CanRunOn_NonTextureAsset_ReturnsFalse()
        {
            var action  = ScriptableObject.CreateInstance<CreateMaterialFromTextureAction>();
            action.baseMaterial = new Material(Shader.Find("Sprites/Default"));
            var nonTex = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            var ctx    = new AssetImportContext("Assets/Data.asset", null, null);

            Assert.IsFalse(action.CanRunOn(nonTex, ctx));

            Object.DestroyImmediate(action.baseMaterial);
            Object.DestroyImmediate(nonTex);
            Object.DestroyImmediate(action);
        }

        [Test]
        public void CreateMaterialFromTexture_CanRunOn_NullBaseMaterial_UsesPipelineDefaultFallback()
        {
            // Built-in RP Default-Material is always available in the Editor, so the null-baseMaterial
            // path always resolves.
            var action = ScriptableObject.CreateInstance<CreateMaterialFromTextureAction>();
            var tex    = new Texture2D(4, 4);
            var ctx    = new AssetImportContext("Assets/T_Rock.png", null, null);

            Assert.IsTrue(action.CanRunOn(tex, ctx));

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(action);
        }

        [Test]
        public void CreateMaterialFromTexture_CanRunOn_ValidTextureAndMaterial_ReturnsTrue()
        {
            var action  = ScriptableObject.CreateInstance<CreateMaterialFromTextureAction>();
            action.baseMaterial = new Material(Shader.Find("Sprites/Default"));
            var tex = new Texture2D(4, 4);
            var ctx = new AssetImportContext("Assets/T_Rock.png", null, null);

            Assert.IsTrue(action.CanRunOn(tex, ctx));

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(action.baseMaterial);
            Object.DestroyImmediate(action);
        }

        // ── CreateScriptableObjectFromTemplateAction ─────────────────────────────

        [Test]
        public void CreateScriptableObjectFromTemplate_CanRunOn_NullTemplate_ReturnsFalse()
        {
            var action = ScriptableObject.CreateInstance<CreateScriptableObjectFromTemplateAction>();
            var ctx    = new AssetImportContext("Assets/T_Rock.png", null, null);

            Assert.IsFalse(action.CanRunOn(null, ctx));

            Object.DestroyImmediate(action);
        }

        [Test]
        public void CreateScriptableObjectFromTemplate_CanRunOn_WithTemplate_ReturnsTrue()
        {
            var action   = ScriptableObject.CreateInstance<CreateScriptableObjectFromTemplateAction>();
            var template = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            action.template = template;
            var ctx = new AssetImportContext("Assets/T_Rock.png", null, null);

            Assert.IsTrue(action.CanRunOn(null, ctx));

            Object.DestroyImmediate(template);
            Object.DestroyImmediate(action);
        }

        // ── CreatePrefabFromTemplateAction ───────────────────────────────────────

        [Test]
        public void CreatePrefabFromTemplate_CanRunOn_NullPrefab_ReturnsFalse()
        {
            var action = ScriptableObject.CreateInstance<CreatePrefabFromTemplateAction>();
            var ctx    = new AssetImportContext("Assets/T_Rock.png", null, null);

            Assert.IsFalse(action.CanRunOn(null, ctx));

            Object.DestroyImmediate(action);
        }

        [Test]
        public void CreatePrefabFromTemplate_CanRunOn_WithPrefab_ReturnsTrue()
        {
            var action = ScriptableObject.CreateInstance<CreatePrefabFromTemplateAction>();
            var go     = new GameObject("TestTemplate");
            action.templatePrefab = go;
            var ctx = new AssetImportContext("Assets/T_Rock.png", null, null);

            Assert.IsTrue(action.CanRunOn(null, ctx));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(action);
        }

        // ── GenerateNineSliceBordersAction — pixel analysis ───────────────────────

        [Test]
        public void NineSliceBorders_ComputeBorder_AllOpaque_ReturnsZeroBorder()
        {
            var (pixels, w, h) = AllOpaque(10, 10);

            var border = GenerateNineSliceBordersAction.ComputeBorder(pixels, w, h, 0.1f);

            Assert.AreEqual(new Vector4(0, 0, 0, 0), border);
        }

        [Test]
        public void NineSliceBorders_ComputeBorder_TwoPixelPaddingAllSides_ReturnsSymmetricBorder()
        {
            const int w = 10, h = 10, pad = 2;
            var pixels = new Color[w * h];
            for (var y = pad; y < h - pad; y++)
                for (var x = pad; x < w - pad; x++)
                    pixels[y * w + x] = Color.white;

            var border = GenerateNineSliceBordersAction.ComputeBorder(pixels, w, h, 0.1f);

            Assert.AreEqual(new Vector4(pad, pad, pad, pad), border);
        }

        [Test]
        public void NineSliceBorders_ComputeBorder_LeftPaddingOnly_ReturnsCorrectLeftBorder()
        {
            const int w = 8, h = 4, leftPad = 3;
            var pixels = new Color[w * h];
            for (var y = 0; y < h; y++)
                for (var x = leftPad; x < w; x++)
                    pixels[y * w + x] = Color.white;

            var border = GenerateNineSliceBordersAction.ComputeBorder(pixels, w, h, 0.1f);

            Assert.AreEqual(leftPad, border.x);
            Assert.AreEqual(0f, border.y);
            Assert.AreEqual(0f, border.z);
            Assert.AreEqual(0f, border.w);
        }

        [Test]
        public void NineSliceBorders_ComputeBorder_BottomPaddingOnly_ReturnsCorrectBottomBorder()
        {
            const int w = 6, h = 8, bottomPad = 2;
            var pixels = new Color[w * h];
            for (var y = bottomPad; y < h; y++)
                for (var x = 0; x < w; x++)
                    pixels[y * w + x] = Color.white;

            var border = GenerateNineSliceBordersAction.ComputeBorder(pixels, w, h, 0.1f);

            Assert.AreEqual(0f, border.x);
            Assert.AreEqual(bottomPad, border.y);
            Assert.AreEqual(0f, border.z);
            Assert.AreEqual(0f, border.w);
        }

        [Test]
        public void NineSliceBorders_ComputeBorder_SingleCenterPixel_ReturnsMaxBorder()
        {
            const int w = 9, h = 9, cx = 4, cy = 4;
            var pixels = new Color[w * h];
            pixels[cy * w + cx] = Color.white;

            var border = GenerateNineSliceBordersAction.ComputeBorder(pixels, w, h, 0.1f);

            Assert.AreEqual(new Vector4(cx, cy, w - 1 - cx, h - 1 - cy), border);
        }

        [Test]
        public void NineSliceBorders_ComputeBorder_BelowAlphaThreshold_TreatedAsTransparent()
        {
            const int w = 6, h = 6;
            var pixels = new Color[w * h];
            pixels[0] = new Color(1f, 1f, 1f, 0.05f); // alpha below threshold 0.1
            pixels[w * h - 1] = Color.white;           // only bottom-right corner truly opaque

            var border = GenerateNineSliceBordersAction.ComputeBorder(pixels, w, h, 0.1f);

            // opaque region is just the last pixel at (w-1, h-1)
            Assert.AreEqual(new Vector4(w - 1, h - 1, 0f, 0f), border);
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        private static (Color[] pixels, int w, int h) AllOpaque(int w, int h)
        {
            var pixels = new Color[w * h];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            return (pixels, w, h);
        }
    }

    // ── P0 #1 regression — folder fallback must use ctx.AssetPath, not rule.targetFolder ──────

    public class ActionFolderFallbackTests
    {
        private const string TestRoot    = "Assets/TempActionsTests";
        private const string SubFolder   = TestRoot + "/Characters/Hero";
        private const string FakeAsset   = SubFolder + "/T_Char_Hero_D.png";
        private const string TokenTarget = "Assets/Art/Characters/{1}/";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "TempActionsTests");
            AssetDatabase.CreateFolder(TestRoot, "Characters");
            AssetDatabase.CreateFolder(TestRoot + "/Characters", "Hero");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
                AssetDatabase.DeleteAsset(TestRoot);
        }

        [Test]
        public void CreateScriptableObjectFromTemplate_EmptyOutputFolder_OutputInAssetPathFolder()
        {
            var rule     = new ImportRule { targetFolder = TokenTarget };
            var ctx      = new AssetImportContext(FakeAsset, rule, null);
            var template = ScriptableObject.CreateInstance<ImporterSettingsDatabase>();
            var action   = ScriptableObject.CreateInstance<CreateScriptableObjectFromTemplateAction>();
            action.template          = template;
            action.outputFolder      = "";
            action.namePattern       = "{assetName}_Data";
            action.overwriteExisting = true;

            try
            {
                action.Execute(template, ctx);
                Assert.IsNotNull(
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(SubFolder + "/T_Char_Hero_D_Data.asset"),
                    "Output must be in the resolved subfolder, not in a token-named folder.");
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(action);
            }
        }

        [Test]
        public void CreateMaterialFromTexture_EmptyOutputFolder_OutputInAssetPathFolder()
        {
            var rule    = new ImportRule { targetFolder = TokenTarget };
            var ctx     = new AssetImportContext(FakeAsset, rule, null);
            var baseMat = new Material(Shader.Find("Sprites/Default"));
            var texture = new Texture2D(4, 4);
            var action  = ScriptableObject.CreateInstance<CreateMaterialFromTextureAction>();
            action.baseMaterial      = baseMat;
            action.outputFolder      = "";
            action.namePattern       = "{assetName}_Mat";
            action.overwriteExisting = true;

            try
            {
                action.Execute(texture, ctx);
                Assert.IsNotNull(
                    AssetDatabase.LoadAssetAtPath<Material>(SubFolder + "/T_Char_Hero_D_Mat.mat"),
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
            var rule    = new ImportRule { targetFolder = TokenTarget };
            var ctx     = new AssetImportContext(FakeAsset, rule, null);
            var baseMat = new Material(Shader.Find("Sprites/Default"));
            var texture = new Texture2D(4, 4);
            var action  = ScriptableObject.CreateInstance<CreateMaterialFromTextureAction>();
            action.baseMaterial      = baseMat;
            action.outputFolder      = "Materials";
            action.namePattern       = "{assetName}_Mat";
            action.overwriteExisting = true;

            try
            {
                action.Execute(texture, ctx);
                Assert.IsNotNull(
                    AssetDatabase.LoadAssetAtPath<Material>(SubFolder + "/Materials/T_Char_Hero_D_Mat.mat"),
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

            var rule   = new ImportRule { targetFolder = TokenTarget };
            var ctx    = new AssetImportContext(FakeAsset, rule, null);
            var action = ScriptableObject.CreateInstance<CreatePrefabFromTemplateAction>();
            action.templatePrefab    = prefabAsset;
            action.outputFolder      = "";
            action.namePattern       = "{assetName}_Prefab";
            action.overwriteExisting = true;

            try
            {
                action.Execute(prefabAsset, ctx);
                Assert.IsNotNull(
                    AssetDatabase.LoadAssetAtPath<GameObject>(SubFolder + "/T_Char_Hero_D_Prefab.prefab"),
                    "Output must be in the resolved subfolder, not in a token-named folder.");
            }
            finally
            {
                Object.DestroyImmediate(action);
            }
        }

        [Test]
        public void CreateTilePaletteEntry_EmptyOutputFolder_NoSpriteAtPath_DoesNotCreateTokenFolder()
        {
            var rule   = new ImportRule { targetFolder = TokenTarget };
            var ctx    = new AssetImportContext(FakeAsset, rule, null);
            var action = ScriptableObject.CreateInstance<CreateTilePaletteEntryAction>();
            action.outputFolder = "";

            action.Execute(null, ctx);

            Assert.IsFalse(AssetDatabase.IsValidFolder("Assets/Art/Characters/{1}"),
                "Literal token folder must not be created.");
            Object.DestroyImmediate(action);
        }
    }
}
