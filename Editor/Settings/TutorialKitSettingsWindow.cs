using UnityEditor;
using UnityEngine;

namespace TutorialKit.Editor
{
    /// <summary>
    /// TutorialKit settings & tools: install the AI authoring skill, jump to the editor/browser,
    /// and manage saved progress.
    /// </summary>
    public sealed class TutorialKitSettingsWindow : EditorWindow
    {
        private Vector2 _scroll;
        private string _lastInstalledPath;

        [MenuItem("Window/TutorialKit/Settings")]
        public static void ShowWindow()
        {
            var w = GetWindow<TutorialKitSettingsWindow>();
            w.titleContent = new GUIContent("TutorialKit");
            w.minSize = new Vector2(460, 380);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(6);
            GUILayout.Label("TutorialKit", TitleStyle);
            EditorGUILayout.LabelField("AAA tutorial authoring for Unity", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            DrawAiSkillSection();
            EditorGUILayout.Space(12);
            DrawToolsSection();
            EditorGUILayout.Space(12);
            DrawProgressSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawAiSkillSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("AI Authoring Skill", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Installs an instruction file that teaches an AI assistant (Claude Code, etc.) how to " +
                    "author, configure, test, and debug tutorials in this project. The node reference is " +
                    "generated live and includes your custom nodes.", WrapLabel);

                string path = TutorialSkillGenerator.ClaudeSkillPath;
                bool installed = TutorialSkillGenerator.IsInstalled(path);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Location", ".claude/skills/" + TutorialSkillGenerator.SkillName + "/SKILL.md");
                EditorGUILayout.LabelField("Status", installed ? "✓ Installed" : "Not installed");

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                    if (GUILayout.Button(installed ? "Update Skill" : "Install Skill", GUILayout.Height(28)))
                    {
                        _lastInstalledPath = TutorialSkillGenerator.Install();
                        UnityEngine.Debug.Log($"[TutorialKit] AI authoring skill written to {_lastInstalledPath}");
                        ShowNotification(new GUIContent("Skill installed"));
                    }
                    GUI.backgroundColor = Color.white;

                    using (new EditorGUI.DisabledScope(!installed))
                        if (GUILayout.Button("Reveal", GUILayout.Height(28), GUILayout.Width(90)))
                            EditorUtility.RevealInFinder(path);

                    if (GUILayout.Button("Preview", GUILayout.Height(28), GUILayout.Width(90)))
                        TutorialSkillPreview.Open();
                }

                if (installed)
                    EditorGUILayout.HelpBox(
                        "In an interactive Claude Code session, the skill is available as /" +
                        TutorialSkillGenerator.SkillName + ". Re-run Update after adding custom nodes.",
                        MessageType.Info);
            }
        }

        private void DrawToolsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Tools", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Graph Editor")) TutorialGraphEditorWindow.ShowWindow();
                    if (GUILayout.Button("Open Tutorial Browser")) TutorialBrowserWindow.ShowWindow();
                }
            }
        }

        private void DrawProgressSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Saved Progress", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Clear the default (PlayerPrefs) tutorial completion flags.", WrapLabel);
                if (GUILayout.Button("Reset All Tutorial Progress"))
                {
                    if (EditorUtility.DisplayDialog("TutorialKit",
                        "Delete all TutorialKit PlayerPrefs completion flags?", "Reset", "Cancel"))
                    {
                        foreach (var guid in AssetDatabase.FindAssets("t:TutorialGraph"))
                        {
                            var g = AssetDatabase.LoadAssetAtPath<TutorialGraph>(AssetDatabase.GUIDToAssetPath(guid));
                            if (g != null) PlayerPrefs.DeleteKey($"tk.{g.TutorialId}.done");
                        }
                        PlayerPrefs.Save();
                        UnityEngine.Debug.Log("[TutorialKit] Cleared tutorial progress.");
                    }
                }
            }
        }

        private static GUIStyle TitleStyle => new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
        private static GUIStyle WrapLabel => new GUIStyle(EditorStyles.label) { wordWrap = true };
    }

    /// <summary>Read-only preview of the generated skill text.</summary>
    internal sealed class TutorialSkillPreview : EditorWindow
    {
        private Vector2 _scroll;
        private string _text;

        public static void Open()
        {
            var w = GetWindow<TutorialSkillPreview>();
            w.titleContent = new GUIContent("Skill Preview");
            w.minSize = new Vector2(560, 500);
            w._text = TutorialSkillGenerator.Build();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_text ?? "", GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }
}
