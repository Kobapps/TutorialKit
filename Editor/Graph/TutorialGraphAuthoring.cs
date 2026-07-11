using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TutorialKit.Editor
{
    /// <summary>
    /// High-level editor API for authoring tutorial graph <b>assets</b> from code (AI tools, generators,
    /// tests). Prefer this over building graphs at runtime: it produces a real <c>.asset</c> — the same
    /// thing the graph editor opens — with the mandatory Start/End nodes, stable ids, connections and
    /// auto-layout, then saves it. Typical flow:
    /// <code>
    /// var g = TutorialGraphAuthoring.CreateGraph("Assets/Tutorials/Shop.asset", "Shop Intro");
    /// var t = TutorialGraphAuthoring.AddNode&lt;ShowTextBoxNode&gt;(g); t.Title = "Hi"; t.Body = "Tap the shop.";
    /// var v = TutorialGraphAuthoring.AddNode&lt;ShowVignetteNode&gt;(g); v.Target = new TutorialTargetRef("shop_button");
    /// TutorialGraphAuthoring.Chain(g, TutorialGraphAuthoring.GetStart(g), t, v, TutorialGraphAuthoring.GetEnd(g));
    /// TutorialGraphAuthoring.Layout(g);
    /// TutorialGraphAuthoring.Save(g);
    /// </code>
    /// </summary>
    public static class TutorialGraphAuthoring
    {
        /// <summary>
        /// Creates a tutorial graph asset at <paramref name="assetPath"/> (must be under "Assets/…", the
        /// ".asset" extension is added if missing). It already contains the required <b>Start</b> and
        /// <b>End</b> nodes. Add nodes, <see cref="Chain"/>/<see cref="Connect"/> them, then
        /// <see cref="Layout"/> + <see cref="Save"/>.
        /// </summary>
        public static TutorialGraph CreateGraph(string assetPath, string displayName = null, string description = null)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/"))
                throw new ArgumentException("assetPath must start with 'Assets/'.", nameof(assetPath));
            if (!assetPath.EndsWith(".asset")) assetPath += ".asset";
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));

            var graph = ScriptableObject.CreateInstance<TutorialGraph>();
            graph.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(graph, assetPath);

            SetMeta(graph, displayName, description);
            EnsureStartEnd(graph); // adds Start (entry) + End, wired Start→End

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            return graph;
        }

        /// <summary>Loads an existing tutorial graph asset for editing.</summary>
        public static TutorialGraph Load(string assetPath) => AssetDatabase.LoadAssetAtPath<TutorialGraph>(assetPath);

        /// <summary>Adds a node of type <typeparamref name="T"/> and returns it so you can set its fields.</summary>
        public static T AddNode<T>(TutorialGraph graph) where T : TutorialNode, new()
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var node = new T();
            node.EnsureId();
            graph.AddNode(node);
            EditorUtility.SetDirty(graph);
            return node;
        }

        /// <summary>Adds a node by its registered TypeId (e.g. "ShowVignetteNode"), including custom nodes.</summary>
        public static TutorialNode AddNode(TutorialGraph graph, string typeId)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var node = NodeTypeRegistry.Create(typeId);
            if (node == null) throw new ArgumentException($"Unknown node TypeId '{typeId}'.", nameof(typeId));
            graph.AddNode(node);
            EditorUtility.SetDirty(graph);
            return node;
        }

        /// <summary>
        /// Connects a single flow edge <paramref name="from"/>.<paramref name="port"/> → <paramref name="to"/>,
        /// replacing any existing edge on that port. <paramref name="port"/> defaults to the node's first
        /// output (use "True"/"False" for a Condition). Use <see cref="ConnectFanOut"/> for concurrent branches.
        /// </summary>
        public static void Connect(TutorialGraph graph, TutorialNode from, TutorialNode to, string port = null)
        {
            if (graph == null || from == null || to == null) return;
            graph.SetEdge(from.Id, ResolvePort(from, port), to.Id);
            EditorUtility.SetDirty(graph);
        }

        /// <summary>Adds an extra edge from one output port to another node (fan-out: branches run concurrently).</summary>
        public static void ConnectFanOut(TutorialGraph graph, TutorialNode from, TutorialNode to, string port = null)
        {
            if (graph == null || from == null || to == null) return;
            graph.AddEdge(from.Id, ResolvePort(from, port), to.Id);
            EditorUtility.SetDirty(graph);
        }

        /// <summary>Connects a sequence with single flow edges: seq[0] → seq[1] → … (e.g. Start → … → End).</summary>
        public static void Chain(TutorialGraph graph, params TutorialNode[] seq)
        {
            if (graph == null || seq == null) return;
            for (int i = 0; i < seq.Length - 1; i++)
                Connect(graph, seq[i], seq[i + 1]);
        }

        /// <summary>Connects a producer's data output into a consumer's data input (e.g. a target provider → a vignette's Target).</summary>
        public static void ConnectData(TutorialGraph graph, TutorialNode from, string fromPort, TutorialNode to, string toPort)
        {
            if (graph == null || from == null || to == null) return;
            graph.AddDataEdge(from.Id, fromPort, to.Id, toPort);
            EditorUtility.SetDirty(graph);
        }

        /// <summary>The graph's mandatory Start node (its entry).</summary>
        public static TutorialNode GetStart(TutorialGraph graph) => Find<StartNode>(graph);

        /// <summary>An End node in the graph (there is always at least one).</summary>
        public static TutorialNode GetEnd(TutorialGraph graph) => Find<EndNode>(graph);

        /// <summary>Ensures the graph has its required Start and (at least one) End node.</summary>
        public static void EnsureStartEnd(TutorialGraph graph)
        {
            if (graph == null) return;
            TutorialGraphView.EnsureStartNode(graph);
            TutorialGraphView.EnsureEndNode(graph);
            EditorUtility.SetDirty(graph);
        }

        /// <summary>Arranges node positions (the same algorithm as the editor's Auto Layout).</summary>
        public static void Layout(TutorialGraph graph, bool vertical = false)
        {
            if (graph == null) return;
            TutorialGraphLayout.Apply(graph, vertical);
            EditorUtility.SetDirty(graph);
        }

        /// <summary>Validates and saves the asset so it opens cleanly in the editor.</summary>
        public static void Save(TutorialGraph graph)
        {
            if (graph == null) return;
            graph.Validate();
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }

        // --- helpers ---

        private static string ResolvePort(TutorialNode from, string port)
        {
            if (!string.IsNullOrEmpty(port)) return port;
            return from.OutputPorts.Count > 0 ? from.OutputPorts[0] : TutorialNode.OutPort;
        }

        private static T Find<T>(TutorialGraph graph) where T : TutorialNode
        {
            if (graph != null)
                foreach (var n in graph.Nodes) if (n is T t) return t;
            return null;
        }

        private static void SetMeta(TutorialGraph graph, string displayName, string description)
        {
            if (string.IsNullOrEmpty(displayName) && string.IsNullOrEmpty(description)) return;
            var so = new SerializedObject(graph);
            if (!string.IsNullOrEmpty(displayName)) so.FindProperty("displayName").stringValue = displayName;
            if (!string.IsNullOrEmpty(description)) so.FindProperty("description").stringValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || folder == "Assets" || AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
