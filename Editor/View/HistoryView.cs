using System.Collections.Generic;
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

                using (new EditorGUI.DisabledScope(_sessions == null || _selectedIndex < 0))
                {
                    if (GUILayout.Button("Undo Selected Session", GUILayout.Width(165f)))
                        UndoSelected();
                }

                GUILayout.Space(8f);

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
                        var s = _sessions[i];
                        var ts    = s.timestamp?.Length >= 19 ? s.timestamp.Substring(0, 19) : s.timestamp ?? "?";
                        var label = $"{ts}  [{s.source}]  ({s.entries?.Count ?? 0})";

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
