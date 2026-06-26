using System.IO;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;

namespace Kodlon.AssetRouter.View
{
    internal sealed class DiagnosticWindow : EditorWindow
    {
        private Vector2 _scroll;
        private bool _autoScroll = true;

        [MenuItem("Tools/Asset Router/Diagnostic Window")]
        public static void OpenWindow()
        {
            var win = GetWindow<DiagnosticWindow>("AR Diagnostics");
            win.minSize = new Vector2(700f, 300f);
            win.Show();
        }

        private void OnEnable()
        {
            DiagnosticLog.IsEnabled = true;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            DiagnosticLog.IsEnabled = false;
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                    DiagnosticLog.Clear();

                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", EditorStyles.toolbarButton, GUILayout.Width(85f));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{DiagnosticLog.Entries.Count} entries", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Time",    GUILayout.Width(80f));
                GUILayout.Label("Asset",   GUILayout.Width(220f));
                GUILayout.Label("Rule",    GUILayout.Width(150f));
                GUILayout.Label("Action",  GUILayout.ExpandWidth(true));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var entries = DiagnosticLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var e      = entries[i];
                var action = e.MatchedRule == null ? "no match"
                    : e.AlreadyInPlace           ? "in place"
                    : e.Moved                    ? "moved"
                    : "queued";

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(e.Timestamp,                              EditorStyles.miniLabel, GUILayout.Width(80f));
                    GUILayout.Label(Path.GetFileName(e.AssetPath),            EditorStyles.miniLabel, GUILayout.Width(220f));
                    GUILayout.Label(e.MatchedRule ?? "—",                     EditorStyles.miniLabel, GUILayout.Width(150f));
                    GUILayout.Label(action,                                   EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndScrollView();

            if (_autoScroll && entries.Count > 0)
                _scroll.y = float.MaxValue;
        }
    }
}
