using System;
using System.Collections.Generic;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Type-tagged JSON (de)serializer for tutorial graphs. Each node is tagged with its stable
    /// <c>TypeId</c> (from <see cref="NodeTypeRegistry"/>) and its fields serialized with
    /// <see cref="JsonUtility"/>, which sidesteps JsonUtility's lack of polymorphism. Custom game
    /// nodes round-trip automatically with no extra code.
    /// </summary>
    public static class TutorialJson
    {
        [Serializable]
        private sealed class GraphDto
        {
            public string tutorialId;
            public string displayName;
            public string description;
            public string entry;
            public List<NodeDto> nodes = new List<NodeDto>();
            public List<EdgeDto> edges = new List<EdgeDto>();
            public List<DataEdgeDto> dataEdges = new List<DataEdgeDto>();
        }

        [Serializable]
        private sealed class DataEdgeDto
        {
            public string from;
            public string fromPort;
            public string to;
            public string toPort;
        }

        [Serializable]
        private sealed class NodeDto
        {
            public string type;   // stable TypeId
            public string id;     // convenience mirror of the node id
            public string data;   // JsonUtility.ToJson(node)
        }

        [Serializable]
        private sealed class EdgeDto
        {
            public string from;
            public string port;
            public string to;
        }

        public static string ToJson(TutorialGraph graph, bool pretty = true)
        {
            if (graph == null) return "{}";
            graph.Validate();

            var dto = new GraphDto
            {
                tutorialId = graph.TutorialId,
                displayName = graph.DisplayName,
                description = graph.Description,
                entry = graph.EntryNodeId,
            };

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                dto.nodes.Add(new NodeDto
                {
                    type = NodeTypeRegistry.GetTypeId(node),
                    id = node.Id,
                    data = JsonUtility.ToJson(node),
                });
            }

            foreach (var e in graph.Edges)
                dto.edges.Add(new EdgeDto { from = e.FromNodeId, port = e.FromPort, to = e.ToNodeId });

            foreach (var e in graph.DataEdges)
                dto.dataEdges.Add(new DataEdgeDto { from = e.FromNodeId, fromPort = e.FromPort, to = e.ToNodeId, toPort = e.ToPort });

            return JsonUtility.ToJson(dto, pretty);
        }

        /// <summary>Creates a new in-memory <see cref="TutorialGraph"/> from JSON.</summary>
        public static TutorialGraph FromJson(string json)
        {
            var graph = ScriptableObject.CreateInstance<TutorialGraph>();
            Overwrite(graph, json);
            return graph;
        }

        /// <summary>Replaces the content of an existing graph from JSON.</summary>
        public static void Overwrite(TutorialGraph target, string json)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Empty tutorial JSON.", nameof(json));

            var dto = JsonUtility.FromJson<GraphDto>(json);
            if (dto == null) throw new FormatException("Could not parse tutorial JSON.");

            var nodes = new List<TutorialNode>();
            if (dto.nodes != null)
            {
                foreach (var nd in dto.nodes)
                {
                    if (nd == null || string.IsNullOrEmpty(nd.type)) continue;
                    var node = NodeTypeRegistry.Create(nd.type);
                    if (node == null)
                    {
                        Debug.LogWarning($"[TutorialKit] Unknown node type '{nd.type}' in JSON; skipping.");
                        continue;
                    }
                    if (!string.IsNullOrEmpty(nd.data))
                        JsonUtility.FromJsonOverwrite(nd.data, node);
                    if (!string.IsNullOrEmpty(nd.id)) node.Id = nd.id;
                    node.EnsureId();
                    nodes.Add(node);
                }
            }

            var edges = new List<TutorialEdge>();
            if (dto.edges != null)
                foreach (var ed in dto.edges)
                    if (ed != null)
                        edges.Add(new TutorialEdge(ed.from, ed.port, ed.to));

            target.Populate(dto.tutorialId, dto.displayName, dto.description, dto.entry, nodes, edges);

            var dataEdges = new List<TutorialDataEdge>();
            if (dto.dataEdges != null)
                foreach (var de in dto.dataEdges)
                    if (de != null)
                        dataEdges.Add(new TutorialDataEdge(de.from, de.fromPort, de.to, de.toPort));
            target.SetDataEdges(dataEdges);

            target.name = string.IsNullOrEmpty(dto.tutorialId) ? "RemoteTutorial" : dto.tutorialId;
        }
    }
}
