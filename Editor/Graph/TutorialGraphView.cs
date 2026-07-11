using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TutorialKit.Editor
{
    /// <summary>
    /// GraphView surface: minimap, blackboard, groups, copy/paste, undo-friendly edits, auto-layout,
    /// multi-connection ports, an animated live indicator, and a selection-driven inspector.
    /// </summary>
    public sealed class TutorialGraphView : GraphView, NodeSearchProvider.EditorWindowHost
    {
        private const string ClipboardMarker = "TutorialKitClipboard::";

        public TutorialGraph Graph { get; private set; }
        public SerializedObject SerializedGraph { get; private set; }
        public NodeInspectorView Inspector { get; set; }

        private readonly EditorWindow _window;
        private readonly NodeSearchProvider _searchProvider;
        private readonly Dictionary<string, TutorialNodeView> _nodeViews = new Dictionary<string, TutorialNodeView>();
        private readonly TutorialBlackboardPanel _blackboard;
        private readonly MiniMap _minimap;
        private bool _loading;
        private bool _minimapPlaced;
        private string _activeNodeId;

        private const string DefaultVerticalPrefKey = "TutorialKit.Graph.DefaultVertical";
        private bool _vertical;

        /// <summary>Project default flow direction for graphs left on <c>UseDefault</c> (a Settings toggle).</summary>
        public static bool DefaultVertical
        {
            get => EditorPrefs.GetBool(DefaultVerticalPrefKey, false);
            set => EditorPrefs.SetBool(DefaultVerticalPrefKey, value);
        }

        /// <summary>Whether the loaded graph currently flows top→bottom (VFX-style) instead of left→right.</summary>
        public bool Vertical => _vertical;

        // A graph's own preference wins; UseDefault falls back to the project setting.
        private static bool ResolveVertical(TutorialGraph g)
        {
            if (g == null) return DefaultVertical;
            switch (g.LayoutDirection)
            {
                case TutorialLayoutDirection.Horizontal: return false;
                case TutorialLayoutDirection.Vertical: return true;
                default: return DefaultVertical;
            }
        }

        /// <summary>Set the CURRENT graph's preferred flow direction (saved on the asset) and re-lay out.</summary>
        public void SetVertical(bool vertical)
        {
            if (Graph == null) return;
            var dir = vertical ? TutorialLayoutDirection.Vertical : TutorialLayoutDirection.Horizontal;
            if (Graph.LayoutDirection == dir && _vertical == vertical) return;
            Undo.RegisterCompleteObjectUndo(Graph, "Set Layout Direction");
            Graph.LayoutDirection = dir;
            EditorUtility.SetDirty(Graph);
            _vertical = vertical;
            Reload();
            AutoLayout();
        }

        public TutorialGraphView(EditorWindow window)
        {
            _window = window;
            style.flexGrow = 1;

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.kobapps.tutorialkit/Editor/Graph/TutorialGraph.uss");
            if (ss != null) styleSheets.Add(ss);

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ClickSelector());

            var grid = new DotGridBackground(this) { name = "grid" };
            Insert(0, grid);
            grid.StretchToParentSize();

            _minimap = new MiniMap { anchored = false };
            _minimap.SetPosition(new Rect(12, 340, 200, 140));
            Add(_minimap);

            _blackboard = new TutorialBlackboardPanel();
            Add(_blackboard);

            _searchProvider = ScriptableObject.CreateInstance<NodeSearchProvider>();
            _searchProvider.Configure(this, this);
            nodeCreationRequest = ctx => SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), _searchProvider);

            graphViewChanged = OnGraphViewChanged;
            serializeGraphElements = OnCopy;
            canPasteSerializedData = data => data != null && data.StartsWith(ClipboardMarker, StringComparison.Ordinal);
            unserializeAndPaste = OnPaste;
            elementsAddedToGroup = (g, e) => ScheduleSaveGroups();
            elementsRemovedFromGroup = (g, e) => ScheduleSaveGroups();
            groupTitleChanged = (g, t) => ScheduleSaveGroups();

            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (!_minimapPlaced && layout.height > 20)
                {
                    _minimap.SetPosition(new Rect(12, layout.height - 155, 200, 140));
                    _minimapPlaced = true;
                }
            });
        }

        /// <summary>
        /// A soft dot-grid canvas background. Renders faint dots as a repeating background image (GPU
        /// tiled — no mesh geometry, so it never hits the vertex limit) that pans and zooms with the
        /// graph via the view transform. Theme-aware; purely decorative.
        /// </summary>
        private sealed class DotGridBackground : VisualElement
        {
            private readonly GraphView _view;
            private const float BaseSpacing = 26f;

            public DotGridBackground(GraphView view)
            {
                _view = view;
                pickingMode = PickingMode.Ignore;
                this.StretchToParentSize();

                bool dark = EditorGUIUtility.isProSkin;
                style.backgroundColor = dark ? new Color(0.16f, 0.165f, 0.19f) : new Color(0.76f, 0.77f, 0.79f);
                var dotColor = dark ? new Color(1f, 1f, 1f, 0.10f) : new Color(0f, 0f, 0f, 0.11f);

                style.backgroundImage = new StyleBackground(BuildDotTile(dotColor));
                style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat));

                _view.viewTransformChanged += _ => UpdateTiling();
                RegisterCallback<GeometryChangedEvent>(_ => UpdateTiling());
                UpdateTiling();
            }

            // Match the tile size + offset to the graph's zoom/pan so the dots track the nodes.
            private void UpdateTiling()
            {
                if (_view == null) return;
                var t = _view.viewTransform;
                float scale = Mathf.Max(0.0001f, t.scale.x);
                float size = BaseSpacing * scale;
                style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(new Length(size), new Length(size)));
                style.backgroundPositionX = new StyleBackgroundPosition(
                    new BackgroundPosition(BackgroundPositionKeyword.Left, new Length(Mathf.Repeat(t.position.x, size))));
                style.backgroundPositionY = new StyleBackgroundPosition(
                    new BackgroundPosition(BackgroundPositionKeyword.Top, new Length(Mathf.Repeat(t.position.y, size))));
            }

            // A small tile holding one soft (anti-aliased) dot in the centre; tiled to make the grid.
            private static Texture2D BuildDotTile(Color dot)
            {
                const int s = 32;
                float radius = 1.7f;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                };
                var px = new Color[s * s];
                var c = new Vector2(s * 0.5f, s * 0.5f);
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) - radius;
                    float cov = Mathf.Clamp01(0.5f - d); // signed-distance anti-aliasing
                    px[y * s + x] = new Color(dot.r, dot.g, dot.b, dot.a * cov);
                }
                tex.SetPixels(px);
                tex.Apply();
                return tex;
            }
        }

        // ---- Loading ----

        public void LoadGraph(TutorialGraph graph)
        {
            _loading = true;
            _nodeViews.Clear();
            // Keep floating panels; delete graph content only.
            var toRemove = new List<GraphElement>();
            foreach (var el in graphElements)
                if (el is TutorialNodeView || el is Edge || el is Group) toRemove.Add(el);
            foreach (var el in toRemove) RemoveElement(el);

            Graph = graph;
            _vertical = ResolveVertical(graph); // must be set before node views are built (drives port layout)
            if (graph != null)
            {
                graph.Validate();
                bool req = EnsureStartNode(graph);
                req |= EnsureEndNode(graph);
                if (req) EditorUtility.SetDirty(graph);
                SerializedGraph = new SerializedObject(graph);

                foreach (var node in graph.Nodes)
                    if (node != null) CreateNodeView(node);
                foreach (var edge in graph.Edges)
                    ConnectViews(edge);
                foreach (var de in graph.DataEdges)
                    ConnectDataEdge(de);

                LoadGroups();
                RefreshEntryBadges();
                MarkValidation();
                if (_activeNodeId != null) HighlightActiveNode(_activeNodeId);
                this.Bind(SerializedGraph);
            }
            _blackboard.SetGraph(graph);
            Inspector?.Clear();
            _loading = false;
        }

        public void Reload()
        {
            if (Graph != null) LoadGraph(Graph);
        }

        private TutorialNodeView CreateNodeView(TutorialNode node)
        {
            var info = NodeTypeRegistry.Get(node.GetType());
            var view = new TutorialNodeView(node, SerializedGraph, info, _vertical);
            view.NodeSelected += OnNodeSelected;
            AddElement(view);
            _nodeViews[node.Id] = view;
            return view;
        }

        private void OnNodeSelected(TutorialNodeView view)
        {
            Inspector?.ShowNode(view.Node, SerializedGraph, view.RefreshSummary);
        }

        private void ConnectViews(TutorialEdge edge)
        {
            if (!_nodeViews.TryGetValue(edge.FromNodeId, out var fromView)) return;
            if (!_nodeViews.TryGetValue(edge.ToNodeId, out var toView)) return;
            if (!fromView.OutputPorts.TryGetValue(edge.FromPort, out var outPort)) return;
            if (toView.InputPort == null) return;

            var e = outPort.ConnectTo(toView.InputPort);
            AddElement(e);
        }

        private void ConnectDataEdge(TutorialDataEdge de)
        {
            if (!_nodeViews.TryGetValue(de.FromNodeId, out var fromView)) return;
            if (!_nodeViews.TryGetValue(de.ToNodeId, out var toView)) return;
            if (!fromView.DataOutputPorts.TryGetValue(de.FromPort, out var outPort)) return;
            if (!toView.DataInputPorts.TryGetValue(de.ToPort, out var inPort)) return;

            var e = outPort.ConnectTo(inPort);
            AddElement(e);
        }

        private static bool IsDataPort(Port p) => p != null && p.portType != typeof(bool);

        private void LoadGroups()
        {
            if (Graph.Groups == null) return;
            foreach (var data in Graph.Groups)
            {
                if (data == null) continue;
                var group = new Group { title = data.Title };
                AddElement(group);
                if (data.NodeIds != null)
                    foreach (var id in data.NodeIds)
                        if (_nodeViews.TryGetValue(id, out var v))
                            group.AddElement(v);
            }
        }

        // ---- Authoring ----

        /// <summary>
        /// Guarantees the graph has exactly one <see cref="StartNode"/> as its entry — adds one (wired
        /// to whatever ran first before) if missing, and points <c>EntryNodeId</c> at it. Returns true
        /// if the graph changed. This is the per-graph "required Start node" invariant.
        /// </summary>
        public static bool EnsureStartNode(TutorialGraph graph)
        {
            if (graph == null) return false;

            StartNode start = null;
            foreach (var n in graph.Nodes) if (n is StartNode s) { start = s; break; }
            if (start != null)
            {
                if (graph.EntryNodeId == start.Id) return false;
                graph.EntryNodeId = start.Id;
                return true;
            }

            var oldEntry = graph.EntryNode; // the node that ran first before a Start existed
            start = new StartNode();
            start.EnsureId();
            start.EditorPosition = oldEntry != null ? oldEntry.EditorPosition + new Vector2(-300f, 0f) : Vector2.zero;
            graph.AddNode(start);
            if (oldEntry != null && oldEntry.Id != start.Id)
                graph.AddEdge(start.Id, TutorialNode.OutPort, oldEntry.Id);
            graph.EntryNodeId = start.Id;
            return true;
        }

        /// <summary>
        /// Guarantees the graph has at least one <see cref="EndNode"/>: if it has none, adds one and
        /// wires every loose flow output to it. Graphs may have several End nodes (one per terminating
        /// branch) — those are left untouched. Returns true if the graph changed.
        /// </summary>
        public static bool EnsureEndNode(TutorialGraph graph)
        {
            if (graph == null) return false;
            foreach (var n in graph.Nodes) if (n is EndNode) return false; // already has at least one

            var end = new EndNode();
            end.EnsureId();
            end.EditorPosition = FarRightPosition(graph);
            graph.AddNode(end);
            // Terminate every loose flow output at the new End so the graph is complete out of the box.
            foreach (var n in graph.Nodes)
            {
                if (n == null || n is EndNode) continue;
                foreach (var port in n.OutputPorts)
                {
                    bool hasNext = false;
                    foreach (var _ in graph.GetNextNodeIds(n.Id, port)) { hasNext = true; break; }
                    if (!hasNext) graph.AddEdge(n.Id, port, end.Id);
                }
            }
            return true;
        }

        /// <summary>
        /// Editor validation: flow outputs that lead nowhere (a branch that stops without reaching an
        /// End node). Returns the offending (nodeId, port) pairs — each is a branch missing an End.
        /// </summary>
        public List<(string nodeId, string port)> FindUnterminatedBranches()
        {
            var result = new List<(string, string)>();
            if (Graph == null) return result;
            foreach (var n in Graph.Nodes)
            {
                if (n == null || n is EndNode) continue;
                foreach (var port in n.OutputPorts)
                {
                    bool hasNext = false;
                    foreach (var _ in Graph.GetNextNodeIds(n.Id, port)) { hasNext = true; break; }
                    if (!hasNext) result.Add((n.Id, port));
                }
            }
            return result;
        }

        /// <summary>Re-runs branch validation and flags offending node views with a warning badge.</summary>
        public void MarkValidation()
        {
            var bad = new Dictionary<string, List<string>>();
            foreach (var (nodeId, port) in FindUnterminatedBranches())
            {
                if (!bad.TryGetValue(nodeId, out var ports)) { ports = new List<string>(); bad[nodeId] = ports; }
                ports.Add(port);
            }
            foreach (var kv in _nodeViews)
            {
                if (bad.TryGetValue(kv.Key, out var ports))
                {
                    string which = ports.Count == 1 && ports[0] != TutorialNode.OutPort ? $"'{ports[0]}' output" : "output";
                    kv.Value.SetWarning(true, $"This branch doesn't reach an End node — connect its {which} to an End.");
                }
                else kv.Value.SetWarning(false, null);
            }
        }

        private static Vector2 FarRightPosition(TutorialGraph graph)
        {
            bool any = false; float maxX = 0f, yAtMax = 0f;
            foreach (var n in graph.Nodes)
            {
                if (n == null || n is EndNode) continue;
                var p = n.EditorPosition;
                if (!any || p.x > maxX) { maxX = p.x; yAtMax = p.y; any = true; }
            }
            return any ? new Vector2(maxX + 300f, yAtMax) : new Vector2(300f, 0f);
        }

        public void CreateNode(NodeTypeInfo info, Vector2 graphPosition)
        {
            if (Graph == null || info == null) return;

            Undo.RegisterCompleteObjectUndo(Graph, "Add Tutorial Node");
            var node = info.CreateInstance();
            node.EditorPosition = graphPosition;
            Graph.AddNode(node);
            EditorUtility.SetDirty(Graph);
            SerializedGraph.Update();

            CreateNodeView(node);
            RefreshEntryBadges();
            MarkValidation();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_loading || Graph == null) return change;

            bool structural = false;
            bool groupsDirty = false;

            if (change.movedElements != null)
            {
                Undo.RegisterCompleteObjectUndo(Graph, "Move Tutorial Node");
                foreach (var el in change.movedElements)
                    if (el is TutorialNodeView v)
                        v.Node.EditorPosition = v.GetPosition().position;
                EditorUtility.SetDirty(Graph);
            }

            if (change.edgesToCreate != null)
            {
                Undo.RegisterCompleteObjectUndo(Graph, "Connect Tutorial Nodes");
                foreach (var edge in change.edgesToCreate)
                    if (edge.output.node is TutorialNodeView from && edge.input.node is TutorialNodeView to)
                    {
                        if (IsDataPort(edge.output))
                            Graph.AddDataEdge(from.Node.Id, edge.output.portName, to.Node.Id, edge.input.portName);
                        else
                            Graph.AddEdge(from.Node.Id, edge.output.portName, to.Node.Id);
                    }
                EditorUtility.SetDirty(Graph);
            }

            if (change.elementsToRemove != null)
            {
                // The Start node is mandatory — never let it be deleted. End nodes can be freely removed
                // (branch validation flags any branch left without one).
                change.elementsToRemove.RemoveAll(el => el is TutorialNodeView sv && sv.Node is StartNode);
                foreach (var el in change.elementsToRemove)
                {
                    switch (el)
                    {
                        case Edge edge when edge.output?.node is TutorialNodeView from && edge.input?.node is TutorialNodeView to:
                            Undo.RegisterCompleteObjectUndo(Graph, "Disconnect Tutorial Nodes");
                            if (IsDataPort(edge.output))
                                Graph.RemoveDataEdge(from.Node.Id, edge.output.portName, to.Node.Id, edge.input.portName);
                            else
                                Graph.RemoveEdge(from.Node.Id, edge.output.portName, to.Node.Id);
                            EditorUtility.SetDirty(Graph);
                            break;
                        case TutorialNodeView view:
                            Undo.RegisterCompleteObjectUndo(Graph, "Delete Tutorial Node");
                            Graph.RemoveNode(view.Node.Id);
                            _nodeViews.Remove(view.Node.Id);
                            EditorUtility.SetDirty(Graph);
                            structural = true;
                            break;
                        case Group _:
                            groupsDirty = true;
                            break;
                    }
                }
            }

            if (groupsDirty) ScheduleSaveGroups();
            if (structural)
                EditorApplication.delayCall += () => { if (Graph != null) Reload(); };
            else if (Graph != null)
                MarkValidation(); // edge add/remove can change which branches reach an End

            return change;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            ports.ForEach(port =>
            {
                if (startPort == port) return;
                if (startPort.node == port.node) return;
                if (startPort.direction == port.direction) return;
                if (startPort.portType != port.portType) return; // flow↔flow, target↔target only
                compatible.Add(port);
            });
            return compatible;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            int nodeSel = 0;
            foreach (var s in selection) if (s is TutorialNodeView) nodeSel++;
            if (nodeSel > 0)
                evt.menu.AppendAction("Group Selection", _ => GroupSelection());

            evt.menu.AppendAction("Auto Layout", _ => AutoLayout());
            base.BuildContextualMenu(evt);
        }

        private void RefreshEntryBadges()
        {
            if (Graph == null) return;
            string entry = Graph.EntryNode != null ? Graph.EntryNode.Id : null;
            foreach (var kv in _nodeViews)
                kv.Value.SetEntry(kv.Key == entry);
        }

        // ---- Panels ----

        private bool _minimapHidden;
        private bool _blackboardHidden;

        public void ToggleMinimap()
        {
            _minimapHidden = !_minimapHidden;
            _minimap.style.display = _minimapHidden ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void ToggleBlackboard()
        {
            _blackboardHidden = !_blackboardHidden;
            _blackboard.style.display = _blackboardHidden ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ---- Groups ----

        private void GroupSelection()
        {
            var group = new Group { title = "Group" };
            AddElement(group);
            foreach (var s in selection)
                if (s is TutorialNodeView v)
                    group.AddElement(v);
            ScheduleSaveGroups();
        }

        private void ScheduleSaveGroups()
        {
            if (_loading) return;
            EditorApplication.delayCall += SaveGroups;
        }

        private void SaveGroups()
        {
            if (Graph == null) return;
            var list = new List<TutorialGroupData>();
            foreach (var el in graphElements)
            {
                if (el is Group grp)
                {
                    var data = new TutorialGroupData { Title = grp.title };
                    foreach (var contained in grp.containedElements)
                        if (contained is TutorialNodeView v)
                            data.NodeIds.Add(v.Node.Id);
                    list.Add(data);
                }
            }
            Undo.RegisterCompleteObjectUndo(Graph, "Edit Groups");
            Graph.SetGroups(list);
            EditorUtility.SetDirty(Graph);
        }

        // ---- Copy / paste ----

        [Serializable] private sealed class ClipNode { public string type; public string data; public string oldId; public Vector2 pos; }
        [Serializable] private sealed class ClipEdge { public string fromOld; public string port; public string toOld; }
        [Serializable] private sealed class Clipboard { public List<ClipNode> nodes = new List<ClipNode>(); public List<ClipEdge> edges = new List<ClipEdge>(); }

        private string OnCopy(IEnumerable<GraphElement> elements)
        {
            var clip = new Clipboard();
            var ids = new HashSet<string>();
            foreach (var el in elements)
                if (el is TutorialNodeView v)
                {
                    ids.Add(v.Node.Id);
                    clip.nodes.Add(new ClipNode
                    {
                        type = NodeTypeRegistry.GetTypeId(v.Node),
                        data = JsonUtility.ToJson(v.Node),
                        oldId = v.Node.Id,
                        pos = v.GetPosition().position,
                    });
                }
            if (Graph != null)
                foreach (var e in Graph.Edges)
                    if (ids.Contains(e.FromNodeId) && ids.Contains(e.ToNodeId))
                        clip.edges.Add(new ClipEdge { fromOld = e.FromNodeId, port = e.FromPort, toOld = e.ToNodeId });

            return ClipboardMarker + JsonUtility.ToJson(clip);
        }

        private void OnPaste(string operationName, string data)
        {
            if (Graph == null || !data.StartsWith(ClipboardMarker, StringComparison.Ordinal)) return;
            var clip = JsonUtility.FromJson<Clipboard>(data.Substring(ClipboardMarker.Length));
            if (clip == null) return;

            Undo.RegisterCompleteObjectUndo(Graph, "Paste Nodes");
            var map = new Dictionary<string, string>();
            var newIds = new List<string>();

            foreach (var cn in clip.nodes)
            {
                var node = NodeTypeRegistry.Create(cn.type);
                if (node == null) continue;
                if (node is StartNode) continue; // only one Start per graph — never paste another (End is fine)
                if (!string.IsNullOrEmpty(cn.data)) JsonUtility.FromJsonOverwrite(cn.data, node);
                string newId = Guid.NewGuid().ToString("N");
                node.Id = newId;
                node.EditorPosition = cn.pos + new Vector2(40f, 40f);
                map[cn.oldId] = newId;
                Graph.AddNode(node);
                newIds.Add(newId);
            }
            foreach (var ce in clip.edges)
                if (map.TryGetValue(ce.fromOld, out var f) && map.TryGetValue(ce.toOld, out var t))
                    Graph.AddEdge(f, ce.port, t);

            EditorUtility.SetDirty(Graph);
            Reload();

            ClearSelection();
            foreach (var id in newIds)
                if (_nodeViews.TryGetValue(id, out var v))
                    AddToSelection(v);
        }

        // ---- Auto layout (longest-path layering, left → right) ----

        // A value/provider node (e.g. a target node) has no flow ports — it's laid out beside its consumer.
        private static bool IsValueNode(TutorialNode n) => n != null && !n.HasInput && n.OutputPorts.Count == 0;

        public void AutoLayout()
        {
            if (Graph == null || Graph.Nodes.Count == 0) return;
            Undo.RegisterCompleteObjectUndo(Graph, "Auto Layout");

            bool vert = _vertical;
            const float laneGap = 300f;             // gutter before a consumer for its provider nodes
            const float provGap = 150f;             // vertical spacing between stacked providers
            float crossStep = vert ? 260f : 150f;   // spacing between siblings (X if vertical, else Y)
            float mainStep = vert ? 190f : 300f;    // spacing between depth layers (Y if vertical, else X)

            // 1. Longest-path depth assignment (flow nodes only).
            var col = new Dictionary<string, int>();
            var queue = new Queue<string>();
            string entry = Graph.EntryNode != null ? Graph.EntryNode.Id : Graph.Nodes[0].Id;
            col[entry] = 0;
            queue.Enqueue(entry);
            int guard = 0, maxGuard = Graph.Nodes.Count * Graph.Nodes.Count + 16;
            while (queue.Count > 0 && guard++ < maxGuard)
            {
                var cur = queue.Dequeue();
                var node = Graph.FindNode(cur);
                if (node == null) continue;
                foreach (var port in node.OutputPorts)
                    foreach (var next in Graph.GetNextNodeIds(cur, port))
                    {
                        int nc = col[cur] + 1;
                        if (!col.TryGetValue(next, out var existing) || nc > existing)
                        {
                            col[next] = nc;
                            queue.Enqueue(next);
                        }
                    }
            }
            int maxCol = 0;
            foreach (var kv in col) if (kv.Value > maxCol) maxCol = kv.Value;
            foreach (var n in Graph.Nodes) // orphan flow nodes → trailing column
                if (n != null && !IsValueNode(n) && !col.ContainsKey(n.Id)) col[n.Id] = maxCol + 1;
            foreach (var kv in col) if (kv.Value > maxCol) maxCol = kv.Value;

            // 2. Flow parents (for barycenter Y).
            var parents = new Dictionary<string, List<string>>();
            foreach (var e in Graph.Edges)
            {
                if (!parents.TryGetValue(e.ToNodeId, out var list)) { list = new List<string>(); parents[e.ToNodeId] = list; }
                list.Add(e.FromNodeId);
            }

            // 3. Which columns need a provider lane (extra width only where actually needed).
            var colHasProviders = new HashSet<int>();
            foreach (var de in Graph.DataEdges)
            {
                var producer = Graph.FindNode(de.FromNodeId);
                if (producer != null && IsValueNode(producer) && col.TryGetValue(de.ToNodeId, out var c))
                    colHasProviders.Add(c);
            }

            // 4. Depth positions. Horizontal reserves a gutter before provider columns; vertical keeps
            //    depth uniform (providers sit to the side, on the cross axis, in both orientations).
            var depthPos = new Dictionary<int, float>();
            if (vert)
            {
                for (int c = 0; c <= maxCol; c++) depthPos[c] = c * mainStep;
            }
            else
            {
                float cursor = 0f;
                for (int c = 0; c <= maxCol; c++)
                {
                    if (colHasProviders.Contains(c)) cursor += laneGap;
                    depthPos[c] = cursor;
                    cursor += mainStep;
                }
            }

            // 5. Barycenter cross placement, layer by layer (aligns chains, spreads branches, centres joins).
            var pos = new Dictionary<string, Vector2>();
            var crossOf = new Dictionary<string, float>();
            var byCol = new Dictionary<int, List<string>>();
            foreach (var n in Graph.Nodes)
            {
                if (n == null || IsValueNode(n)) continue;
                int c = col[n.Id];
                if (!byCol.TryGetValue(c, out var list)) { list = new List<string>(); byCol[c] = list; }
                list.Add(n.Id);
            }
            for (int c = 0; c <= maxCol; c++)
            {
                if (!byCol.TryGetValue(c, out var ids)) continue;
                var desired = new Dictionary<string, float>();
                foreach (var id in ids)
                {
                    float sum = 0f; int cnt = 0;
                    if (parents.TryGetValue(id, out var ps))
                        foreach (var p in ps) if (crossOf.TryGetValue(p, out var pc)) { sum += pc; cnt++; }
                    desired[id] = cnt > 0 ? sum / cnt : 0f;
                }
                ids.Sort((a, b) => desired[a].CompareTo(desired[b]));
                float prev = float.NegativeInfinity;
                foreach (var id in ids)
                {
                    float cc = Mathf.Max(desired[id], prev + crossStep);
                    crossOf[id] = cc;
                    prev = cc;
                    pos[id] = vert ? new Vector2(cc, depthPos[c]) : new Vector2(depthPos[c], cc);
                }
            }
            float graphBottom = 0f; foreach (var kv in pos) if (kv.Value.y > graphBottom) graphBottom = kv.Value.y;

            // 6. Providers sit to the LEFT of their consumer (data ports stay on the node's left edge in
            //    both orientations), stacked downward and ordered by the consumer's input-port order, so
            //    each data edge rises cleanly to its port instead of routing under a node.
            var providersByConsumer = new Dictionary<string, List<(string id, int portIdx)>>();
            var dangling = new List<string>();
            foreach (var n in Graph.Nodes)
            {
                if (n == null || !IsValueNode(n)) continue;
                string consumerId = null, toPort = null;
                foreach (var de in Graph.DataEdges)
                    if (de.FromNodeId == n.Id) { consumerId = de.ToNodeId; toPort = de.ToPort; break; }

                if (consumerId != null && pos.ContainsKey(consumerId))
                {
                    if (!providersByConsumer.TryGetValue(consumerId, out var list))
                    { list = new List<(string, int)>(); providersByConsumer[consumerId] = list; }
                    list.Add((n.Id, PortIndex(consumerId, toPort)));
                }
                else dangling.Add(n.Id);
            }

            // Pack gutters left-to-right, top-to-bottom so providers sharing a gutter stay collision-free.
            var laneUsed = new Dictionary<int, HashSet<int>>();
            var consumerOrder = new List<string>(providersByConsumer.Keys);
            consumerOrder.Sort((a, b) =>
            {
                Vector2 pa = pos[a], pb = pos[b];
                return Mathf.Abs(pa.x - pb.x) > 0.5f ? pa.x.CompareTo(pb.x) : pa.y.CompareTo(pb.y);
            });
            foreach (var consumerId in consumerOrder)
            {
                Vector2 cp = pos[consumerId];
                float laneX = cp.x - laneGap;
                int laneKey = Mathf.RoundToInt(laneX / 4f);
                if (!laneUsed.TryGetValue(laneKey, out var used)) { used = new HashSet<int>(); laneUsed[laneKey] = used; }
                var list = providersByConsumer[consumerId];
                list.Sort((a, b) => a.portIdx.CompareTo(b.portIdx));
                int bucket = Mathf.RoundToInt(cp.y / provGap) + 1; // start one row below the consumer
                foreach (var (id, _) in list)
                {
                    while (used.Contains(bucket)) bucket++;
                    used.Add(bucket);
                    pos[id] = new Vector2(laneX, bucket * provGap);
                    bucket++;
                }
            }

            float danglingY = graphBottom + provGap * 2f;
            foreach (var id in dangling)
            {
                pos[id] = new Vector2(0f, danglingY);
                danglingY += provGap * 0.9f;
            }

            // 7. Apply.
            foreach (var n in Graph.Nodes)
            {
                if (n == null || !pos.TryGetValue(n.Id, out var p)) continue;
                n.EditorPosition = p;
                if (_nodeViews.TryGetValue(n.Id, out var v))
                    v.SetPosition(new Rect(p, new Vector2(200, 90)));
            }

            EditorUtility.SetDirty(Graph);
            EditorApplication.delayCall += () => FrameAll();
        }

        // Index of an input data port on a node (for ordering providers to match port layout).
        private int PortIndex(string nodeId, string portName)
        {
            var node = Graph.FindNode(nodeId);
            if (node != null && portName != null)
                for (int i = 0; i < node.InputDataPorts.Count; i++)
                    if (node.InputDataPorts[i].Name == portName) return i;
            return int.MaxValue;
        }

        // ---- Live debugger ----

        private readonly HashSet<Edge> _traversedEdges = new HashSet<Edge>();

        public void HighlightActiveNode(string nodeId)
        {
            if (_activeNodeId != null && _activeNodeId != nodeId && _nodeViews.TryGetValue(_activeNodeId, out var prev))
                prev.SetVisited(true);
            _activeNodeId = nodeId;
            foreach (var kv in _nodeViews)
                kv.Value.SetActive(kv.Key == nodeId);
            if (nodeId != null && _nodeViews.TryGetValue(nodeId, out var cur))
                cur.SetVisited(true);
        }

        /// <summary>Colours the edge(s) the run just traversed.</summary>
        public void MarkTraversedEdge(string fromId, string port, string toId)
        {
            edges.ForEach(e =>
            {
                if (e.output?.node is TutorialNodeView f && f.Node.Id == fromId && e.output.portName == port
                    && e.input?.node is TutorialNodeView t && t.Node.Id == toId)
                {
                    SetEdgeColor(e, new Color(1f, 0.78f, 0.2f));
                    _traversedEdges.Add(e);
                }
            });
        }

        /// <summary>Stops the active pulse but keeps the visited trail (tutorial finished).</summary>
        public void StopActive()
        {
            if (_activeNodeId != null && _nodeViews.TryGetValue(_activeNodeId, out var v))
            {
                v.SetActive(false);
                v.SetVisited(true);
            }
            _activeNodeId = null;
        }

        /// <summary>Clears all live-debug visuals (call at the start of a new run).</summary>
        public void ResetDebugStates()
        {
            _activeNodeId = null;
            foreach (var kv in _nodeViews) kv.Value.ResetDebug();
            foreach (var e in _traversedEdges) SetEdgeColor(e, new Color(0.55f, 0.55f, 0.55f));
            _traversedEdges.Clear();
        }

        public void ClearActiveNode() => StopActive();

        private static void SetEdgeColor(Edge e, Color c)
        {
            if (e?.edgeControl == null) return;
            e.edgeControl.inputColor = c;
            e.edgeControl.outputColor = c;
            e.edgeControl.MarkDirtyRepaint();
        }

        public Vector2 ScreenToGraphPosition(Vector2 screenPosition)
        {
            var windowLocal = screenPosition - _window.position.position;
            return contentViewContainer.WorldToLocal(windowLocal);
        }
    }
}
