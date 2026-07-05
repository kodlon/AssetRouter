using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.View
{
    [InitializeOnLoad]
    internal sealed class WelcomeWindow : EditorWindow
    {
        private const string SessionKey = "AssetRouter.WelcomeShown";

        static WelcomeWindow()
        {
            EditorApplication.delayCall -= CheckAndShow;
            EditorApplication.delayCall += CheckAndShow;
        }

        private static void CheckAndShow()
        {
            if (Application.isBatchMode)
                return;

            if (AssetDatabase.FindAssets("t:ImporterSettingsDatabase").Length > 0)
                return;

            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);

            var win = CreateInstance<WelcomeWindow>();
            win.titleContent = new GUIContent("Asset Router");
            win.minSize = win.maxSize = new Vector2(440f, 190f);
            win.ShowUtility();
            var mainWin = EditorGUIUtility.GetMainWindowPosition();
            win.position = new Rect(
                mainWin.x + (mainWin.width  - 440f) * 0.5f,
                mainWin.y + (mainWin.height - 190f) * 0.5f,
                440f, 190f);
        }

        private void OnGUI()
        {
            GUILayout.Space(18f);

            EditorGUILayout.LabelField("Welcome to Asset Router", EditorStyles.boldLabel);

            GUILayout.Space(6f);

            EditorGUILayout.LabelField(
                "Asset Router routes imported assets to target folders and applies presets " +
                "automatically based on naming rules. A settings database stores your rules.",
                EditorStyles.wordWrappedLabel);

            GUILayout.Space(14f);

            EditorGUILayout.LabelField("Create a default database now?", EditorStyles.boldLabel);

            GUILayout.Space(10f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4f);

                if (GUILayout.Button("Create", GUILayout.Height(28f), GUILayout.Width(120f)))
                {
                    CreateDatabase();
                    Close();
                }

                GUILayout.Space(8f);

                if (GUILayout.Button("Not now", GUILayout.Height(28f), GUILayout.Width(90f)))
                    Close();
            }

            GUILayout.Space(10f);
        }

        private static void CreateDatabase()
        {
            const string folder = "Assets/AssetRouter";
            const string path   = "Assets/AssetRouter/ImporterSettingsDatabase.asset";

            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "AssetRouter");

            var db = CreateInstance<ImporterSettingsDatabase>();
            DefaultDatabaseFactory.PopulateDefaults(db);

            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            DatabaseLocator.InvalidateCache();

            Debug.Log($"[AssetRouter] Database created: {path}");

            AssetRouterWindow.OpenWindow();
        }
    }
}
