using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.Editor
{
    internal static class ActionScaffoldingWizard
    {
        [MenuItem("Assets/Create/Asset Router/New Action.../Basic Action")]
        private static void CreateBasicAction()
            => Scaffold("NewBasicAction", BasicActionTemplate);

        [MenuItem("Assets/Create/Asset Router/New Action.../Texture Filter Action")]
        private static void CreateTextureFilterAction()
            => Scaffold("NewTextureFilterAction", TextureFilterActionTemplate);

        [MenuItem("Assets/Create/Asset Router/New Action.../Sprite Factory Action")]
        private static void CreateSpriteFactoryAction()
            => Scaffold("NewSpriteFactoryAction", SpriteFactoryActionTemplate);

        [MenuItem("Assets/Create/Asset Router/New Action.../Prefab Factory Action")]
        private static void CreatePrefabFactoryAction()
            => Scaffold("NewPrefabFactoryAction", PrefabFactoryActionTemplate);

        private static void Scaffold(string defaultName, string template)
        {
            var savePath = EditorUtility.SaveFilePanelInProject(
                "Save new action",
                defaultName,
                "cs",
                "Choose where to save the new action script.");

            if (string.IsNullOrEmpty(savePath))
                return;

            var className  = SanitizeIdentifier(Path.GetFileNameWithoutExtension(savePath), defaultName);
            var ns         = SanitizeIdentifier(PlayerSettings.productName, "Game") + ".AssetRouter";
            var content    = template
                .Replace("{{ACTION_NAME}}", className)
                .Replace("{{NAMESPACE}}", ns);

            File.WriteAllText(PathUtility.ToAbsolute(savePath), content);
            AssetDatabase.Refresh();
        }

        private static readonly HashSet<string> ReservedKeywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
            "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
            "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
            "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static",
            "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        /// <summary>
        /// Strips characters that are not valid in a C# identifier, guards against a leading digit or empty
        /// result, and escapes reserved keywords (e.g. a file named "class.cs" would otherwise generate an
        /// uncompilable "class class : ...").
        /// </summary>
        private static string SanitizeIdentifier(string raw, string fallback)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(raw))
                foreach (var c in raw)
                    if (char.IsLetterOrDigit(c) || c == '_')
                        sb.Append(c);

            if (sb.Length == 0)
                return fallback;

            if (char.IsDigit(sb[0]))
                sb.Insert(0, '_');

            var result = sb.ToString();
            return ReservedKeywords.Contains(result) ? "_" + result : result;
        }

        // ── Templates ─────────────────────────────────────────────────────────────

        private const string BasicActionTemplate =
@"using UnityEditor;
using UnityEngine;
using Kodlon.AssetRouter.Actions;

namespace {{NAMESPACE}}
{
    [CreateAssetMenu(menuName = ""Asset Router/Actions/{{ACTION_NAME}}"", fileName = ""{{ACTION_NAME}}"")]
    public sealed class {{ACTION_NAME}} : AssetImportActionAsset
    {
        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx) => true;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            // TODO: implement
        }
    }
}
";

        private const string TextureFilterActionTemplate =
@"using UnityEditor;
using UnityEngine;
using Kodlon.AssetRouter.Actions;

namespace {{NAMESPACE}}
{
    [CreateAssetMenu(menuName = ""Asset Router/Actions/{{ACTION_NAME}}"", fileName = ""{{ACTION_NAME}}"")]
    public sealed class {{ACTION_NAME}} : AssetImportActionAsset
    {
        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => AssetImporter.GetAtPath(ctx.AssetPath) is TextureImporter;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (AssetImporter.GetAtPath(ctx.AssetPath) is not TextureImporter importer)
                return;

            // TODO: modify importer settings, then reimport. Always guard with an equality check first
            // (compare the setting you're about to write against its current value) — an unconditional
            // ImportAsset call here will reimport on every run, forever.
            // AssetDatabase.ImportAsset(ctx.AssetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
";

        private const string SpriteFactoryActionTemplate =
@"using System.IO;
using UnityEditor;
using UnityEngine;
using Kodlon.AssetRouter.Actions;

namespace {{NAMESPACE}}
{
    [CreateAssetMenu(menuName = ""Asset Router/Actions/{{ACTION_NAME}}"", fileName = ""{{ACTION_NAME}}"")]
    public sealed class {{ACTION_NAME}} : AssetImportActionAsset
    {
        public string outputFolder = """";
        public string namePattern  = ""{assetName}_Output"";
        public bool overwriteExisting = false;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => AssetImporter.GetAtPath(ctx.AssetPath) is TextureImporter ti
               && ti.textureType == TextureImporterType.Sprite;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ctx.AssetPath);
            if (sprite == null) return;

            var folder   = (string.IsNullOrEmpty(outputFolder) ? (Path.GetDirectoryName(ctx.AssetPath) ?? string.Empty) : outputFolder).Replace('\\', '/').TrimEnd('/');
            var baseName = Path.GetFileNameWithoutExtension(ctx.AssetPath);
            var outName  = namePattern.Replace(""{assetName}"", baseName);
            var outPath  = folder + ""/"" + outName + "".asset"";

            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<Object>(outPath) != null)
                return;

            // TODO: create output asset using sprite, then call AssetDatabase.CreateAsset(myAsset, outPath)

            ctx.Logger.Log($""[AssetRouter] {{ACTION_NAME}} → {outPath}"");
        }
    }
}
";

        private const string PrefabFactoryActionTemplate =
@"using System.IO;
using UnityEditor;
using UnityEngine;
using Kodlon.AssetRouter.Actions;

namespace {{NAMESPACE}}
{
    [CreateAssetMenu(menuName = ""Asset Router/Actions/{{ACTION_NAME}}"", fileName = ""{{ACTION_NAME}}"")]
    public sealed class {{ACTION_NAME}} : AssetImportActionAsset
    {
        public GameObject templatePrefab;
        public string outputFolder  = """";
        public string namePattern   = ""{assetName}_Prefab"";
        public bool overwriteExisting = false;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => templatePrefab != null;

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            var folder     = (string.IsNullOrEmpty(outputFolder) ? (Path.GetDirectoryName(ctx.AssetPath) ?? string.Empty) : outputFolder).Replace('\\', '/').TrimEnd('/');
            var baseName   = System.IO.Path.GetFileNameWithoutExtension(ctx.AssetPath);
            var prefabName = namePattern.Replace(""{assetName}"", baseName);
            var prefabPath = folder + ""/"" + prefabName + "".prefab"";

            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(templatePrefab);

            try
            {
                // TODO: configure instance using importedAsset before saving
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            ctx.Logger.Log($""[AssetRouter] {{ACTION_NAME}} → {prefabPath}"");
        }
    }
}
";
    }
}
