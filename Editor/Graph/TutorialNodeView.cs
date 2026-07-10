using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace TutorialKit.Editor
{
    /// <summary>Slim node view (title, colour chip, summary, ports). Editing happens in the inspector.</summary>
    public sealed class TutorialNodeView : Node
    {
        public TutorialNode Node { get; }
        public Port InputPort { get; private set; }
        public readonly Dictionary<string, Port> OutputPorts = new Dictionary<string, Port>();
        public readonly Dictionary<string, Port> DataInputPorts = new Dictionary<string, Port>();
        public readonly Dictionary<string, Port> DataOutputPorts = new Dictionary<string, Port>();

        private static readonly Color DataPortColor = new Color(0.20f, 0.82f, 0.75f);

        /// <summary>Raised when this node becomes the selected node.</summary>
        public event Action<TutorialNodeView> NodeSelected;

        private readonly VisualElement _entryBadge;
        private readonly Label _summary;
        private readonly bool _vertical;
        private VisualElement _topStrip, _bottomStrip;
        private Label _visitedBadge;
        private Label _warnBadge;
        private IVisualElementScheduledItem _pulse;
        private double _pulseStart;
        private bool _visited;
        private bool _active;

        public TutorialNodeView(TutorialNode node, SerializedObject serializedGraph, NodeTypeInfo info)
            : this(node, serializedGraph, info, false) { }

        public TutorialNodeView(TutorialNode node, SerializedObject serializedGraph, NodeTypeInfo info, bool vertical)
        {
            Node = node;
            _vertical = vertical;
            title = node.DisplayName;
            AddToClassList("tk-node");
            if (vertical) AddToClassList("tk-node-vertical");
            if (info != null && !string.IsNullOrEmpty(info.Description))
                tooltip = info.Description;

            Color accent = info != null && info.HasColor ? info.Color : new Color(0.4f, 0.45f, 0.55f);
            titleContainer.style.backgroundColor = new StyleColor(Dim(accent));

            // Colour chip in the title bar.
            var chip = new VisualElement();
            chip.AddToClassList("tk-node-icon");
            chip.style.backgroundColor = new StyleColor(accent);
            titleContainer.Insert(0, chip);

            // Flow ports: vertical (top in / bottom out, VFX-style) or horizontal (left in / right out).
            var flowOrient = vertical ? Orientation.Vertical : Orientation.Horizontal;
            bool multiOut = node.OutputPorts.Count > 1;

            // Input (many may converge).
            if (node.HasInput)
            {
                InputPort = InstantiatePort(flowOrient, Direction.Input, Port.Capacity.Multi, typeof(bool));
                InputPort.portName = vertical ? "" : "In";
                (vertical ? TopStrip() : inputContainer).Add(InputPort);
            }

            // Outputs (each may fan out to several nodes).
            foreach (var portName in node.OutputPorts)
            {
                var p = InstantiatePort(flowOrient, Direction.Output, Port.Capacity.Multi, typeof(bool));
                p.portName = vertical && !multiOut ? "" : portName; // keep True/False labels when branching
                (vertical ? BottomStrip() : outputContainer).Add(p);
                OutputPorts[portName] = p;
            }

            // Data input ports (typed value injection, e.g. an injected Target).
            foreach (var dp in node.InputDataPorts)
            {
                var cap = dp.Multi ? Port.Capacity.Multi : Port.Capacity.Single;
                var p = InstantiatePort(Orientation.Horizontal, Direction.Input, cap, TutorialPortTypes.ToPortType(dp.TypeId));
                p.portName = dp.Name;
                p.portColor = DataPortColor;
                inputContainer.Add(p);
                DataInputPorts[dp.Name] = p;
            }

            // Data output ports (produced values, e.g. a Target from a provider).
            foreach (var dp in node.OutputDataPorts)
            {
                var p = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, TutorialPortTypes.ToPortType(dp.TypeId));
                p.portName = dp.Name;
                p.portColor = DataPortColor;
                outputContainer.Add(p);
                DataOutputPorts[dp.Name] = p;
            }

            _entryBadge = new Label("★ START")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new StyleColor(new Color(0.32f, 0.85f, 0.4f)),
                    fontSize = 10,
                    display = DisplayStyle.None,
                    marginLeft = 6, marginRight = 4,
                },
            };
            titleContainer.Add(_entryBadge);

            _visitedBadge = new Label("✓")
            {
                style =
                {
                    color = new StyleColor(new Color(0.4f, 0.85f, 0.45f)),
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11,
                    display = DisplayStyle.None,
                    marginRight = 4,
                },
            };
            titleContainer.Add(_visitedBadge);

            _warnBadge = new Label("⚠")
            {
                style =
                {
                    color = new StyleColor(new Color(1f, 0.78f, 0.2f)),
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    display = DisplayStyle.None,
                    marginRight = 4,
                },
            };
            titleContainer.Add(_warnBadge);

            _summary = new Label();
            _summary.AddToClassList("tk-node-summary");
            RefreshSummary();
            var titleParent = titleContainer.parent;
            int titleIdx = titleParent.IndexOf(titleContainer);
            titleParent.Insert(titleIdx >= 0 ? titleIdx + 1 : titleParent.childCount, _summary);

            RefreshExpandedState();
            RefreshPorts();
            SetPosition(new Rect(node.EditorPosition, new Vector2(200, 90)));
        }

        public void RefreshSummary()
        {
            var s = Node.GetSummary(null);
            _summary.text = string.IsNullOrEmpty(s) ? "" : s;
            _summary.style.display = string.IsNullOrEmpty(s) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void SetEntry(bool isEntry) =>
            _entryBadge.style.display = isEntry ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Show/hide the branch-validation warning badge (⚠) with an explanatory tooltip.</summary>
        public void SetWarning(bool warn, string tooltipText)
        {
            _warnBadge.style.display = warn ? DisplayStyle.Flex : DisplayStyle.None;
            _warnBadge.tooltip = warn ? tooltipText : null;
        }

        public override void OnSelected()
        {
            base.OnSelected();
            NodeSelected?.Invoke(this);
        }

        /// <summary>Animated "currently executing" indicator.</summary>
        public void SetActive(bool active)
        {
            _active = active;
            if (active)
            {
                if (_pulse == null)
                {
                    _pulseStart = EditorApplication.timeSinceStartup;
                    _pulse = schedule.Execute(() =>
                    {
                        float t = (float)(EditorApplication.timeSinceStartup - _pulseStart);
                        float a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t * 6f));
                        SetBorder(2.5f, new Color(1f, 0.78f, 0.2f, a));
                    }).Every(33);
                }
            }
            else
            {
                _pulse?.Pause();
                _pulse = null;
                ApplyVisitedStyle();
            }
        }

        /// <summary>Marks this node as already executed in the current run.</summary>
        public void SetVisited(bool visited)
        {
            _visited = visited;
            _visitedBadge.style.display = visited ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_active) ApplyVisitedStyle();
        }

        public void ResetDebug()
        {
            SetActive(false);
            SetVisited(false);
        }

        private void ApplyVisitedStyle()
        {
            if (_visited)
            {
                style.borderTopWidth = style.borderRightWidth = style.borderBottomWidth = 0;
                style.borderLeftWidth = 3;
                var g = new StyleColor(new Color(0.35f, 0.8f, 0.4f));
                style.borderLeftColor = g;
            }
            else
            {
                SetBorder(0f, Color.clear);
            }
        }

        private void SetBorder(float w, Color c)
        {
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = w;
            var sc = new StyleColor(c);
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = sc;
        }

        public static SerializedProperty FindNodeProperty(SerializedObject serializedGraph, string nodeId)
        {
            var nodes = serializedGraph.FindProperty("nodes");
            if (nodes == null) return null;
            for (int i = 0; i < nodes.arraySize; i++)
            {
                var el = nodes.GetArrayElementAtIndex(i);
                var idProp = el.FindPropertyRelative("id");
                if (idProp != null && idProp.stringValue == nodeId)
                    return el;
            }
            return null;
        }

        // Vertical-mode port strips: a centered row of connectors along the node's top / bottom edge.
        private VisualElement TopStrip()
        {
            if (_topStrip == null)
            {
                _topStrip = MakeStrip("tk-top-ports");
                mainContainer.Insert(0, _topStrip);
            }
            return _topStrip;
        }

        private VisualElement BottomStrip()
        {
            if (_bottomStrip == null)
            {
                _bottomStrip = MakeStrip("tk-bottom-ports");
                mainContainer.Add(_bottomStrip);
            }
            return _bottomStrip;
        }

        private static VisualElement MakeStrip(string name) => new VisualElement
        {
            name = name,
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.Center,
                alignItems = Align.Center,
            },
        };

        // A tinted header: blend the accent toward the dark node body so it reads as a coloured banner.
        private static Color Dim(Color c) => Color.Lerp(c, new Color(0.17f, 0.18f, 0.21f), 0.42f);
    }
}
