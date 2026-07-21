using System.Collections.Generic;
using Kodlon.AssetRouter.Data;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.View
{
    internal sealed class DryRunView
    {
        private List<DryRunEntry> _entries;
        private bool _forceReimport;
        private Vector2 _scroll;
        private bool _showNoMatch;

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
                    _entries = DryRunPlanner.Scan(db);

                GUILayout.Space(8f);

                if (_entries != null)
                {
                    if (GUILayout.Button("Select All", GUILayout.Width(80f)))
                        SetSelection(true);

                    if (GUILayout.Button("None", GUILayout.Width(50f)))
                        SetSelection(false);
                }

                GUILayout.FlexibleSpace();

                _showNoMatch = GUILayout.Toggle(_showNoMatch, "Show unmatched", GUILayout.Width(120f));
                _forceReimport = GUILayout.Toggle(_forceReimport, "Force re-import", GUILayout.Width(115f));
            }

            GUILayout.Space(4f);

            if (_entries == null)
            {
                EditorGUILayout.HelpBox("Click \"Scan Project\" to preview routing changes.", MessageType.Info);
                DrawReimportAllButton(db);

                return;
            }

            var toMove = 0;
            var inPlace = 0;
            var noMatch = 0;
            var selected = 0;

            foreach (var e in _entries)
            {
                if (e.MatchedRule == null)
                    noMatch++;
                else if (e.AlreadyInPlace)
                    inPlace++;
                else
                    toMove++;

                if (e.Selected)
                    selected++;
            }

            EditorGUILayout.LabelField($"Scan results:  {toMove} to move   {inPlace} in place   {noMatch} unmatched",
                EditorStyles.miniLabel);

            GUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("", GUILayout.Width(20f));
                GUILayout.Label("File", GUILayout.Width(155f));
                GUILayout.Label("Current Folder", GUILayout.Width(160f));
                GUILayout.Label("Target Folder", GUILayout.Width(160f));
                GUILayout.Label("Rule", GUILayout.ExpandWidth(true));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var entry in _entries)
            {
                if (entry.MatchedRule == null && !_showNoMatch)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    var canSelect = entry.MatchedRule != null && (!entry.AlreadyInPlace || _forceReimport);

                    if (canSelect)
                        entry.Selected = EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(20f));
                    else
                        GUILayout.Label(string.Empty, GUILayout.Width(20f));

                    var nameStyle = entry.AlreadyInPlace ? EditorStyles.miniLabel :
                        entry.MatchedRule != null ? EditorStyles.boldLabel : EditorStyles.miniLabel;

                    EditorGUILayout.LabelField(entry.FileName, nameStyle, GUILayout.Width(155f));
                    EditorGUILayout.LabelField(entry.CurrentFolder, EditorStyles.miniLabel, GUILayout.Width(160f));

                    var targetText = entry.MatchedRule == null ? "—" :
                        entry.AlreadyInPlace ? "(in place)" : entry.TargetPath ?? "—";

                    EditorGUILayout.LabelField(targetText, EditorStyles.miniLabel, GUILayout.Width(160f));
                    EditorGUILayout.LabelField(entry.MatchedRule?.ruleName ?? "—", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selected == 0))
                {
                    if (GUILayout.Button($"Apply Selected ({selected})", GUILayout.Height(28f)))
                        ApplySelected(db);
                }

                GUILayout.Space(8f);
                DrawReimportAllButton(db);
            }
        }

        private void ApplySelected(ImporterSettingsDatabase db)
        {
            if (_entries == null)
                return;

            BatchMover.Move(_entries, db, _forceReimport);
            _entries = null;
        }

        private void DrawReimportAllButton(ImporterSettingsDatabase db)
        {
            if (GUILayout.Button("Force Re-import In-Place", GUILayout.Height(28f)))
                ReimportInPlace(db);
        }

        private void ReimportInPlace(ImporterSettingsDatabase db)
        {
            var all = DryRunPlanner.Scan(db);

            // Only in-place matched assets are touched here — moving out-of-place assets is what
            // "Apply Selected" is for, and must go through the preview/selection flow, not this button.
            foreach (var e in all)
                e.Selected = e.MatchedRule != null && e.AlreadyInPlace;

            BatchMover.Move(all, db, true);
            _entries = null;
        }

        private void SetSelection(bool value)
        {
            if (_entries == null)
                return;

            foreach (var e in _entries)
            {
                if (e.MatchedRule != null)
                    e.Selected = value;
            }
        }
    }
}