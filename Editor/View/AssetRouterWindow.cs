using System.Collections.Generic;
using System.IO;
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

        // Conflict state — refreshed whenever rules change.
        private List<RuleConflict> _conflicts;
        private HashSet<int> _conflictedIndices;

        // Live-preview cache — recomputed only when the selected rule's pattern changes.
        private string _lastPreviewPattern;
        private string _cachedPreview;

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

            if (_serializedDb.ApplyModifiedProperties())
            {
                DatabaseLocator.InvalidateCache();
                RefreshConflicts();
            }
        }

        // ── Conflict detection ────────────────────────────────────────────────────

        private void RefreshConflicts()
        {
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
                $"Rule conflicts detected: {string.Join(", ", parts)}. Rules marked ⚠ may route the same file.",
                MessageType.Warning);
        }

        // ── Rule list ─────────────────────────────────────────────────────────────

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
                var nameProp = element.FindPropertyRelative("ruleName");
                var patternProp = element.FindPropertyRelative("pattern");
                var targetProp = element.FindPropertyRelative("targetFolder");

                rect.y += (ListElementHeight - EditorGUIUtility.singleLineHeight) * 0.5f;
                rect.height = EditorGUIUtility.singleLineHeight;

                var toggleRect = new Rect(rect.x, rect.y, 18f, rect.height);
                enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);

                var labelRect = new Rect(rect.x + 22f, rect.y, rect.width - 22f, rect.height);

                var labelStyle = !enabledProp.boolValue ? EditorStyles.miniLabel
                    : isActive ? EditorStyles.boldLabel
                    : EditorStyles.label;

                var hasConflict = _conflictedIndices != null && _conflictedIndices.Contains(index);
                var nameText = hasConflict ? $"⚠ {nameProp.stringValue}" : nameProp.stringValue;
                var patternText = string.IsNullOrEmpty(patternProp.stringValue) ? "*" : patternProp.stringValue;

                EditorGUI.LabelField(labelRect, $"{nameText}   [{patternText}]   -> {targetProp.stringValue}", labelStyle);
            };

            _rulesList.onSelectCallback = list =>
            {
                _selectedIndex = list.index;
                _lastPreviewPattern = null; // invalidate preview cache on selection change
            };

            _rulesList.onAddCallback = list => AddRule(list, new ImportRule());

            _rulesList.onRemoveCallback = list =>
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(list);
                _selectedIndex = -1;
                RefreshConflicts();
            };
        }

        // ── Database management ───────────────────────────────────────────────────

        private void CreateNewDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create ImporterSettingsDatabase",
                "ImporterSettingsDatabase", "asset", "Choose location to save the database");

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

        private void LoadDatabase(ImporterSettingsDatabase db)
        {
            _database = db;
            _serializedDb = _database != null ? new SerializedObject(_database) : null;
            _rulesList = null;
            _selectedIndex = -1;
            _lastPreviewPattern = null;

            if (_serializedDb != null)
                BuildReorderableList();

            RefreshConflicts();
        }

        // ── Drawing ───────────────────────────────────────────────────────────────

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

            EditorGUILayout.EndHorizontal();
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

                SectionLabel("Pattern");
                Field(element, "patternMode", "Mode");
                Field(element, "pattern", "Pattern");
                Field(element, "matchAgainstFullPath", "Match Full Path");

                DrawPatternPreview(ruleRef);

                SectionLabel("Target");
                Field(element, "targetFolder", "Target Folder");

                SectionLabel("Settings");
                Field(element, "preset", "Import Preset");

                EditorGUI.indentLevel--;
            }
        }

        private void DrawPatternPreview(BaseImportRule rule)
        {
            if (string.IsNullOrEmpty(rule.pattern))
                return;

            // Recompute only when the pattern string has changed.
            if (rule.pattern != _lastPreviewPattern)
            {
                _lastPreviewPattern = rule.pattern;
                _cachedPreview = BuildPatternPreview(rule);
            }

            if (string.IsNullOrEmpty(_cachedPreview))
                return;

            var isError = _cachedPreview.StartsWith("⚠");
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = isError ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.4f, 0.8f, 0.4f) },
                wordWrap = true
            };

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(_cachedPreview, style);
            EditorGUI.indentLevel--;
        }

        private static string BuildPatternPreview(BaseImportRule rule)
        {
            // Validate regex syntax first.
            if (rule.patternMode == PatternMode.Regex
                && PatternMatcher.TryGetRegexError(rule.pattern, out var error))
                return $"⚠ {error}";

            // Find up to 3 matching examples from the project (capped to avoid stalling large projects).
            var matches = new List<string>(3);
            var guids = AssetDatabase.FindAssets("", new[] { "Assets" });
            var limit = Mathf.Min(guids.Length, 500);

            for (var i = 0; i < limit && matches.Count < 3; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (PatternMatcher.Matches(rule, path))
                    matches.Add(Path.GetFileName(path));
            }

            return matches.Count > 0
                ? $"✓ e.g. {string.Join(", ", matches)}"
                : "— no matches found in project";
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void Field(SerializedProperty parent, string propName, string label)
        {
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(propName), new GUIContent(label));
        }

        private static void SectionLabel(string text)
        {
            GUILayout.Space(SectionSpacing);
            EditorGUILayout.LabelField(text, EditorStyles.miniBoldLabel);
        }
    }
}
