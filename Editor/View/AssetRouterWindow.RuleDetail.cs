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
    internal sealed partial class AssetRouterWindow
    {
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
                Field(element, "pattern", "Pattern",
                    tooltip: "Glob (e.g. T_*_D.png) or Regex. " +
                             "Patterns with path separators (e.g. Assets/**) require 'Match Full Path' to be enabled.");
                Field(element, "matchAgainstFullPath", "Match Full Path",
                    tooltip: "When enabled, the pattern is compared against the full asset path " +
                             "(e.g. Assets/Art/T_Rock.png) instead of just the filename (T_Rock.png). " +
                             "Required for path-based patterns such as Assets/**.");
                Field(element, "scopeFolder", "Scope Folder",
                    tooltip: "When set, this rule only applies to assets inside this folder. " +
                             "Leave empty to match in all monitored folders. E.g. Assets/Art/Raw/");

                DrawPatternPreview(ruleRef);

                SectionLabel("Target");
                Field(element, "targetFolder", "Target Folder",
                    tooltip: "Destination folder for matched assets. Supports capture group tokens:\n" +
                             "  {1}, {2}, … — positional (Glob: each * is a capture group, left-to-right)\n" +
                             "  {name}      — named  (Regex: use (?<name>…) syntax)\n" +
                             "  {{ / }}     — literal brace escapes\n" +
                             "  ?           — single-char wildcard, NOT a capture (does not produce {n})\n" +
                             "Example: pattern T_Char_*_* with target Assets/Art/Characters/{1}/ routes\n" +
                             "T_Char_Hero_D.png → Assets/Art/Characters/Hero/");

                SectionLabel("Settings");
                Field(element, "preset", "Import Preset");

                if (ruleRef is ImportRule)
                {
                    SectionLabel("Post-Import Actions");
                    DrawActionsListFor(element);
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawActionsListFor(SerializedProperty ruleElement)
        {
            var actionsProp = ruleElement.FindPropertyRelative("postImportActions");
            if (actionsProp == null) return;

            EnsureActionsListBuilt(actionsProp);
            _actionsList?.DoLayoutList();
        }

        private void EnsureActionsListBuilt(SerializedProperty actionsProp)
        {
            if (_actionsList != null && _actionsForRuleIndex == _selectedIndex)
                return;

            _actionsForRuleIndex = _selectedIndex;
            _actionsList = new ReorderableList(_serializedDb, actionsProp, true, true, true, true);

            _actionsList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Post-Import Actions");

            _actionsList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;

            _actionsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var el = actionsProp.GetArrayElementAtIndex(index);
                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.ObjectField(rect, el, typeof(AssetImportActionAsset), GUIContent.none);
            };

            _actionsList.onAddDropdownCallback = (buttonRect, list) =>
            {
                var menu = new GenericMenu();
                var types = TypeCache.GetTypesDerivedFrom<AssetImportActionAsset>();

                foreach (var type in types)
                {
                    if (type.IsAbstract) continue;
                    var capturedType = type;
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false,
                        () => CreateAndAddAction(capturedType, list));
                }

                if (menu.GetItemCount() == 0)
                    menu.AddDisabledItem(new GUIContent("No action types found"));

                menu.ShowAsContext();
            };

            _actionsList.onRemoveCallback = list =>
            {
                var idx = list.index;
                if (idx < 0 || idx >= actionsProp.arraySize) return;

                var actionProp = actionsProp.GetArrayElementAtIndex(idx);
                var actionObj  = actionProp.objectReferenceValue;

                // Null the reference before DeleteArrayElementAtIndex (Unity requirement for Object refs).
                actionProp.objectReferenceValue = null;
                _serializedDb.ApplyModifiedProperties();

                actionsProp.DeleteArrayElementAtIndex(idx);
                _serializedDb.ApplyModifiedProperties();

                if (actionObj != null && AssetDatabase.IsSubAsset(actionObj) && !IsReferencedByOtherRule(actionObj))
                {
                    AssetDatabase.RemoveObjectFromAsset(actionObj);
                    DestroyImmediate(actionObj, true);
                    AssetDatabase.SaveAssets();
                }
            };
        }

        private void CreateAndAddAction(Type actionType, ReorderableList list)
        {
            var newAction = CreateInstance(actionType) as AssetImportActionAsset;
            if (newAction == null) return;

            newAction.name = ObjectNames.NicifyVariableName(actionType.Name);
            AssetDatabase.AddObjectToAsset(newAction, _database);
            EditorUtility.SetDirty(_database);

            _serializedDb.Update();
            var newIndex = list.serializedProperty.arraySize;
            list.serializedProperty.arraySize++;
            list.serializedProperty.GetArrayElementAtIndex(newIndex).objectReferenceValue = newAction;
            _serializedDb.ApplyModifiedProperties();
        }

        private void DrawPatternPreview(BaseImportRule rule)
        {
            if (string.IsNullOrEmpty(rule.pattern))
                return;

            if (rule.pattern != _lastPreviewPattern)
            {
                _lastPreviewPattern = rule.pattern;
                _cachedPreview      = null;
                _previewRebuildTime = EditorApplication.timeSinceStartup + PreviewDebounceSeconds;
            }

            if (_cachedPreview == null && _previewRebuildTime > 0
                && EditorApplication.timeSinceStartup >= _previewRebuildTime)
            {
                _cachedPreview      = BuildPatternPreview(rule);
                _previewRebuildTime = -1;
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
            if (rule.patternMode == PatternMode.Regex
                && PatternMatcher.TryGetRegexError(rule.pattern, out var error))
                return $"⚠ {error}";

            var hasTokens = !string.IsNullOrEmpty(rule.targetFolder) && rule.targetFolder.Contains('{');
            var lines     = new List<string>(3);
            var guids     = AssetDatabase.FindAssets("", new[] { "Assets" });
            var limit     = Mathf.Min(guids.Length, 500);
            var hasScope  = !string.IsNullOrEmpty(rule.scopeFolder);

            for (var i = 0; i < limit && lines.Count < 3; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (hasScope && !PathUtility.IsUnderFolder(path, rule.scopeFolder))
                    continue;

                var m = PatternMatcher.Match(rule, path);
                if (m == null)
                    continue;

                var fileName = Path.GetFileName(path);

                if (hasTokens)
                {
                    if (!TargetResolver.TryValidate(rule.targetFolder, m, out var tokenErr) && lines.Count == 0)
                        return $"⚠ {tokenErr}";

                    var resolved = TargetResolver.Resolve(rule.targetFolder, m);
                    lines.Add($"{fileName}  →  {resolved}");
                }
                else
                {
                    lines.Add(fileName);
                }
            }

            if (lines.Count == 0)
                return "— no matches found in project";

            return hasTokens
                ? "✓ e.g.\n" + string.Join("\n", lines)
                : "✓ e.g. " + string.Join(", ", lines);
        }

        private void CleanUpActionSubAssets(IList<AssetImportActionAsset> actions)
        {
            if (actions == null) return;

            foreach (var action in actions)
            {
                if (action == null || !AssetDatabase.IsSubAsset(action))
                    continue;

                if (!IsReferencedByOtherRule(action))
                {
                    AssetDatabase.RemoveObjectFromAsset(action);
                    DestroyImmediate(action, true);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private bool IsReferencedByOtherRule(UnityEngine.Object actionObj)
        {
            if (_database?.rules == null) return false;

            foreach (var r in _database.rules)
            {
                if (r is not ImportRule ir || ir.postImportActions == null)
                    continue;

                foreach (var a in ir.postImportActions)
                {
                    if (a == actionObj)
                        return true;
                }
            }

            return false;
        }

        private static void Field(SerializedProperty parent, string propName, string label, string tooltip = null)
        {
            var prop = parent.FindPropertyRelative(propName);
            if (prop == null) return;
            var content = tooltip != null ? new GUIContent(label, tooltip) : new GUIContent(label);
            EditorGUILayout.PropertyField(prop, content);
        }

        private static void SectionLabel(string text)
        {
            GUILayout.Space(SectionSpacing);
            EditorGUILayout.LabelField(text, EditorStyles.miniBoldLabel);
        }
    }
}
