using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TutorialKit.Editor
{
    /// <summary>
    /// Draws a <see cref="TutorialSettings"/> block, hiding the fields that don't apply to the chosen
    /// play mode and summarising the resulting behaviour in plain language.
    /// </summary>
    [CustomPropertyDrawer(typeof(TutorialSettings))]
    public sealed class TutorialSettingsDrawer : PropertyDrawer
    {
        private const float SummaryHeight = 32f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;

            foreach (var field in VisibleFields(property))
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(field, true);

            return height + EditorGUIUtility.standardVerticalSpacing + SummaryHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                foreach (var field in VisibleFields(property))
                {
                    row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                    row.height = EditorGUI.GetPropertyHeight(field, true);
                    EditorGUI.PropertyField(row, field, true);
                }

                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                row.height = SummaryHeight;
                EditorGUI.HelpBox(EditorGUI.IndentedRect(row), Summarise(property), MessageType.Info);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        /// <summary>The fields worth showing for the current play mode / lock choice, in display order.</summary>
        private static IEnumerable<SerializedProperty> VisibleFields(SerializedProperty root)
        {
            var playMode = root.FindPropertyRelative("playMode");
            yield return playMode;

            if (ModeOf(playMode) == TutorialPlayMode.Recurring)
            {
                yield return root.FindPropertyRelative("maxPlays");
                yield return root.FindPropertyRelative("cooldownSeconds");
            }

            yield return root.FindPropertyRelative("whenBusy");

            var lockInput = root.FindPropertyRelative("lockInputWhilePlaying");
            yield return lockInput;
            if (lockInput.boolValue)
                yield return root.FindPropertyRelative("inputLockGroup");

            yield return root.FindPropertyRelative("pauseGameWhilePlaying");
            yield return root.FindPropertyRelative("allowSkip");
        }

        private static TutorialPlayMode ModeOf(SerializedProperty playMode) =>
            (TutorialPlayMode)playMode.enumValueIndex;

        private static string Summarise(SerializedProperty root)
        {
            switch (ModeOf(root.FindPropertyRelative("playMode")))
            {
                case TutorialPlayMode.SingleUse:
                    return "Plays once and never again — completion is saved to persistence.";

                case TutorialPlayMode.OncePerSession:
                    return "Plays once per app run. Nothing is saved, so it plays again after a restart.";

                default:
                    int maxPlays = root.FindPropertyRelative("maxPlays").intValue;
                    float cooldown = root.FindPropertyRelative("cooldownSeconds").floatValue;

                    var text = maxPlays > 0
                        ? $"Plays up to {maxPlays} time{(maxPlays == 1 ? "" : "s")}"
                        : "Plays every time it's triggered";
                    if (cooldown > 0f)
                        text += $", at most once every {cooldown:0.#}s";

                    return text + ". Completion is never saved.";
            }
        }
    }
}
