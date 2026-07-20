using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TutorialKit.Editor
{
    /// <summary>
    /// Right-docked inspector with two tabs: <b>Node</b> (the selected node's fields) and <b>Tutorial</b>
    /// (the graph's identity and playback <see cref="TutorialSettings"/>). Each tab gets a colour-coded
    /// header — the node's accent, or a neutral accent for the tutorial.
    /// </summary>
    public sealed class NodeInspectorView : VisualElement
    {
        /// <summary>Which of the inspector's two tabs is showing.</summary>
        public enum Tab { Node = 0, Tutorial = 1 }

        private static readonly Color DefaultAccent = new Color(0.36f, 0.40f, 0.48f);
        private static readonly Color GraphAccent = new Color(0.29f, 0.33f, 0.41f);

        private readonly Button _nodeTab;
        private readonly Button _tutorialTab;
        private readonly VisualElement _header;
        private readonly VisualElement _chip;
        private readonly Label _title;
        private readonly Button _editScript;
        private readonly Label _typeLabel;
        private readonly Label _desc;
        private readonly Label _empty;
        private readonly VisualElement _card;
        private readonly VisualElement _fields;

        // What the panel is currently describing; re-rendered whenever the tab or the selection changes.
        private Tab _tab = Tab.Tutorial;
        private TutorialGraph _graph;
        private SerializedObject _serializedGraph;
        private TutorialNode _node;
        private Action _onNodeChanged;

        private MonoScript _script;
        private static readonly Dictionary<Type, MonoScript> _scriptCache = new Dictionary<Type, MonoScript>();

        public NodeInspectorView()
        {
            AddToClassList("tk-inspector");

            var tabs = new VisualElement();
            tabs.AddToClassList("tk-tabs");
            _nodeTab = MakeTab("Node", "The selected node's properties", Tab.Node);
            _tutorialTab = MakeTab("Tutorial", "Settings for the whole tutorial: play mode, busy policy, input lock, pause", Tab.Tutorial);
            tabs.Add(_nodeTab);
            tabs.Add(_tutorialTab);
            Add(tabs);

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

            _empty = new Label();
            _empty.AddToClassList("tk-inspector-empty");
            Add(_empty);

            _card = new VisualElement();
            _card.AddToClassList("tk-inspector-card");
            _fields = new VisualElement();
            _card.Add(_fields);
            Add(_card);

            Render();
        }

        private Button MakeTab(string text, string tooltip, Tab tab)
        {
            var button = new Button(() => SelectTab(tab)) { text = text, tooltip = tooltip };
            button.AddToClassList("tk-tab");
            return button;
        }

        /// <summary>Switches tabs (also reachable from the tab bar).</summary>
        public void SelectTab(Tab tab)
        {
            _tab = tab;
            Render();
        }

        /// <summary>Points the panel at a graph. Shows the Tutorial tab, since nothing is selected yet.</summary>
        public void SetGraph(TutorialGraph graph, SerializedObject serializedGraph)
        {
            _graph = graph;
            _serializedGraph = serializedGraph;
            _node = null;
            _onNodeChanged = null;
            _tab = Tab.Tutorial;
            Render();
        }

        /// <summary>Shows a node's fields, switching to the Node tab.</summary>
        public void ShowNode(TutorialNode node, SerializedObject serializedGraph, Action onChanged)
        {
            _node = node;
            _serializedGraph = serializedGraph;
            _onNodeChanged = onChanged;
            _tab = Tab.Node;
            Render();
        }

        public void Clear()
        {
            _graph = null;
            _serializedGraph = null;
            _node = null;
            _onNodeChanged = null;
            Render();
        }

        // ---- Rendering ----

        private void Render()
        {
            _fields.Clear();
            _script = null;
            _editScript.style.display = DisplayStyle.None;

            _nodeTab.EnableInClassList("tk-tab--active", _tab == Tab.Node);
            _tutorialTab.EnableInClassList("tk-tab--active", _tab == Tab.Tutorial);

            if (_tab == Tab.Tutorial) RenderTutorial();
            else RenderNode();
        }

        private void RenderTutorial()
        {
            if (_graph == null || _serializedGraph == null)
            {
                ShowPlaceholder("Inspector", "Open a tutorial to edit its settings.", DefaultAccent);
                return;
            }

            SetAccent(GraphAccent);
            _title.text = "Tutorial Settings";
            _typeLabel.text = _graph.DisplayName;
            SetDescription("Applies to the whole tutorial — how often it plays, what happens if it's triggered while another one is running, and what the game does while it plays.");
            _empty.style.display = DisplayStyle.None;
            _card.style.display = DisplayStyle.Flex;

            foreach (var path in new[] { "tutorialId", "displayName", "description", "settings", "autoOpenEditor" })
            {
                var prop = _serializedGraph.FindProperty(path);
                if (prop == null) continue;
                var field = new PropertyField(prop);
                field.BindProperty(prop);
                _fields.Add(field);
            }

            _fields.Bind(_serializedGraph);
        }

        private void RenderNode()
        {
            if (_node == null || _serializedGraph == null)
            {
                ShowPlaceholder("Inspector",
                    "Select a node to edit its properties.\n\nRight-click the canvas to add nodes.",
                    DefaultAccent);
                return;
            }

            var info = NodeTypeRegistry.Get(_node.GetType());
            SetAccent(info != null && info.HasColor ? info.Color : DefaultAccent);

            _empty.style.display = DisplayStyle.None;
            _card.style.display = DisplayStyle.Flex;
            _title.text = _node.DisplayName;
            _typeLabel.text = info != null && !string.IsNullOrEmpty(info.MenuPath) ? info.MenuPath : _node.GetType().Name;
            SetDescription(info != null ? info.Description : null);

            // Shortcut to open the node's own .cs (works for one-class-per-file nodes, i.e. custom nodes).
            _script = FindScript(_node.GetType());
            _editScript.style.display = _script != null ? DisplayStyle.Flex : DisplayStyle.None;

            var el = TutorialNodeView.FindNodeProperty(_serializedGraph, _node.Id);
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
                var onChanged = _onNodeChanged;
                var field = new PropertyField(it);
                field.BindProperty(it);
                field.RegisterValueChangeCallback(_ => onChanged?.Invoke());
                _fields.Add(field);
            }
            if (!any)
                _fields.Add(new Label("This node has no editable fields.")
                { style = { color = new Color(0.6f, 0.62f, 0.68f), unityFontStyleAndWeight = FontStyle.Italic } });

            _fields.Bind(_serializedGraph);
        }

        private void ShowPlaceholder(string title, string message, Color accent)
        {
            SetAccent(accent);
            _title.text = title;
            _typeLabel.text = "";
            SetDescription(null);
            _empty.text = message;
            _empty.style.display = DisplayStyle.Flex;
            _card.style.display = DisplayStyle.None;
        }

        private void SetDescription(string text)
        {
            _desc.text = text ?? "";
            _desc.style.display = string.IsNullOrEmpty(_desc.text) ? DisplayStyle.None : DisplayStyle.Flex;
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
