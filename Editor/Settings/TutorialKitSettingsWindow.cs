using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
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

        [MenuItem("Tools/TutorialKit/Settings")]
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
            DrawAnimationSection();
            EditorGUILayout.Space(12);
            DrawPointerArtSection();
            EditorGUILayout.Space(12);
            DrawStyleDefaultsSection();
            EditorGUILayout.Space(12);
            DrawPointerAnimationSection();
            EditorGUILayout.Space(12);
            DrawShaderSection();
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

        private SerializedObject _settingsSO;

        private const string PointerArtDir = "Packages/com.kobapps.tutorialkit/Runtime/Resources/TutorialKit/Pointers/";
        private const string VignetteShaderPath = "Packages/com.kobapps.tutorialkit/Runtime/Overlay/Shaders/UIVignette.shader";
        private static readonly (string field, string sprite, string label)[] PointerFields =
        {
            ("pointerHand", "hand_point", "Hand (point / tap)"),
            ("pointerHandOpen", "hand_open", "Hand — open (drag)"),
            ("pointerHandClosed", "hand_closed", "Hand — closed (grab)"),
            ("pointerArrow", "arrow", "Arrow"),
        };

        private void DrawPointerArtSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Default Pointer Art", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Sprites the pointer nodes use by default. Replace any to re-skin the hand/arrow " +
                    "project-wide; leave one empty to use the bundled default (CC0, Kenney). Custom art often " +
                    "reads smaller than the bundled hand at the same size — use Art Scale below to match it.", WrapLabel);

                var settings = LoadSettings();
                if (settings == null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("No settings asset yet.", EditorStyles.miniLabel);
                    GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                    if (GUILayout.Button("Create Settings Asset (with generic defaults)", GUILayout.Height(26)))
                    {
                        Selection.activeObject = CreateSettings();
                        _settingsSO = null;
                    }
                    GUI.backgroundColor = Color.white;
                    return;
                }

                if (_settingsSO == null || _settingsSO.targetObject != settings)
                    _settingsSO = new SerializedObject(settings);

                _settingsSO.Update();
                EditorGUI.BeginChangeCheck();
                foreach (var (field, _, label) in PointerFields)
                {
                    var prop = _settingsSO.FindProperty(field);
                    if (prop != null) EditorGUILayout.PropertyField(prop, new GUIContent(label));
                }

                // Size and Art Scale live here as well as in Pointer Animation: this is the section a game
                // opens after swapping in its own art, and scale is the first thing that needs fixing.
                EditorGUILayout.Space(4);
                GUILayout.Label("Scale (whole project)", EditorStyles.miniBoldLabel);
                var sizeProp = _settingsSO.FindProperty("pointerDefaults.Size");
                if (sizeProp != null)
                    EditorGUILayout.PropertyField(sizeProp, new GUIContent("Pointer Size (px)",
                        "Base pointer size in pixels. Scales the sprite AND the tap ring — the overall " +
                        "footprint of every pointer in the game."));
                var scaleProp = _settingsSO.FindProperty("pointerArtScale");
                if (scaleProp != null)
                {
                    EditorGUILayout.PropertyField(scaleProp, new GUIContent("Art Scale (×)",
                        "Multiplier for the pointer sprites only, on top of Pointer Size. Raise it when custom " +
                        "art looks too small (padded art usually needs 1.5–3). The tap ring and gesture " +
                        "distances keep following Pointer Size."));
                    if (sizeProp != null)
                        EditorGUILayout.LabelField(" ",
                            $"Sprites drawn at {sizeProp.floatValue * scaleProp.floatValue:0.#} px " +
                            "(longest side, aspect preserved)", EditorStyles.miniLabel);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    _settingsSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    TutorialSpriteFactory.ClearCache();
                }

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reset to bundled defaults"))
                    {
                        AssignDefaults(settings);
                        _settingsSO = null;
                        TutorialSpriteFactory.ClearCache();
                    }
                    if (GUILayout.Button("Ping asset", GUILayout.Width(100)))
                        EditorGUIUtility.PingObject(settings);
                }
                EditorGUILayout.HelpBox(
                    "Play-mode preview updates immediately. Sprites should be imported as Sprite (2D and UI).",
                    MessageType.None);
            }
        }

        private void DrawStyleDefaultsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Default Vignette & Text Box Style", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Project-wide defaults. A Show Vignette node with 'Use Global Style' on draws with the " +
                    "vignette values; the built-in text box uses the text box values, and a node's 'Default' " +
                    "alignment / 0 typewriter speed resolve to these.", WrapLabel);

                var settings = LoadSettings();
                if (settings == null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("No settings asset yet.", EditorStyles.miniLabel);
                    GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                    if (GUILayout.Button("Create Settings Asset (with generic defaults)", GUILayout.Height(26)))
                    {
                        Selection.activeObject = CreateSettings();
                        _settingsSO = null;
                    }
                    GUI.backgroundColor = Color.white;
                    return;
                }

                if (_settingsSO == null || _settingsSO.targetObject != settings)
                    _settingsSO = new SerializedObject(settings);

                _settingsSO.Update();
                EditorGUI.BeginChangeCheck();
                var vig = _settingsSO.FindProperty("vignetteDefaults");
                var txt = _settingsSO.FindProperty("textBoxDefaults");
                if (vig != null) EditorGUILayout.PropertyField(vig, new GUIContent("Vignette"), true);
                EditorGUILayout.Space(2);
                if (txt != null) EditorGUILayout.PropertyField(txt, new GUIContent("Text Box"), true);
                if (EditorGUI.EndChangeCheck())
                {
                    _settingsSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    TutorialKitSettings.ClearCache();
                }
            }
        }

        private void DrawPointerAnimationSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Pointer Animation", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Project-wide look and per-gesture timings for every animated pointer (tutorial pointers " +
                    "and standalone TutorialPointers). Durations are seconds at speed 1; a pointer's speed " +
                    "multiplier still scales them. Changes apply the next time a pointer is shown.", WrapLabel);

                var settings = LoadSettings();
                if (settings == null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("No settings asset yet.", EditorStyles.miniLabel);
                    GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                    if (GUILayout.Button("Create Settings Asset (with generic defaults)", GUILayout.Height(26)))
                    {
                        Selection.activeObject = CreateSettings();
                        _settingsSO = null;
                    }
                    GUI.backgroundColor = Color.white;
                    return;
                }

                if (_settingsSO == null || _settingsSO.targetObject != settings)
                    _settingsSO = new SerializedObject(settings);

                _settingsSO.Update();
                EditorGUI.BeginChangeCheck();
                var ptr = _settingsSO.FindProperty("pointerDefaults");
                if (ptr != null) EditorGUILayout.PropertyField(ptr, new GUIContent("Pointer"), true);
                if (EditorGUI.EndChangeCheck())
                {
                    _settingsSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    TutorialKitSettings.ClearCache();
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Reset pointer animation to defaults"))
                {
                    if (EditorUtility.DisplayDialog("TutorialKit",
                        "Reset all pointer appearance & timing values to the built-in defaults?", "Reset", "Cancel"))
                    {
                        ResetPointerDefaults(settings);
                        _settingsSO = null;
                    }
                }
                EditorGUILayout.HelpBox(
                    "Tip: enter Play mode and show a pointer (e.g. via a tutorial or TutorialPointers.DoubleTap) " +
                    "to preview timing changes live.", MessageType.None);
            }
        }

        // Replace the whole pointerDefaults sub-object with a fresh default instance, so every nested
        // timing group returns to its code default without hand-clearing each field.
        private static void ResetPointerDefaults(TutorialKitSettings s)
        {
            var field = typeof(TutorialKitSettings).GetField("pointerDefaults",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(s, new TutorialPointerDefaults());
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            TutorialKitSettings.ClearCache();
        }

        private static TutorialKitSettings LoadSettings()
        {
            var s = Resources.Load<TutorialKitSettings>(TutorialKitSettings.ResourceName);
            if (s != null) return s;
            foreach (var guid in AssetDatabase.FindAssets("t:TutorialKitSettings"))
                return AssetDatabase.LoadAssetAtPath<TutorialKitSettings>(AssetDatabase.GUIDToAssetPath(guid));
            return null;
        }

        private static TutorialKitSettings CreateSettings()
        {
            if (!AssetDatabase.IsValidFolder("Assets/TutorialKit"))
                AssetDatabase.CreateFolder("Assets", "TutorialKit");
            if (!AssetDatabase.IsValidFolder("Assets/TutorialKit/Resources"))
                AssetDatabase.CreateFolder("Assets/TutorialKit", "Resources");

            var s = ScriptableObject.CreateInstance<TutorialKitSettings>();
            AssetDatabase.CreateAsset(s, "Assets/TutorialKit/Resources/" + TutorialKitSettings.ResourceName + ".asset");
            AssignDefaults(s);
            AssetDatabase.SaveAssets();
            TutorialKitSettings.ClearCache();
            TutorialSpriteFactory.ClearCache();
            UnityEngine.Debug.Log("[TutorialKit] Created settings asset at Assets/TutorialKit/Resources/" +
                                  TutorialKitSettings.ResourceName + ".asset");
            return s;
        }

        // Fill the sprite fields with the bundled pointer art so they're visible and easy to swap.
        private static void AssignDefaults(TutorialKitSettings s)
        {
            var so = new SerializedObject(s);
            foreach (var (field, sprite, _) in PointerFields)
            {
                var prop = so.FindProperty(field);
                if (prop != null)
                    prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(PointerArtDir + sprite + ".png");
            }
            // The bundled art is calibrated for 1×, so restoring it restores the scale too.
            var scaleProp = so.FindProperty("pointerArtScale");
            if (scaleProp != null) scaleProp.floatValue = 1f;
            var shaderProp = so.FindProperty("vignetteShader");
            if (shaderProp != null)
                shaderProp.objectReferenceValue = LoadVignetteShader();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(s);
        }

        // Assign only the vignette shader, leaving the pointer art untouched.
        private static void AssignVignetteShader(TutorialKitSettings s)
        {
            var so = new SerializedObject(s);
            var prop = so.FindProperty("vignetteShader");
            if (prop != null) prop.objectReferenceValue = LoadVignetteShader();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(s);
        }

        private static Shader LoadVignetteShader() =>
            AssetDatabase.LoadAssetAtPath<Shader>(VignetteShaderPath) ?? Shader.Find("TutorialKit/UIVignette");

        private void DrawShaderSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Bundled Shaders", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "The overlay looks up its vignette shader with Shader.Find, which works in the editor but " +
                    "gets stripped from a build. Referencing it here (this asset lives in Resources) keeps it in " +
                    "the build so highlights render in a player.", WrapLabel);

                var settings = LoadSettings();
                if (settings == null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("No settings asset yet — create it in the section above.", EditorStyles.miniLabel);
                    return;
                }

                if (_settingsSO == null || _settingsSO.targetObject != settings)
                    _settingsSO = new SerializedObject(settings);

                _settingsSO.Update();
                var prop = _settingsSO.FindProperty("vignetteShader");
                EditorGUI.BeginChangeCheck();
                if (prop != null) EditorGUILayout.PropertyField(prop, new GUIContent("Vignette Shader"));
                if (EditorGUI.EndChangeCheck())
                {
                    _settingsSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                }

                if (prop != null && prop.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "No shader assigned — the vignette overlay may not render in a build. Assign the bundled shader.",
                        MessageType.Warning);
                    if (GUILayout.Button("Assign bundled UIVignette shader"))
                    {
                        AssignVignetteShader(settings);
                        _settingsSO = null;
                    }
                }
            }
        }

        private const string DOTweenDefine = "TUTORIALKIT_DOTWEEN";

        private void DrawAnimationSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Animation Backend", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Which tween engine the overlays use. The built-in runner needs no dependencies. DOTween " +
                    "is optional — selecting it adds the " + DOTweenDefine + " scripting define (only enable it " +
                    "when DOTween is installed). Plug in your own via TutorialTween.Register.", WrapLabel);

                var settings = LoadSettings();
                string current = settings != null ? settings.TweenAdapterId : TutorialTween.NativeId;
                bool isDo = string.Equals(current, DOTweenRunnerId, StringComparison.OrdinalIgnoreCase);

                EditorGUILayout.Space(4);
                int sel = isDo ? 1 : 0;
                int next = EditorGUILayout.Popup(new GUIContent("Adapter"), sel,
                    new[] { "Built-in (native)", "DOTween" });
                if (next != sel)
                {
                    var s = settings != null ? settings : CreateSettings();
                    if (next == 1) { SetDefine(DOTweenDefine, true); SetTweenId(s, DOTweenRunnerId); }
                    else SetTweenId(s, TutorialTween.NativeId);
                }

                if (next == 1 || isDo)
                {
                    bool defineOn = HasDefine(DOTweenDefine);
                    bool registered = TutorialTween.IsAvailable(DOTweenRunnerId);
                    if (!defineOn)
                        EditorGUILayout.HelpBox("Enabling DOTween — Unity will recompile with " + DOTweenDefine + ".", MessageType.Info);
                    else if (!registered)
                        EditorGUILayout.HelpBox(DOTweenDefine + " is set but the DOTween adapter isn't compiled/registered " +
                            "yet (recompiling, or DOTween isn't installed). Overlays use the built-in runner until it is.",
                            MessageType.Warning);
                    else
                        EditorGUILayout.HelpBox("DOTween adapter active.", MessageType.None);

                    using (new EditorGUI.DisabledScope(!defineOn))
                        if (GUILayout.Button("Remove " + DOTweenDefine + " define"))
                            SetDefine(DOTweenDefine, false);
                }
            }
        }

        private const string DOTweenRunnerId = "dotween";

        private static bool HasDefine(string symbol)
        {
            var nbt = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            return PlayerSettings.GetScriptingDefineSymbols(nbt)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Contains(symbol);
        }

        private static void SetDefine(string symbol, bool on)
        {
            var nbt = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var list = PlayerSettings.GetScriptingDefineSymbols(nbt)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (on == list.Contains(symbol)) return;
            if (on) list.Add(symbol); else list.Remove(symbol);
            PlayerSettings.SetScriptingDefineSymbols(nbt, string.Join(";", list));
        }

        private static void SetTweenId(TutorialKitSettings s, string id)
        {
            if (s == null) return;
            var so = new SerializedObject(s);
            var prop = so.FindProperty("tweenAdapterId");
            if (prop != null) prop.stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(s);
            TutorialKitSettings.ClearCache();
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

                EditorGUILayout.Space(4);
                bool auto = EditorGUILayout.ToggleLeft(
                    new GUIContent("Auto-open the graph editor when a tutorial plays",
                        "Project-wide default: in Play mode, open and live-attach the graph editor to whichever " +
                        "tutorial starts. Each tutorial can override this (Always / Never) in its Tutorial settings."),
                    TutorialGraphAutoOpen.Enabled);
                if (auto != TutorialGraphAutoOpen.Enabled) TutorialGraphAutoOpen.Enabled = auto;

                EditorGUILayout.Space(2);
                int dir = TutorialGraphView.DefaultVertical ? 1 : 0;
                int newDir = EditorGUILayout.Popup(
                    new GUIContent("Default graph layout",
                        "Flow direction for graphs set to 'Use Default'. Each graph can override this with the " +
                        "Vertical toggle in the graph editor toolbar."),
                    dir, new[] { "Horizontal (left → right)", "Vertical (top → bottom)" });
                if (newDir != dir) TutorialGraphView.DefaultVertical = newDir == 1;
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
