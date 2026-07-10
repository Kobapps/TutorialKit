using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TutorialKit.Editor
{
    /// <summary>
    /// A floating, draggable, collapsible blackboard panel. Declares typed variables on the graph
    /// (Bool/Int/Float/String) whose defaults seed the run-time blackboard; nodes read/write by key.
    /// </summary>
    public sealed class TutorialBlackboardPanel : VisualElement
    {
        private sealed class LiveRow { public TutorialBlackboardVar Var; public Label Live; }

        private TutorialGraph _graph;
        private readonly List<TutorialBlackboardVar> _vars = new List<TutorialBlackboardVar>();
        private readonly List<LiveRow> _rows = new List<LiveRow>();
        private readonly VisualElement _body;
        private readonly Button _collapseBtn;
        private bool _collapsed;

        private bool _dragging;
        private Vector2 _dragOffset;

        public TutorialBlackboardPanel()
        {
            AddToClassList("tk-panel");
            style.position = Position.Absolute;
            style.left = 12;
            style.top = 12;
            style.width = 236;

            var header = new VisualElement();
            header.AddToClassList("tk-panel-header");

            _collapseBtn = new Button(ToggleCollapsed) { text = "▾", style = { width = 20, marginRight = 2 } };
            header.Add(_collapseBtn);

            var title = new Label("Blackboard");
            title.AddToClassList("tk-panel-title");
            header.Add(title);

            var add = new Button(AddVariable) { text = "＋", tooltip = "Add variable", style = { width = 22 } };
            header.Add(add);
            Add(header);

            _body = new VisualElement();
            _body.AddToClassList("tk-panel-body");
            Add(_body);

            header.RegisterCallback<PointerDownEvent>(OnHeaderDown);
            header.RegisterCallback<PointerMoveEvent>(OnHeaderMove);
            header.RegisterCallback<PointerUpEvent>(OnHeaderUp);

            schedule.Execute(RefreshLive).Every(300);
            schedule.Execute(SyncTick).Every(1000);
        }

        // Auto-adds flag keys referenced by Set Flag / Condition nodes so they appear here.
        private void SyncTick()
        {
            if (_graph == null || EditorApplication.isPlaying) return;
            if (SyncFromGraph()) Rebuild();
        }

        private bool SyncFromGraph()
        {
            if (_graph == null) return false;
            var existing = new HashSet<string>();
            foreach (var v in _vars) if (!string.IsNullOrEmpty(v.Key)) existing.Add(v.Key);

            bool changed = false;
            foreach (var node in _graph.Nodes)
            {
                string key = null;
                if (node is SetFlagNode sf) key = sf.Key;
                else if (node is ConditionNode c && c.Kind == ConditionNode.ConditionKind.BlackboardFlag) key = c.Key;

                if (!string.IsNullOrEmpty(key) && existing.Add(key))
                {
                    _vars.Add(new TutorialBlackboardVar { Key = key, Type = BlackboardVarType.Bool, DefaultValue = "false" });
                    changed = true;
                }
            }
            if (changed) Save();
            return changed;
        }

        // Shows live runtime values next to each variable during Play mode.
        private void RefreshLive()
        {
            var bb = EditorApplication.isPlaying ? TutorialDirector.Current?.ActiveBlackboard : null;
            foreach (var r in _rows)
            {
                if (bb != null && r.Var != null && !string.IsNullOrEmpty(r.Var.Key) && bb.TryGetValue(r.Var.Key, out var val))
                {
                    r.Live.text = "→ " + val;
                    r.Live.style.display = DisplayStyle.Flex;
                }
                else
                {
                    r.Live.style.display = DisplayStyle.None;
                }
            }
        }

        public void SetGraph(TutorialGraph graph)
        {
            _graph = graph;
            _vars.Clear();
            if (graph?.Blackboard != null)
                foreach (var v in graph.Blackboard)
                    if (v != null) _vars.Add(new TutorialBlackboardVar { Key = v.Key, Type = v.Type, DefaultValue = v.DefaultValue });
            SyncFromGraph();
            Rebuild();
        }

        private void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            _body.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _collapseBtn.text = _collapsed ? "▸" : "▾";
        }

        private void AddVariable()
        {
            _vars.Add(new TutorialBlackboardVar { Key = "var" + (_vars.Count + 1), Type = BlackboardVarType.Bool, DefaultValue = "false" });
            Save();
            Rebuild();
        }

        private void Rebuild()
        {
            _body.Clear();
            _rows.Clear();
            if (_graph == null)
            {
                _body.Add(new Label("No graph loaded.") { style = { color = new Color(0.6f, 0.62f, 0.68f) } });
                return;
            }
            if (_vars.Count == 0)
                _body.Add(new Label("No variables. Click ＋ to add.") { style = { color = new Color(0.6f, 0.62f, 0.68f), fontSize = 10 } });

            foreach (var v in _vars)
                _body.Add(BuildRow(v));
        }

        private VisualElement BuildRow(TutorialBlackboardVar v)
        {
            var row = new VisualElement();
            row.AddToClassList("tk-bb-row");

            var key = new TextField { value = v.Key, tooltip = "Variable key (referenced by Set Flag / Condition nodes)", style = { width = 80 } };
            key.RegisterValueChangedCallback(e => { v.Key = e.newValue; Save(); });
            row.Add(key);

            var type = new EnumField(v.Type) { tooltip = "Variable type", style = { width = 62 } };
            type.RegisterValueChangedCallback(e => { v.Type = (BlackboardVarType)e.newValue; Save(); Rebuild(); });
            row.Add(type);

            row.Add(BuildValueField(v));

            var live = new Label
            {
                style =
                {
                    color = new StyleColor(new Color(1f, 0.82f, 0.35f)),
                    fontSize = 10,
                    marginLeft = 4,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    display = DisplayStyle.None,
                },
            };
            row.Add(live);
            _rows.Add(new LiveRow { Var = v, Live = live });

            var del = new Button(() => { _vars.Remove(v); Save(); Rebuild(); }) { text = "×", tooltip = "Delete", style = { width = 20 } };
            row.Add(del);
            return row;
        }

        private VisualElement BuildValueField(TutorialBlackboardVar v)
        {
            switch (v.Type)
            {
                case BlackboardVarType.Bool:
                {
                    var t = new Toggle { value = ParseBool(v.DefaultValue), tooltip = "Default value", style = { flexGrow = 1 } };
                    t.RegisterValueChangedCallback(e => { v.DefaultValue = e.newValue ? "true" : "false"; Save(); });
                    return t;
                }
                case BlackboardVarType.Int:
                {
                    int.TryParse(v.DefaultValue, out var iv);
                    var f = new IntegerField { value = iv, tooltip = "Default value", style = { flexGrow = 1 } };
                    f.RegisterValueChangedCallback(e => { v.DefaultValue = e.newValue.ToString(); Save(); });
                    return f;
                }
                case BlackboardVarType.Float:
                {
                    float.TryParse(v.DefaultValue, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var fv);
                    var f = new FloatField { value = fv, tooltip = "Default value", style = { flexGrow = 1 } };
                    f.RegisterValueChangedCallback(e =>
                    {
                        v.DefaultValue = e.newValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        Save();
                    });
                    return f;
                }
                default:
                {
                    var tf = new TextField { value = v.DefaultValue, tooltip = "Default value", style = { flexGrow = 1 } };
                    tf.RegisterValueChangedCallback(e => { v.DefaultValue = e.newValue; Save(); });
                    return tf;
                }
            }
        }

        private static bool ParseBool(string s) =>
            s == "1" || string.Equals(s, "true", System.StringComparison.OrdinalIgnoreCase);

        private void Save()
        {
            if (_graph == null) return;
            Undo.RegisterCompleteObjectUndo(_graph, "Edit Blackboard");
            var copy = new List<TutorialBlackboardVar>(_vars.Count);
            foreach (var v in _vars)
                copy.Add(new TutorialBlackboardVar { Key = v.Key, Type = v.Type, DefaultValue = v.DefaultValue });
            _graph.SetBlackboard(copy);
            EditorUtility.SetDirty(_graph);
        }

        // ---- Drag ----
        private void OnHeaderDown(PointerDownEvent e)
        {
            _dragging = true;
            _dragOffset = new Vector2(e.localPosition.x, e.localPosition.y);
            (e.currentTarget as VisualElement)?.CapturePointer(e.pointerId);
        }

        private void OnHeaderMove(PointerMoveEvent e)
        {
            if (!_dragging || parent == null) return;
            Vector2 p = parent.WorldToLocal(e.position);
            style.left = Mathf.Max(0, p.x - _dragOffset.x);
            style.top = Mathf.Max(0, p.y - _dragOffset.y);
        }

        private void OnHeaderUp(PointerUpEvent e)
        {
            _dragging = false;
            (e.currentTarget as VisualElement)?.ReleasePointer(e.pointerId);
        }
    }
}
