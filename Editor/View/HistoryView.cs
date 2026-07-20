using System;
using System.Collections.Generic;
using System.Globalization;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.View
{
    internal sealed class HistoryView
    {
        private List<OperationSession> _sessions;
        private int    _selectedIndex = -1;
        private Vector2 _sessionsScroll;
        private Vector2 _entriesScroll;

        public void Draw()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
                    Refresh();

                GUILayout.FlexibleSpace();

                var undoDisabled = _sessions == null || _selectedIndex < 0 || _selectedIndex >= _sessions.Count;

                using (new EditorGUI.DisabledScope(undoDisabled))
                {
                    if (GUILayout.Button("Undo Selected Session", GUILayout.Width(165f)))
                        UndoSelected();
                }

                GUILayout.Space(8f);

                if (GUILayout.Button("Empty Recycle", GUILayout.Width(105f)))
                    EmptyRecycle();

                GUILayout.Space(4f);

                if (GUILayout.Button("Clear History", GUILayout.Width(105f)))
                    ClearHistory();
            }

            GUILayout.Space(4f);

            if (_sessions == null)
            {
                EditorGUILayout.HelpBox("Click Refresh to load history.", MessageType.Info);
                return;
            }

            if (_sessions.Count == 0)
            {
                EditorGUILayout.HelpBox("No operation history found.", MessageType.Info);
                return;
            }

            var halfWidth = EditorGUIUtility.currentViewWidth * 0.45f;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(halfWidth)))
                {
                    EditorGUILayout.LabelField("Sessions", EditorStyles.boldLabel);
                    _sessionsScroll = EditorGUILayout.BeginScrollView(_sessionsScroll);

                    for (var i = _sessions.Count - 1; i >= 0; i--)
                    {
                        var s      = _sessions[i];
                        var moves  = s.entries?.Count ?? 0;
                        var arts   = s.createdAssets?.Count  ?? 0;
                        var dirs   = s.createdFolders?.Count ?? 0;
                        var extras = (arts > 0 || dirs > 0) ? $"  +{arts}a/{dirs}f" : string.Empty;
                        var label  = $"{FormatLocalTimestamp(s.timestamp)}  [{s.source}]  ({moves}){extras}";

                        var style = i == _selectedIndex ? EditorStyles.boldLabel : EditorStyles.miniLabel;

                        if (GUILayout.Button(label, style))
                            _selectedIndex = i;
                    }

                    EditorGUILayout.EndScrollView();
                }

                GUILayout.Space(4f);

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);

                    if (_selectedIndex >= 0 && _selectedIndex < _sessions.Count)
                    {
                        _entriesScroll = EditorGUILayout.BeginScrollView(_entriesScroll);
                        var session = _sessions[_selectedIndex];

                        if (session.entries != null)
                        {
                            foreach (var e in session.entries)
                                EditorGUILayout.LabelField($"{e.from}  →  {e.to}", EditorStyles.miniLabel);
                        }

                        if ((session.createdAssets  != null && session.createdAssets.Count  > 0)
                            || (session.createdFolders != null && session.createdFolders.Count > 0))
                        {
                            GUILayout.Space(6f);
                            EditorGUILayout.LabelField("Created by this session", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField("These will be cleaned up when the session is undone.", EditorStyles.miniLabel);

                            if (session.createdAssets != null)
                            {
                                foreach (var a in session.createdAssets)
                                    EditorGUILayout.LabelField($"asset  •  {a}", EditorStyles.miniLabel);
                            }

                            if (session.createdFolders != null)
                            {
                                foreach (var f in session.createdFolders)
                                    EditorGUILayout.LabelField($"folder •  {f}", EditorStyles.miniLabel);
                            }
                        }

                        EditorGUILayout.EndScrollView();
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Select a session on the left.", EditorStyles.miniLabel);
                    }
                }
            }
        }

        private void Refresh()
        {
            _sessions      = OperationLog.ReadAll();
            _selectedIndex = -1;
        }

        private void UndoSelected()
        {
            if (_sessions == null || _selectedIndex < 0 || _selectedIndex >= _sessions.Count)
                return;

            UndoEngine.Revert(_sessions[_selectedIndex]);
            Refresh();
        }

        // Log writes ISO-8601 UTC; convert to local for display.
        // RoundtripKind reads the trailing 'Z' — do not combine with AssumeUniversal (throws).
        private static string FormatLocalTimestamp(string rawTimestamp)
        {
            if (string.IsNullOrEmpty(rawTimestamp))
                return "?";

            if (DateTime.TryParse(rawTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            return rawTimestamp.Length >= 19 ? rawTimestamp.Substring(0, 19) : rawTimestamp;
        }

        private static void EmptyRecycle()
        {
            if (!AssetDatabase.IsValidFolder(UndoEngine.RecycleFolder))
            {
                EditorUtility.DisplayDialog("Empty Recycle", "The recycle folder is already empty.", "OK");
                return;
            }

            var contents = AssetDatabase.FindAssets(string.Empty, new[] { UndoEngine.RecycleFolder });
            if (contents == null || contents.Length == 0)
            {
                AssetDatabase.DeleteAsset(UndoEngine.RecycleFolder);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Empty Recycle",
                    $"Delete {contents.Length} asset(s) in {UndoEngine.RecycleFolder}?\nThis cannot be undone.",
                    "Delete",
                    "Cancel"))
                return;

            AssetDatabase.DeleteAsset(UndoEngine.RecycleFolder);
        }

        private void ClearHistory()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear History",
                    "This will permanently delete all operation history.\nThis action cannot be undone.",
                    "Clear",
                    "Cancel"))
                return;

            OperationLog.Clear();
            _sessions      = null;
            _selectedIndex = -1;
        }
    }
}
