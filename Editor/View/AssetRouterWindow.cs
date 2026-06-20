using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Kodlon.AssetRouter.View
{
    internal sealed class AssetRouterWindow : EditorWindow
    {
        private const float ListElementHeight = 22f;
        private const float SectionSpacing = 6f;
        private ImporterSettingsDatabase _database;
        private bool _isFilterFoldoutOpen;
        private ReorderableList _rulesList;

        private GUIStyle _saveButtonStyle;
        private Vector2 _scrollPos;
        private int _selectedIndex = -1;
        private SerializedObject _serializedDb;

        [MenuItem("Tools/Asset Router Settings")]
        public static void OpenWindow()
        {
            var win = GetWindow<AssetRouterWindow>("Asset Router");
            win.minSize = new Vector2(520f, 480f);
            win.Show();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += OnProjectChanged;
            LoadDatabase(DatabaseLocator.FindDatabase());
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        // Automatically picks up a database that was created externally (e.g. by
        // AssetRouterInitializer) while the window was already open and showing no database.
        private void OnProjectChanged()
        {
            if (_database != null)
                return;

            DatabaseLocator.InvalidateCache();
            LoadDatabase(DatabaseLocator.FindDatabase());
        }

        private void OnGUI()
        {
            DrawDatabaseToolbar();

            if (_database == null)
            {
                DrawNoDatabaseHint();

                return;
            }

            _serializedDb.Update();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawGeneralSettings();
            GUILayout.Space(SectionSpacing);
            DrawFilterSettings();
            GUILayout.Space(SectionSpacing);
            DrawRulesList();
            GUILayout.Space(SectionSpacing);
            DrawSelectedRuleDetails();

            EditorGUILayout.EndScrollView();

            GUILayout.Space(4f);
            DrawSaveButton();

            if (_serializedDb.ApplyModifiedProperties())
                DatabaseLocator.InvalidateCache();
        }

        private void AddRule(ReorderableList list, BaseImportRule newRule)
        {
            _serializedDb.Update();

            var newIndex = list.serializedProperty.arraySize;
            list.serializedProperty.arraySize++;

            var newEl = list.serializedProperty.GetArrayElementAtIndex(newIndex);
            newEl.managedReferenceValue = newRule;

            _serializedDb.ApplyModifiedProperties();

            list.index = newIndex;
            _selectedIndex = newIndex;
        }

        private void BuildReorderableList()
        {
            var rulesProp = _serializedDb.FindProperty("rules");

            _rulesList = new ReorderableList(_serializedDb, rulesProp,
                true, true,
                true, true);

            _rulesList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Import Rules", EditorStyles.boldLabel);

            _rulesList.elementHeightCallback = _ => ListElementHeight;

            _rulesList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = rulesProp.GetArrayElementAtIndex(index);
                var ruleRef = element.managedReferenceValue as BaseImportRule;

                if (ruleRef == null)
                    return;

                var enabled = element.FindPropertyRelative("isEnabled");
                var name = element.FindPropertyRelative("ruleName");
                var prefix = element.FindPropertyRelative("prefix");
                var target = element.FindPropertyRelative("targetFolder");

                rect.y += (ListElementHeight - EditorGUIUtility.singleLineHeight) * 0.5f;
                rect.height = EditorGUIUtility.singleLineHeight;

                var toggleRect = new Rect(rect.x, rect.y, 18f, rect.height);
                enabled.boolValue = EditorGUI.Toggle(toggleRect, enabled.boolValue);

                var labelRect = new Rect(rect.x + 22f, rect.y, rect.width - 22f, rect.height);

                var labelStyle = !enabled.boolValue ? EditorStyles.miniLabel :
                    isActive ? EditorStyles.boldLabel : EditorStyles.label;

                var prefixText = string.IsNullOrEmpty(prefix.stringValue) ? "*" : $"{prefix.stringValue}*";
                EditorGUI.LabelField(labelRect, $"{name.stringValue}   [{prefixText}]   -> {target.stringValue}", labelStyle);
            };

            _rulesList.onSelectCallback = list => _selectedIndex = list.index;

            _rulesList.onAddCallback = list => AddRule(list, new ImportRule());

            _rulesList.onRemoveCallback = list =>
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(list);
                _selectedIndex = -1;
            };
        }

        private void CreateNewDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create ImporterSettingsDatabase",
                "ImporterSettingsDatabase",
                "asset",
                "Choose location to save the database");

            if (string.IsNullOrEmpty(path))
                return;

            var db = CreateInstance<ImporterSettingsDatabase>();

            // CreateInstance does not invoke Reset(), so populate defaults explicitly.
            DefaultDatabaseFactory.PopulateDefaults(db);

            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();

            DatabaseLocator.InvalidateCache();
            LoadDatabase(db);

            Debug.Log($"[AssetRouter] Database created: \"{path}\".");
        }

        private void DrawDatabaseToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Database:", GUILayout.Width(68f));

            EditorGUI.BeginChangeCheck();
            var picked = (ImporterSettingsDatabase)EditorGUILayout.ObjectField(_database, typeof(ImporterSettingsDatabase), false);

            if (EditorGUI.EndChangeCheck())
                LoadDatabase(picked);

            if (GUILayout.Button("Create New", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                CreateNewDatabase();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilterSettings()
        {
            _isFilterFoldoutOpen = EditorGUILayout.Foldout(_isFilterFoldoutOpen, "File Filter Settings", true, EditorStyles.foldoutHeader);

            if (!_isFilterFoldoutOpen)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_serializedDb.FindProperty("monitoredExtensions"), new GUIContent("Monitored Extensions"), true);
            EditorGUILayout.PropertyField(_serializedDb.FindProperty("ignoredFolders"), new GUIContent("Ignored Folders"), true);
            EditorGUI.indentLevel--;
        }

        private void DrawGeneralSettings()
        {
            EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_serializedDb.FindProperty("enableAutoImport"), new GUIContent("Enable Auto Import"));
            EditorGUILayout.PropertyField(_serializedDb.FindProperty("showPopupForUnknownFiles"), new GUIContent("Popup for Unknown Files"));
            EditorGUI.indentLevel--;
        }

        private void DrawNoDatabaseHint()
        {
            GUILayout.Space(24f);

            EditorGUILayout.HelpBox("No ImporterSettingsDatabase selected.\nCreate a new one or select an existing one above.",
                MessageType.Info);

            GUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Create New Database", GUILayout.Width(200f)))
                    CreateNewDatabase();

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawRulesList()
        {
            _rulesList?.DoLayoutList();
        }

        private void DrawSaveButton()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (_saveButtonStyle == null)
                {
                    _saveButtonStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontStyle = FontStyle.Bold,
                        fixedHeight = 30f
                    };
                }

                if (GUILayout.Button("Save / Apply", _saveButtonStyle, GUILayout.Width(150f)))
                {
                    EditorUtility.SetDirty(_database);
                    AssetDatabase.SaveAssets();
                    DatabaseLocator.InvalidateCache();
                    Debug.Log("[AssetRouter] Settings saved.");
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(4f);
        }

        private void DrawSelectedRuleDetails()
        {
            if (_rulesList == null)
                return;

            _selectedIndex = _rulesList.index;

            var rulesProp = _serializedDb.FindProperty("rules");

            if (_selectedIndex < 0 || _selectedIndex >= rulesProp.arraySize)
                return;

            var element = rulesProp.GetArrayElementAtIndex(_selectedIndex);
            var ruleRef = element.managedReferenceValue as BaseImportRule;

            if (ruleRef == null)
                return;

            EditorGUILayout.LabelField(ruleRef.ruleName, EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.indentLevel++;

                SectionLabel("Identification");
                Field(element, "ruleName", "Rule Name");

                SectionLabel("Conditions");
                Field(element, "prefix", "Prefix");
                Field(element, "suffix", "Suffix");
                Field(element, "extensionFilter", "Extension Filter");

                SectionLabel("Target");
                Field(element, "targetFolder", "Target Folder");

                SectionLabel("Settings");
                Field(element, "preset", "Import Preset");

                EditorGUI.indentLevel--;
            }
        }

        private static void Field(SerializedProperty parent, string propName, string label)
        {
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(propName), new GUIContent(label));
        }

        private void LoadDatabase(ImporterSettingsDatabase db)
        {
            _database = db;
            _serializedDb = _database != null ? new SerializedObject(_database) : null;
            _rulesList = null;
            _selectedIndex = -1;

            if (_serializedDb != null)
                BuildReorderableList();
        }

        private static void SectionLabel(string text)
        {
            GUILayout.Space(SectionSpacing);
            EditorGUILayout.LabelField(text, EditorStyles.miniBoldLabel);
        }
    }
}
