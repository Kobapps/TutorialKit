using System;
using System.Collections.Generic;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// A serialized, authorable tutorial sequence: a set of polymorphic <see cref="TutorialNode"/>s
    /// connected by <see cref="TutorialEdge"/>s, played from <see cref="EntryNodeId"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "TutorialKit/Tutorial Graph", fileName = "NewTutorial", order = 0)]
    public class TutorialGraph : ScriptableObject
    {
        [SerializeField] private string tutorialId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField, HideInInspector] private string entryNodeId;

        [SerializeReference] private List<TutorialNode> nodes = new List<TutorialNode>();
        [SerializeField] private List<TutorialEdge> edges = new List<TutorialEdge>();
        [SerializeField] private List<TutorialDataEdge> dataEdges = new List<TutorialDataEdge>();
        [SerializeField] private List<TutorialGroupData> groups = new List<TutorialGroupData>();
        [SerializeField] private List<TutorialBlackboardVar> blackboard = new List<TutorialBlackboardVar>();
        [SerializeField, HideInInspector] private TutorialLayoutDirection layoutDirection = TutorialLayoutDirection.UseDefault;

        public IReadOnlyList<TutorialGroupData> Groups => groups;
        public IReadOnlyList<TutorialBlackboardVar> Blackboard => blackboard;

        /// <summary>This graph's preferred editor flow direction. <c>UseDefault</c> follows the project setting.</summary>
        public TutorialLayoutDirection LayoutDirection
        {
            get => layoutDirection;
            set => layoutDirection = value;
        }

        /// <summary>Stable id used for persistence and remote lookup. Falls back to the asset name.</summary>
        public string TutorialId => string.IsNullOrEmpty(tutorialId) ? name : tutorialId;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;

        public string EntryNodeId
        {
            get => entryNodeId;
            set => entryNodeId = value;
        }

        public IReadOnlyList<TutorialNode> Nodes => nodes;
        public IReadOnlyList<TutorialEdge> Edges => edges;
        public IReadOnlyList<TutorialDataEdge> DataEdges => dataEdges;

        public TutorialNode EntryNode
        {
            get
            {
                var n = FindNode(entryNodeId);
                if (n != null) return n;
                return nodes.Count > 0 ? nodes[0] : null;
            }
        }

        public TutorialNode FindNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && nodes[i].Id == nodeId)
                    return nodes[i];
            return null;
        }

        /// <summary>First node reached by following <paramref name="port"/> (used by layout/single-flow helpers).</summary>
        public string GetNextNodeId(string nodeId, string port)
        {
            if (string.IsNullOrEmpty(nodeId) || port == null) return null;
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                if (e.FromNodeId == nodeId && e.FromPort == port)
                    return e.ToNodeId;
            }
            return null;
        }

        /// <summary>All nodes reached by following <paramref name="port"/> (an output port may fan out to several).</summary>
        public List<string> GetNextNodeIds(string nodeId, string port)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(nodeId) || port == null) return result;
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                if (e.FromNodeId == nodeId && e.FromPort == port && !string.IsNullOrEmpty(e.ToNodeId))
                    result.Add(e.ToNodeId);
            }
            return result;
        }

        // ---- Authoring API (used by the editor and by remote loading) ----

        public void AddNode(TutorialNode node)
        {
            if (node == null) return;
            node.EnsureId();
            nodes.Add(node);
            if (string.IsNullOrEmpty(entryNodeId))
                entryNodeId = node.Id;
        }

        public void RemoveNode(string nodeId)
        {
            nodes.RemoveAll(n => n == null || n.Id == nodeId);
            edges.RemoveAll(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId);
            dataEdges.RemoveAll(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId);
            if (entryNodeId == nodeId)
                entryNodeId = nodes.Count > 0 ? nodes[0].Id : null;
        }

        /// <summary>Replaces all edges from (node, port) with a single edge. Convenience for linear flows.</summary>
        public void SetEdge(string fromNodeId, string fromPort, string toNodeId)
        {
            edges.RemoveAll(e => e.FromNodeId == fromNodeId && e.FromPort == fromPort);
            edges.Add(new TutorialEdge(fromNodeId, fromPort, toNodeId));
        }

        /// <summary>Adds an edge (an output port may connect to several nodes). Ignores duplicates.</summary>
        public void AddEdge(string fromNodeId, string fromPort, string toNodeId)
        {
            var e = new TutorialEdge(fromNodeId, fromPort, toNodeId);
            if (!edges.Contains(e)) edges.Add(e);
        }

        /// <summary>Removes all edges from (node, port).</summary>
        public void RemoveEdge(string fromNodeId, string fromPort)
        {
            edges.RemoveAll(e => e.FromNodeId == fromNodeId && e.FromPort == fromPort);
        }

        /// <summary>Removes one specific edge.</summary>
        public void RemoveEdge(string fromNodeId, string fromPort, string toNodeId)
        {
            edges.RemoveAll(e => e.FromNodeId == fromNodeId && e.FromPort == fromPort && e.ToNodeId == toNodeId);
        }

        public void ClearEdgesTo(string toNodeId) => edges.RemoveAll(e => e.ToNodeId == toNodeId);

        // ---- Data edges (typed value connections) ----

        /// <summary>Connects a producer's output data port to a consumer's input data port. Ignores exact
        /// duplicates; a multi-capacity input may have several sources (single ones are managed by the editor).</summary>
        public void AddDataEdge(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            var e = new TutorialDataEdge(fromNodeId, fromPort, toNodeId, toPort);
            if (!dataEdges.Contains(e)) dataEdges.Add(e);
        }

        public void RemoveDataEdge(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            dataEdges.RemoveAll(e => e.FromNodeId == fromNodeId && e.FromPort == fromPort &&
                                     e.ToNodeId == toNodeId && e.ToPort == toPort);
        }

        /// <summary>Finds the producer feeding a consumer's input data port, if any.</summary>
        public bool TryGetDataSource(string toNodeId, string toPort, out string fromNodeId, out string fromPort)
        {
            for (int i = 0; i < dataEdges.Count; i++)
            {
                var e = dataEdges[i];
                if (e.ToNodeId == toNodeId && e.ToPort == toPort)
                {
                    fromNodeId = e.FromNodeId;
                    fromPort = e.FromPort;
                    return true;
                }
            }
            fromNodeId = null;
            fromPort = null;
            return false;
        }

        /// <summary>All producers feeding a consumer's input data port (for multi-capacity inputs).</summary>
        public List<(string fromNodeId, string fromPort)> GetDataSources(string toNodeId, string toPort)
        {
            var list = new List<(string, string)>();
            for (int i = 0; i < dataEdges.Count; i++)
            {
                var e = dataEdges[i];
                if (e.ToNodeId == toNodeId && e.ToPort == toPort)
                    list.Add((e.FromNodeId, e.FromPort));
            }
            return list;
        }

        // ---- Groups (editor organisation) ----

        /// <summary>Replaces all group data (called by the graph editor on save).</summary>
        public void SetGroups(List<TutorialGroupData> newGroups)
        {
            groups = newGroups ?? new List<TutorialGroupData>();
        }

        // ---- Blackboard ----

        public void SetBlackboard(List<TutorialBlackboardVar> vars)
        {
            blackboard = vars ?? new List<TutorialBlackboardVar>();
        }

        /// <summary>Seeds a run-time blackboard dictionary with this graph's declared defaults.</summary>
        public void SeedBlackboard(IDictionary<string, object> target)
        {
            if (blackboard == null || target == null) return;
            foreach (var v in blackboard)
            {
                if (v == null || string.IsNullOrEmpty(v.Key)) continue;
                target[v.Key] = v.ToValue();
            }
        }

        /// <summary>Replaces the entire graph content (used by the JSON loader).</summary>
        public void Populate(string id, string display, string desc, string entry,
            List<TutorialNode> newNodes, List<TutorialEdge> newEdges)
        {
            tutorialId = id;
            displayName = display;
            description = desc;
            entryNodeId = entry;
            nodes = newNodes ?? new List<TutorialNode>();
            edges = newEdges ?? new List<TutorialEdge>();
            dataEdges = new List<TutorialDataEdge>();
            foreach (var n in nodes) n?.EnsureId();
        }

        /// <summary>Replaces the data edges (used by the JSON loader).</summary>
        public void SetDataEdges(List<TutorialDataEdge> newDataEdges) =>
            dataEdges = newDataEdges ?? new List<TutorialDataEdge>();

        /// <summary>Ensures every node has an id and the entry points at a valid node.</summary>
        public void Validate()
        {
            foreach (var n in nodes) n?.EnsureId();
            if (FindNode(entryNodeId) == null && nodes.Count > 0)
                entryNodeId = nodes[0].Id;
        }
    }
}
