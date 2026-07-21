using System.Collections.Generic;
using System.Text;
using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.View
{
    internal sealed class NamingValidatorView
    {
        private Vector2 _scroll;
        private List<DryRunEntry> _violations;

        public void Draw(ImporterSettingsDatabase db)
        {
            if (db == null)
            {
                EditorGUILayout.HelpBox("No database selected.", MessageType.Info);

                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Project", GUILayout.Width(110f)))
                    _violations = Scan(db);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(_violations == null || _violations.Count == 0))
                {
                    if (GUILayout.Button("Copy to Clipboard", GUILayout.Width(130f)))
                        CopyToClipboard();
                }
            }

            GUILayout.Space(4f);

            if (_violations == null)
            {
                EditorGUILayout.HelpBox("Click \"Scan Project\" to find monitored assets that match no import rule.", MessageType.Info);

                return;
            }

            if (_violations.Count == 0)
            {
                EditorGUILayout.HelpBox("All monitored assets match at least one rule.", MessageType.None);

                return;
            }

            EditorGUILayout.LabelField($"{_violations.Count} file(s) match no rule:", EditorStyles.boldLabel);
            GUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("File", GUILayout.Width(220f));
                GUILayout.Label("Current Folder", GUILayout.ExpandWidth(true));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var entry in _violations)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(entry.FileName, EditorStyles.miniLabel, GUILayout.Width(220f));
                    EditorGUILayout.LabelField(entry.CurrentFolder, EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void CopyToClipboard()
        {
            if (_violations == null || _violations.Count == 0)
                return;

            var sb = new StringBuilder();

            foreach (var entry in _violations)
                sb.AppendLine(entry.AssetPath);

            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        private static List<DryRunEntry> Scan(ImporterSettingsDatabase db)
        {
            var all = DryRunPlanner.Scan(db);
            var violations = new List<DryRunEntry>(all.Count);

            foreach (var entry in all)
            {
                if (entry.MatchedRule == null)
                    violations.Add(entry);
            }

            return violations;
        }
    }
}