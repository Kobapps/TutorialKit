using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TutorialKit.Editor
{
    /// <summary>Lists every <see cref="TutorialGraph"/> in the project with quick actions.</summary>
    public sealed class TutorialBrowserWindow : EditorWindow
    {
        private Vector2 _scroll;
        private readonly List<TutorialGraph> _graphs = new List<TutorialGraph>();

        [MenuItem("Window/TutorialKit/Tutorial Browser")]
        public static void ShowWindow()
        {
            var w = GetWindow<TutorialBrowserWindow>();
            w.titleContent = new GUIContent("Tutorials");
            w.minSize = new Vector2(420, 300);
            w.Refresh();
        }

        private void OnFocus() => Refresh();

        private void Refresh()
        {
            _graphs.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:TutorialGraph"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var g = AssetDatabase.LoadAssetAtPath<TutorialGraph>(path);
                if (g != null) _graphs.Add(g);
            }
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("All Tutorials", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("New Tutorial", EditorStyles.toolbarButton))
                    CreateNew();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                    Refresh();
            }

            if (_graphs.Count == 0)
                EditorGUILayout.HelpBox("No TutorialGraph assets found. Create one via Assets ▸ Create ▸ TutorialKit ▸ Tutorial Graph.", MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var g in _graphs)
            {
                if (g == null) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(g.DisplayName, EditorStyles.boldLabel);
                        bool done = Application.isPlaying && TutorialDirector.Current != null
                            && TutorialDirector.Current.Persistence.IsTutorialCompleted(g.TutorialId);
                        if (done) EditorGUILayout.LabelField("✓ completed", GUILayout.Width(90));
                    }
                    EditorGUILayout.LabelField($"id: {g.TutorialId}   ·   {g.Nodes.Count} nodes", EditorStyles.miniLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Edit"))
                            TutorialGraphEditorWindow.Open(g);
                        if (GUILayout.Button("Select"))
                            Selection.activeObject = g;
                        if (GUILayout.Button("Reset Progress"))
                        {
                            PlayerPrefs.DeleteKey($"tk.{g.TutorialId}.done");
                            PlayerPrefs.Save();
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void CreateNew()
        {
            var path = EditorUtility.SaveFilePanelInProject("New Tutorial", "NewTutorial", "asset",
                "Create a new TutorialGraph asset");
            if (string.IsNullOrEmpty(path)) return;
            var graph = ScriptableObject.CreateInstance<TutorialGraph>();
            var start = new StartNode();
            start.EnsureId();
            graph.AddNode(start);
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            Refresh();
            TutorialGraphEditorWindow.Open(graph);
        }
    }
}
