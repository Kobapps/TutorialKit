using System;
using System.Collections.Generic;
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
            EditorGUILayout.HelpBox(DescribeProgress(graph), MessageType.None);

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
                Persistence.ResetTutorial(graph.TutorialId);
                Debug.Log($"[TutorialKit] Cleared saved progress (completion, play count, cooldown) for '{graph.TutorialId}'.");
            }
        }

        /// <summary>
        /// The live persistence backend while playing, so the readout reflects a custom save adapter;
        /// PlayerPrefs otherwise, which is what the default adapter writes.
        /// </summary>
        private static IPersistenceService Persistence =>
            (Application.isPlaying && TutorialDirector.Current != null)
                ? TutorialDirector.Current.Persistence
                : _editModePersistence ??= new PlayerPrefsPersistenceService();

        private static PlayerPrefsPersistenceService _editModePersistence;

        /// <summary>One line of saved state, so authors can see why a tutorial won't replay.</summary>
        private static string DescribeProgress(TutorialGraph graph)
        {
            var persistence = Persistence;
            var id = graph.TutorialId;

            int plays = TutorialDirector.GetPlayCount(persistence, id);
            if (plays == 0 && !persistence.IsTutorialCompleted(id))
                return "Saved progress:  never played";

            var parts = new List<string> { $"played {plays}×" };
            if (persistence.IsTutorialCompleted(id)) parts.Add("marked completed");

            var since = TutorialDirector.GetTimeSinceLastPlay(persistence, id);
            if (since.HasValue && since.Value != TimeSpan.MaxValue)
                parts.Add($"last {Ago(since.Value)}");

            return "Saved progress:  " + string.Join(" · ", parts);
        }

        private static string Ago(TimeSpan span)
        {
            if (span.TotalMinutes < 1) return $"{span.TotalSeconds:0}s ago";
            if (span.TotalHours < 1) return $"{span.TotalMinutes:0}m ago";
            if (span.TotalDays < 1) return $"{span.TotalHours:0}h ago";
            return $"{span.TotalDays:0}d ago";
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
