using System.IO;
using UnityEditor;
using UnityEngine;

namespace TutorialKit.Editor
{
    /// <summary>Styled inspector for <see cref="TutorialGraph"/> with editor/export/import actions.</summary>
    [CustomEditor(typeof(TutorialGraph))]
    public sealed class TutorialGraphInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var graph = (TutorialGraph)target;

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "nodes", "edges", "entryNodeId");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox($"{graph.Nodes.Count} nodes · {graph.Edges.Count} connections", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button("Open in Graph Editor", GUILayout.Height(28)))
                    TutorialGraphEditorWindow.Open(graph);
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export JSON…"))
                    ExportJson(graph);
                if (GUILayout.Button("Import JSON…"))
                    ImportJson(graph);
            }

            if (GUILayout.Button("Reset Saved Progress"))
            {
                PlayerPrefs.DeleteKey($"tk.{graph.TutorialId}.done");
                PlayerPrefs.Save();
                Debug.Log($"[TutorialKit] Cleared completion for '{graph.TutorialId}'.");
            }
        }

        private static void ExportJson(TutorialGraph graph)
        {
            var path = EditorUtility.SaveFilePanel("Export Tutorial JSON", Application.dataPath, graph.TutorialId, "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, TutorialJson.ToJson(graph));
            Debug.Log($"[TutorialKit] Exported '{graph.TutorialId}' → {path}");
        }

        private static void ImportJson(TutorialGraph graph)
        {
            var path = EditorUtility.OpenFilePanel("Import Tutorial JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            Undo.RecordObject(graph, "Import Tutorial JSON");
            TutorialJson.Overwrite(graph, File.ReadAllText(path));
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TutorialKit] Imported into '{graph.name}'.");
        }
    }

    /// <summary>Styled inspector for <see cref="TutorialTrigger"/> with a Play-now action.</summary>
    [CustomEditor(typeof(TutorialTrigger))]
    public sealed class TutorialTriggerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var trigger = (TutorialTrigger)target;

            EditorGUILayout.Space(6);
            if (trigger.Graph != null && GUILayout.Button("Open Graph"))
                TutorialGraphEditorWindow.Open(trigger.Graph);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button(Application.isPlaying ? "Play Now" : "Play Now (enter Play mode)"))
                    trigger.TryPlay();
            }
        }
    }

    /// <summary>Inspector for <see cref="TutorialTarget"/> that surfaces the effective id.</summary>
    [CustomEditor(typeof(TutorialTarget))]
    public sealed class TutorialTargetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var t = (TutorialTarget)target;
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox($"Referenced by tutorials as id:  \"{t.Id}\"", MessageType.Info);
        }
    }
}
