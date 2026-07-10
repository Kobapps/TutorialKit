using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TutorialKit.Editor
{
    /// <summary>
    /// Generates and installs an AI authoring skill (SKILL.md) describing how to author, configure,
    /// test, and debug TutorialKit tutorials in THIS project. The node reference is generated live
    /// from <see cref="NodeTypeRegistry"/>, so custom game nodes are documented automatically.
    /// </summary>
    public static class TutorialSkillGenerator
    {
        public const string SkillName = "tutorialkit-author";
        private const string BodyAssetPath = "Packages/com.tutorialkit/Editor/AISkills/SkillBody.txt";

        private const string Description =
            "Author, configure, test, and debug TutorialKit tutorial sequences in this Unity project " +
            "(vignette highlights, animated pointers, text boxes, input waits, custom nodes). Use when " +
            "creating or editing onboarding / FTUE / tutorial flows built with TutorialKit.";

        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        public static string ClaudeSkillPath =>
            Path.Combine(ProjectRoot, ".claude", "skills", SkillName, "SKILL.md");

        public static bool IsInstalled(string path) => File.Exists(path);

        /// <summary>Writes the skill to the given path (defaults to the project's .claude/skills).</summary>
        public static string Install(string path = null)
        {
            path ??= ClaudeSkillPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Build(), new UTF8Encoding(false));
            return path;
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.Append("---\n");
            sb.Append("name: ").Append(SkillName).Append('\n');
            sb.Append("description: ").Append(Description).Append('\n');
            sb.Append("---\n\n");

            var body = AssetDatabase.LoadAssetAtPath<TextAsset>(BodyAssetPath);
            sb.Append(body != null ? body.text : "# TutorialKit authoring\n(Skill body missing.)\n");
            sb.Append('\n');
            sb.Append(BuildNodeReference());
            return sb.ToString();
        }

        private static string BuildNodeReference()
        {
            var sb = new StringBuilder();
            foreach (var info in NodeTypeRegistry.All)
            {
                if (info == null) continue;
                sb.Append("\n### ").Append(info.TypeId).Append('\n');
                sb.Append('`').Append(info.MenuPath).Append('`');
                if (!string.IsNullOrEmpty(info.Description)) sb.Append(" — ").Append(info.Description);
                sb.Append('\n');

                TutorialNode instance = null;
                try { instance = info.CreateInstance(); } catch { /* ignore */ }

                var fields = info.Type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                bool any = false;
                foreach (var f in fields)
                {
                    if (f.DeclaringType == typeof(TutorialNode)) continue;
                    any = true;
                    object def = null;
                    if (instance != null) { try { def = f.GetValue(instance); } catch { } }
                    sb.Append("- `").Append(f.Name).Append("` (").Append(Friendly(f.FieldType)).Append(')');
                    if (def != null)
                    {
                        string ds = def.ToString();
                        if (!string.IsNullOrEmpty(ds)) sb.Append(" = ").Append(ds);
                    }
                    sb.Append('\n');
                }

                // Output ports
                if (instance != null)
                {
                    var ports = instance.OutputPorts;
                    if (ports != null && ports.Count != 1)
                    {
                        sb.Append("- output ports: ");
                        for (int i = 0; i < ports.Count; i++) sb.Append(i > 0 ? ", " : "").Append('`').Append(ports[i]).Append('`');
                        if (ports.Count == 0) sb.Append("(none — terminal)");
                        sb.Append('\n');
                    }
                }
                if (!any) sb.Append("- (no fields)\n");
            }
            return sb.ToString();
        }

        private static string Friendly(Type t)
        {
            if (t == typeof(float)) return "float";
            if (t == typeof(int)) return "int";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";
            if (t == typeof(Vector2)) return "Vector2";
            if (t == typeof(Color)) return "Color";
            if (t == typeof(TutorialTargetRef)) return "target id";
            if (t.IsEnum)
            {
                var names = Enum.GetNames(t);
                return t.Name + " {" + string.Join("|", names) + "}";
            }
            return t.Name;
        }
    }
}
