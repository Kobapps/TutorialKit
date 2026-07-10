using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TutorialKit.Editor
{
    /// <summary>Right-docked inspector: a colour-coded header (the node's accent) plus its editable fields.</summary>
    public sealed class NodeInspectorView : VisualElement
    {
        private static readonly Color DefaultAccent = new Color(0.36f, 0.40f, 0.48f);

        private readonly VisualElement _header;
        private readonly VisualElement _chip;
        private readonly Label _title;
        private readonly Button _editScript;
        private readonly Label _typeLabel;
        private readonly Label _desc;
        private readonly Label _empty;
        private readonly VisualElement _card;
        private readonly VisualElement _fields;

        private MonoScript _script;
        private static readonly Dictionary<Type, MonoScript> _scriptCache = new Dictionary<Type, MonoScript>();

        public NodeInspectorView()
        {
            AddToClassList("tk-inspector");

            _header = new VisualElement();
            _header.AddToClassList("tk-inspector-header");
            _chip = new VisualElement();
            _chip.AddToClassList("tk-inspector-chip");
            _title = new Label("Inspector");
            _title.AddToClassList("tk-inspector-title");
            _header.Add(_chip);
            _header.Add(_title);

            _editScript = new Button(OpenScript) { tooltip = "Open this node's C# script in your editor" };
            _editScript.AddToClassList("tk-inspector-editscript");
            _editScript.Add(ScriptIcon());
            _editScript.Add(new Label("Edit Script"));
            _editScript.style.display = DisplayStyle.None;
            _header.Add(_editScript);
            Add(_header);

            _typeLabel = new Label();
            _typeLabel.AddToClassList("tk-inspector-type");
            Add(_typeLabel);

            _desc = new Label();
            _desc.AddToClassList("tk-inspector-desc");
            Add(_desc);

            _empty = new Label("Select a node to edit its properties.\n\nRight-click the canvas to add nodes.");
            _empty.AddToClassList("tk-inspector-empty");
            Add(_empty);

            _card = new VisualElement();
            _card.AddToClassList("tk-inspector-card");
            _fields = new VisualElement();
            _card.Add(_fields);
            Add(_card);

            SetAccent(DefaultAccent);
            _card.style.display = DisplayStyle.None;
        }

        public void Clear()
        {
            _fields.Clear();
            _title.text = "Inspector";
            _typeLabel.text = "";
            _desc.text = "";
            _script = null;
            _editScript.style.display = DisplayStyle.None;
            _empty.style.display = DisplayStyle.Flex;
            _card.style.display = DisplayStyle.None;
            SetAccent(DefaultAccent);
        }

        public void ShowNode(TutorialNode node, SerializedObject serializedGraph, Action onChanged)
        {
            _fields.Clear();
            if (node == null || serializedGraph == null)
            {
                Clear();
                return;
            }

            var info = NodeTypeRegistry.Get(node.GetType());
            Color accent = info != null && info.HasColor ? info.Color : DefaultAccent;
            SetAccent(accent);

            _empty.style.display = DisplayStyle.None;
            _card.style.display = DisplayStyle.Flex;
            _title.text = node.DisplayName;
            _typeLabel.text = info != null && !string.IsNullOrEmpty(info.MenuPath) ? info.MenuPath : node.GetType().Name;
            _desc.text = info != null ? info.Description ?? "" : "";
            _desc.style.display = string.IsNullOrEmpty(_desc.text) ? DisplayStyle.None : DisplayStyle.Flex;

            // Shortcut to open the node's own .cs (works for one-class-per-file nodes, i.e. custom nodes).
            _script = FindScript(node.GetType());
            _editScript.style.display = _script != null ? DisplayStyle.Flex : DisplayStyle.None;

            var el = TutorialNodeView.FindNodeProperty(serializedGraph, node.Id);
            if (el == null) return;

            var end = el.GetEndProperty();
            var it = el.Copy();
            bool enter = true;
            bool any = false;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (SerializedProperty.EqualContents(it, end)) break;
                if (it.name == "id" || it.name == "editorPosition") continue;
                any = true;
                var field = new PropertyField(it);
                field.BindProperty(it);
                field.RegisterValueChangeCallback(_ => onChanged?.Invoke());
                _fields.Add(field);
            }
            if (!any)
                _fields.Add(new Label("This node has no editable fields.")
                { style = { color = new Color(0.6f, 0.62f, 0.68f), unityFontStyleAndWeight = FontStyle.Italic } });

            _fields.Bind(serializedGraph);
        }

        private void OpenScript()
        {
            if (_script != null) AssetDatabase.OpenAsset(_script);
        }

        private static Image ScriptIcon()
        {
            var img = new Image { style = { width = 13, height = 13, marginRight = 3 } };
            try { img.image = EditorGUIUtility.IconContent("cs Script Icon").image; } catch { }
            return img;
        }

        // Resolve the MonoScript for a node type by matching the class it defines. This finds custom
        // nodes authored one-per-file (file named after the class); built-in nodes that share a file
        // return no script, so the button simply stays hidden for them.
        private static MonoScript FindScript(Type type)
        {
            if (type == null) return null;
            if (_scriptCache.TryGetValue(type, out var cached))
                return cached; // may be null (cached "not found")

            MonoScript found = null;
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript " + type.Name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms == null) continue;
                var cls = ms.GetClass();
                if (cls == type ||
                    (cls == null && System.IO.Path.GetFileNameWithoutExtension(path) == type.Name))
                {
                    found = ms;
                    break;
                }
            }
            _scriptCache[type] = found;
            return found;
        }

        private void SetAccent(Color accent)
        {
            // Richer banner + readable title based on luminance.
            _header.style.backgroundColor = new StyleColor(Color.Lerp(accent, Color.black, 0.12f));
            Color text = ReadableOn(accent);
            _title.style.color = new StyleColor(text);
            _chip.style.backgroundColor = new StyleColor(new Color(text.r, text.g, text.b, 0.9f));
        }

        private static Color ReadableOn(Color bg)
        {
            float lum = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            return lum > 0.62f ? new Color(0.12f, 0.12f, 0.14f) : Color.white;
        }
    }
}
