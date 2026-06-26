using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Actions;
using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Kodlon.AssetRouter.View
{
    internal sealed partial class AssetRouterWindow : EditorWindow
    {
        private const float ListElementHeight = 22f;
        private const float SectionSpacing = 6f;
        private const double PreviewDebounceSeconds = 0.3;

        private ImporterSettingsDatabase _database;
        private bool _isFilterFoldoutOpen;
        private ReorderableList _rulesList;
        private GUIStyle _saveButtonStyle;
        private Vector2 _scrollPos;
        private int _selectedIndex = -1;
        private SerializedObject _serializedDb;

        private List<RuleConflict> _conflicts;
        private HashSet<int> _conflictedIndices;

        private string _lastPreviewPattern;
        private string _cachedPreview;
        private double _previewRebuildTime = -1;

        private ReorderableList _actionsList;
        private int _actionsForRuleIndex = -1;

        private static readonly string[] TabLabels = { "Settings", "Dry Run", "History", "Validate" };
        private int _activeTab;
        private readonly DryRunView _dryRunView = new DryRunView();
        private readonly HistoryView _historyView = new HistoryView();
        private readonly NamingValidatorView _validatorView = new NamingValidatorView();
        private Dictionary<string, int> _statsCache;

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

            _activeTab = GUILayout.Toolbar(_activeTab, TabLabels);
            GUILayout.Space(4f);

            switch (_activeTab)
            {
                case 0: DrawSettingsTab(); break;
                case 1: _dryRunView.Draw(_database); break;
                case 2: _historyView.Draw(); break;
                case 3: _validatorView.Draw(_database); break;
            }

            if (_serializedDb.ApplyModifiedProperties())
            {
                DatabaseLocator.InvalidateCache();
                RefreshConflicts();
            }

            if (_activeTab == 0 && _previewRebuildTime > 0 && EditorApplication.timeSinceStartup < _previewRebuildTime)
                Repaint();
        }

        private void DrawSettingsTab()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawGeneralSettings();
            GUILayout.Space(SectionSpacing);
            DrawFilterSettings();
            GUILayout.Space(SectionSpacing);
            DrawConflictBanner();
            DrawRulesList();
            GUILayout.Space(SectionSpacing);
            DrawSelectedRuleDetails();

            EditorGUILayout.EndScrollView();

            GUILayout.Space(4f);
            DrawSaveButton();
        }

        private void RefreshConflicts()
        {
            _actionsList = null;
            _actionsForRuleIndex = -1;

            _conflicts = _database != null
                ? ConflictDetector.Detect(_database.rules)
                : null;

            _conflictedIndices = new HashSet<int>();

            if (_conflicts == null)
                return;

            foreach (var c in _conflicts)
            {
                _conflictedIndices.Add(c.IndexA);
                _conflictedIndices.Add(c.IndexB);
            }
        }

        private void DrawConflictBanner()
        {
            if (_conflicts == null || _conflicts.Count == 0)
                return;

            var duplicates = 0;
            var overlaps = 0;

            foreach (var c in _conflicts)
            {
                if (c.Type == ConflictType.Duplicate) duplicates++;
                else overlaps++;
            }

            var parts = new List<string>();
            if (duplicates > 0) parts.Add($"{duplicates} duplicate(s)");
            if (overlaps > 0) parts.Add($"{overlaps} overlap(s)");

            EditorGUILayout.HelpBox(
                $"Rule conflicts detected: {string.Join(", ", parts)}. Rules marked ⚠ may route the same file.\n" +
                "Note: overlap detection is heuristic — false negatives are possible for uncommon patterns.",
                MessageType.Warning);
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

            RefreshConflicts();
        }

        private void BuildReorderableList()
        {
            var rulesProp = _serializedDb.FindProperty("rules");

            _rulesList = new ReorderableList(_serializedDb, rulesProp, true, true, true, true);

            _rulesList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Import Rules", EditorStyles.boldLabel);

            _rulesList.elementHeightCallback = _ => ListElementHeight;

            _rulesList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = rulesProp.GetArrayElementAtIndex(index);
                var ruleRef = element.managedReferenceValue as BaseImportRule;

                if (ruleRef == null)
                    return;

                var enabledProp = element.FindPropertyRelative("isEnabled");
                var nameProp    = element.FindPropertyRelative("ruleName");
                var patternProp = element.FindPropertyRelative("pattern");
                var targetProp  = element.FindPropertyRelative("targetFolder");

                rect.y += (ListElementHeight - EditorGUIUtility.singleLineHeight) * 0.5f;
                rect.height = EditorGUIUtility.singleLineHeight;

                var toggleRect = new Rect(rect.x, rect.y, 18f, rect.height);
                enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);

                var labelRect = new Rect(rect.x + 22f, rect.y, rect.width - 22f, rect.height);

                var labelStyle = !enabledProp.boolValue ? EditorStyles.miniLabel
                    : isActive ? EditorStyles.boldLabel
                    : EditorStyles.label;

                var hasConflict = _conflictedIndices != null && _conflictedIndices.Contains(index);
                var nameText    = hasConflict ? $"⚠ {nameProp.stringValue}" : nameProp.stringValue;
                var patternText = string.IsNullOrEmpty(patternProp.stringValue) ? "*" : patternProp.stringValue;
                var statCount   = _statsCache != null && _statsCache.TryGetValue(nameProp.stringValue, out var sc) && sc > 0
                    ? $"  ({sc}✓)"
                    : "";

                EditorGUI.LabelField(labelRect, $"{nameText}   [{patternText}]   -> {targetProp.stringValue}{statCount}", labelStyle);
            };

            _rulesList.onSelectCallback = list =>
            {
                _selectedIndex = list.index;
                _lastPreviewPattern = null;
                _cachedPreview = null;
                _previewRebuildTime = -1;
                _actionsList = null;
                _actionsForRuleIndex = -1;
            };

            _rulesList.onAddCallback = list => AddRule(list, new ImportRule());

            _rulesList.onRemoveCallback = list =>
            {
                var idx = list.index;

                // Remove rule first, apply, then clean actions — otherwise IsReferencedByOtherRule still sees the deleted rule and all sub-assets become orphans.
                List<AssetImportActionAsset> actionsToClean = null;
                if (idx >= 0 && idx < rulesProp.arraySize)
                {
                    var element = rulesProp.GetArrayElementAtIndex(idx);
                    if (element.managedReferenceValue is ImportRule importRule
                        && importRule.postImportActions != null)
                        actionsToClean = new List<AssetImportActionAsset>(importRule.postImportActions);
                }

                ReorderableList.defaultBehaviours.DoRemoveButton(list);
                _serializedDb.ApplyModifiedProperties();

                if (actionsToClean != null)
                    CleanUpActionSubAssets(actionsToClean);

                _selectedIndex = -1;
                RefreshConflicts();
            };
        }

        private void CreateNewDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create ImporterSettingsDatabase",
                "ImporterSettingsDatabase", "asset", "Choose location to save the database");

            if (string.IsNullOrEmpty(path))
                return;

            var db = CreateInstance<ImporterSettingsDatabase>();
            DefaultDatabaseFactory.PopulateDefaults(db);

            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();

            DatabaseLocator.InvalidateCache();
            LoadDatabase(db);

            Debug.Log($"[AssetRouter] Database created: \"{path}\".");
        }

        private void LoadDatabase(ImporterSettingsDatabase db)
        {
            _database = db;
            _serializedDb = _database != null ? new SerializedObject(_database) : null;
            _rulesList = null;
            _selectedIndex = -1;
            _lastPreviewPattern = null;
            _cachedPreview = null;
            _previewRebuildTime = -1;
            _actionsList = null;
            _actionsForRuleIndex = -1;
            _statsCache = RuleStatsStore.ReadAll();

            if (_serializedDb != null)
                BuildReorderableList();

            RefreshConflicts();
        }

        private void DrawDatabaseToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Database:", GUILayout.Width(68f));

            EditorGUI.BeginChangeCheck();
            var picked = (ImporterSettingsDatabase)EditorGUILayout.ObjectField(
                _database, typeof(ImporterSettingsDatabase), false);

            if (EditorGUI.EndChangeCheck())
                LoadDatabase(picked);

            if (GUILayout.Button("Create New", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                CreateNewDatabase();

            using (new EditorGUI.DisabledScope(_database == null))
            {
                if (GUILayout.Button("Export JSON", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    ExportDatabaseToJson();

                if (GUILayout.Button("Import JSON", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    ImportDatabaseFromJson();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ExportDatabaseToJson()
        {
            var path = EditorUtility.SaveFilePanel("Export Database as JSON", "", "ImporterSettings", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                JsonExporter.ExportToFile(_database, path);
                Debug.Log($"[AssetRouter] Database exported to: {path}");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Export Failed", e.Message, "OK");
            }
        }

        private void ImportDatabaseFromJson()
        {
            var path = EditorUtility.OpenFilePanel("Import Database from JSON", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                JsonImporter.ImportFromFile(_database, path);
                LoadDatabase(_database);
                Debug.Log($"[AssetRouter] Database imported from: {path}");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Import Failed", e.Message, "OK");
            }
        }

        private void DrawGeneralSettings()
        {
            EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_serializedDb.FindProperty("enableAutoImport"), new GUIContent("Enable Auto Import"));
            EditorGUILayout.PropertyField(_serializedDb.FindProperty("showPopupForUnknownFiles"), new GUIContent("Popup for Unknown Files"));
            EditorGUI.indentLevel--;
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

        private void DrawNoDatabaseHint()
        {
            GUILayout.Space(24f);
            EditorGUILayout.HelpBox(
                "No ImporterSettingsDatabase selected.\nCreate a new one or select an existing one above.",
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
    }
}
