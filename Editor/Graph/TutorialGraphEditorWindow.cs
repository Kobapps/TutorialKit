using System;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TutorialKit.Editor
{
    /// <summary>
    /// GraphView authoring window: browse tutorials, edit nodes/connections with a minimap,
    /// blackboard, groups, inspector, auto-layout, and a live play indicator.
    /// </summary>
    public sealed class TutorialGraphEditorWindow : EditorWindow
    {
        private TutorialGraphView _graphView;
        private NodeInspectorView _inspector;
        private TutorialGraph _graph;
        private Label _statusLabel;
        private ToolbarToggle _verticalToggle;
        private bool _subscribed;
        private bool _attach = true;

        [MenuItem("Window/TutorialKit/Tutorial Graph Editor")]
        public static void ShowWindow()
        {
            var w = GetWindow<TutorialGraphEditorWindow>();
            w.titleContent = new GUIContent("Tutorial Graph");
            w.minSize = new Vector2(820, 460);
        }

        public static void Open(TutorialGraph graph)
        {
            var w = GetWindow<TutorialGraphEditorWindow>();
            w.titleContent = new GUIContent("Tutorial Graph");
            w.OpenGraph(graph);
            w.Focus();
        }

        /// <summary>
        /// Opens (or focuses) the window on a tutorial that just started playing and attaches the live
        /// debugger to it. Called by <see cref="TutorialGraphAutoOpen"/> when a tutorial begins.
        /// </summary>
        public static void ShowLive(TutorialHandle handle)
        {
            if (handle?.Graph == null) return;
            var w = GetWindow<TutorialGraphEditorWindow>();
            w.titleContent = new GUIContent("Tutorial Graph");
            w._attach = true;
            w.OpenGraph(handle.Graph);
            w.Attach(handle);
            w.TrySubscribeDebug(); // subscribe immediately so node transitions update live
            w.Focus();
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            var obj = EditorUtility.EntityIdToObject(instanceId);
            if (obj is TutorialGraph g) { Open(g); return true; }
            return false;
        }

        private void OnEnable()
        {
            rootVisualElement.Clear();

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.tutorialkit/Editor/Graph/TutorialGraph.uss");
            if (ss != null) rootVisualElement.styleSheets.Add(ss);

            BuildToolbar();

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            _graphView = new TutorialGraphView(this) { style = { flexGrow = 1 } };
            _inspector = new NodeInspectorView();
            _graphView.Inspector = _inspector;
            row.Add(_graphView);
            row.Add(_inspector);
            rootVisualElement.Add(row);

            if (_graph != null) OpenGraph(_graph);

            rootVisualElement.schedule.Execute(TrySubscribeDebug).Every(500);
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            UnsubscribeDebug();
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo() => _graphView?.Reload();

        private void BuildToolbar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("tk-ribbon");

            // ---- Manage: pick / save / run a tutorial ----
            var manage = Section(bar, "Manage");
            var menu = new ToolbarMenu { text = "Tutorials" };
            foreach (var guid in AssetDatabase.FindAssets("t:TutorialGraph"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                menu.menu.AppendAction(name, _ =>
                {
                    var g = AssetDatabase.LoadAssetAtPath<TutorialGraph>(path);
                    if (g != null) OpenGraph(g);
                });
            }
            manage.Add(menu);
            manage.Add(IconButton("Save", "SaveActive", "Save all assets (Ctrl+S)",
                () => { if (_graph != null) AssetDatabase.SaveAssets(); }));
            manage.Add(IconButton("Test", "PlayButton", "Enter Play mode and run this tutorial", TestInPlay));

            // ---- Layout: arrange & orient the graph ----
            var layout = Section(bar, "Layout");
            layout.Add(IconButton("Auto Layout", "ScaleTool", "Tidy the graph (respects the layout direction)",
                () => _graphView?.AutoLayout()));
            layout.Add(IconButton("Frame", "ViewToolZoom", "Frame all nodes (A)", () => _graphView?.FrameAll()));
            _verticalToggle = new ToolbarToggle
            {
                text = "Vertical",
                value = TutorialGraphView.DefaultVertical,
                tooltip = "Flow THIS graph top → bottom (VFX-graph style). Saved on the graph; new graphs " +
                          "follow the project default in Settings.",
            };
            _verticalToggle.RegisterValueChangedCallback(e =>
            {
                _graphView?.SetVertical(e.newValue);
                if (_graphView != null) _verticalToggle.SetValueWithoutNotify(_graphView.Vertical);
            });
            layout.Add(_verticalToggle);

            // ---- View: panels & live-attach ----
            var view = Section(bar, "View");
            view.Add(new ToolbarButton(() => _graphView?.ToggleBlackboard()) { text = "Blackboard" });
            view.Add(new ToolbarButton(() => _graphView?.ToggleMinimap()) { text = "Minimap" });
            var attachToggle = new ToolbarToggle { text = "Attach", value = _attach, tooltip = "Auto-follow the running tutorial in Play mode" };
            attachToggle.RegisterValueChangedCallback(e => _attach = e.newValue);
            view.Add(attachToggle);

            // ---- Info: status (stretches to fill) ----
            var info = Section(bar, "Info");
            var infoWrap = info.parent;
            infoWrap.style.flexGrow = 1;
            info.style.flexGrow = 1;
            _statusLabel = new Label(" No tutorial loaded ");
            _statusLabel.AddToClassList("tk-ribbon-status");
            info.Add(_statusLabel);

            // ---- Settings (far right) ----
            var settings = Section(bar, "");
            settings.Add(IconButton("Settings", "Settings", "TutorialKit settings & AI skill",
                TutorialKitSettingsWindow.ShowWindow));

            rootVisualElement.Add(bar);
        }

        // A ribbon section: a row of controls with a small caption beneath, preceded by a divider.
        private static VisualElement Section(VisualElement bar, string title)
        {
            if (bar.childCount > 0)
            {
                var sep = new VisualElement();
                sep.AddToClassList("tk-ribbon-sep");
                bar.Add(sep);
            }
            var wrap = new VisualElement();
            wrap.AddToClassList("tk-ribbon-section");
            var row = new VisualElement();
            row.AddToClassList("tk-ribbon-row");
            wrap.Add(row);
            var label = new Label(title ?? "");
            label.AddToClassList("tk-ribbon-label");
            if (string.IsNullOrEmpty(title)) label.style.visibility = Visibility.Hidden;
            wrap.Add(label);
            bar.Add(wrap);
            return row;
        }

        private static ToolbarButton IconButton(string text, string iconName, string tooltip, Action onClick)
        {
            var b = new ToolbarButton(onClick) { tooltip = tooltip };
            b.AddToClassList("tk-toolbar-btn");
            GUIContent content = null;
            try { content = EditorGUIUtility.IconContent(iconName); } catch { }
            if (content != null && content.image != null)
                b.Add(new Image { image = content.image });
            b.Add(new Label(text));
            return b;
        }

        public void OpenGraph(TutorialGraph graph)
        {
            _graph = graph;
            if (_graphView == null) return;
            _graphView.LoadGraph(graph);
            _graphView.FrameAll();
            _verticalToggle?.SetValueWithoutNotify(_graphView.Vertical); // reflect this graph's saved direction
            if (_statusLabel != null)
                _statusLabel.text = graph != null ? $" {graph.DisplayName}  ({graph.Nodes.Count} nodes) " : " No tutorial loaded ";
        }

        private void TestInPlay()
        {
            if (_graph == null) return;
            var path = AssetDatabase.GetAssetPath(_graph);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                EditorUtility.DisplayDialog("TutorialKit", "Save the tutorial as an asset before testing.", "OK");
                return;
            }
            SessionState.SetString(TutorialTestLauncher.PendingGuidKey, guid);
            if (!EditorApplication.isPlaying)
                EditorApplication.isPlaying = true;
            else
                TutorialTestLauncher.PlayPending();
        }

        // ---- Live debugger (attaches to and visualises the running tutorial) ----

        private void TrySubscribeDebug()
        {
            if (!EditorApplication.isPlaying)
            {
                UnsubscribeDebug();
                _graphView?.ResetDebugStates();
                SetNormalStatus();
                return;
            }

            var dir = TutorialDirector.Current;
            if (dir == null) return;

            if (!_subscribed)
            {
                dir.Started += OnStarted;
                dir.NodeEntered += OnNodeEntered;
                dir.NodeExited += OnNodeExited;
                dir.Finished += OnFinished;
                _subscribed = true;
            }

            // Attach if a tutorial is already running (e.g. the editor was opened mid-run).
            var handle = dir.ActiveHandle;
            if (_attach && handle != null && handle.IsRunning && handle.Graph != _graph)
                Attach(handle);
        }

        private void UnsubscribeDebug()
        {
            var dir = TutorialDirector.Current;
            if (_subscribed && dir != null)
            {
                dir.Started -= OnStarted;
                dir.NodeEntered -= OnNodeEntered;
                dir.NodeExited -= OnNodeExited;
                dir.Finished -= OnFinished;
            }
            _subscribed = false;
        }

        private void Attach(TutorialHandle handle)
        {
            if (handle?.Graph == null) return;
            if (handle.Graph != _graph) OpenGraph(handle.Graph);
            _graphView?.ResetDebugStates();
            if (handle.CurrentNode != null)
                _graphView?.HighlightActiveNode(handle.CurrentNode.Id);
            SetDebugStatus(handle.CurrentNode != null ? handle.CurrentNode.DisplayName : "running");
        }

        private void OnStarted(TutorialHandle handle)
        {
            if (_attach) Attach(handle);
            else if (handle.Graph == _graph) _graphView?.ResetDebugStates();
        }

        private void OnNodeEntered(TutorialGraph graph, TutorialNode node)
        {
            if (graph != _graph) return;
            _graphView?.HighlightActiveNode(node.Id);
            SetDebugStatus(node.DisplayName);
        }

        private void OnNodeExited(TutorialGraph graph, TutorialNode node, string port)
        {
            if (graph != _graph || port == null) return;
            foreach (var next in graph.GetNextNodeIds(node.Id, port))
                _graphView?.MarkTraversedEdge(node.Id, port, next);
        }

        private void OnFinished(TutorialHandle handle)
        {
            if (handle.Graph != _graph) return;
            _graphView?.StopActive();
            if (_statusLabel != null)
                _statusLabel.text = $"  {handle.Graph.DisplayName}  ·  {StatusIcon(handle.Status)} {handle.Status}";
        }

        private static string StatusIcon(TutorialStatus s) =>
            s == TutorialStatus.Completed ? "✓" : s == TutorialStatus.Faulted ? "✗" : "■";

        private void SetDebugStatus(string current)
        {
            if (_statusLabel != null)
                _statusLabel.text = $"  ▶ Running: {current}";
        }

        private void SetNormalStatus()
        {
            if (_statusLabel != null)
                _statusLabel.text = _graph != null ? $"  {_graph.DisplayName}  ({_graph.Nodes.Count} nodes) " : " No tutorial loaded ";
        }
    }

    /// <summary>Plays a tutorial when Play mode is entered from the graph editor's Test button.</summary>
    [InitializeOnLoad]
    internal static class TutorialTestLauncher
    {
        public const string PendingGuidKey = "TutorialKit.TestGuid";

        static TutorialTestLauncher()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    PlayPending();
            };
        }

        public static void PlayPending()
        {
            var guid = SessionState.GetString(PendingGuidKey, null);
            if (string.IsNullOrEmpty(guid)) return;
            SessionState.EraseString(PendingGuidKey);

            var path = AssetDatabase.GUIDToAssetPath(guid);
            var graph = AssetDatabase.LoadAssetAtPath<TutorialGraph>(path);
            if (graph == null) return;

            TutorialDirector.EnsureExists().Play(graph, force: true);
        }
    }

    /// <summary>
    /// When enabled (default), auto-opens the graph editor on whichever tutorial starts playing and
    /// live-attaches it — no need to open the window first. Listens to the static
    /// <see cref="TutorialDirector.AnyStarted"/> hook so it works for asset and code-built graphs alike.
    /// </summary>
    [InitializeOnLoad]
    internal static class TutorialGraphAutoOpen
    {
        public const string PrefKey = "TutorialKit.AutoOpenOnPlay";

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static TutorialGraphAutoOpen()
        {
            TutorialDirector.AnyStarted -= OnAnyStarted;
            TutorialDirector.AnyStarted += OnAnyStarted;
        }

        private static void OnAnyStarted(TutorialHandle handle)
        {
            if (!Enabled || handle?.Graph == null) return;
            var h = handle;
            // Defer one tick: opening an editor window from inside the runtime start callback is safest next frame.
            EditorApplication.delayCall += () =>
            {
                if (h?.Graph != null) TutorialGraphEditorWindow.ShowLive(h);
            };
        }
    }
}
