using System.Collections.Generic;
using UnityEngine;

namespace TutorialKit.Editor
{
    /// <summary>
    /// Data-only graph auto-layout: computes each node's <c>EditorPosition</c> (longest-path layering +
    /// barycenter placement, with provider/value nodes tucked into a side gutter). Used by the graph
    /// editor's Auto Layout button and by <see cref="TutorialGraphAuthoring"/> — no open window required.
    /// </summary>
    public static class TutorialGraphLayout
    {
        // A value/provider node (e.g. a target node) has no flow ports — it's laid out beside its consumer.
        public static bool IsValueNode(TutorialNode n) => n != null && !n.HasInput && n.OutputPorts.Count == 0;

        /// <summary>Computes and writes positions onto the graph's nodes (<paramref name="vertical"/> = top→bottom flow).</summary>
        public static void Apply(TutorialGraph graph, bool vertical = false)
        {
            var pos = ComputePositions(graph, vertical);
            foreach (var n in graph.Nodes)
                if (n != null && pos.TryGetValue(n.Id, out var p)) n.EditorPosition = p;
        }

        /// <summary>Computes a position for every node without mutating the graph.</summary>
        public static Dictionary<string, Vector2> ComputePositions(TutorialGraph graph, bool vertical)
        {
            var pos = new Dictionary<string, Vector2>();
            if (graph == null || graph.Nodes.Count == 0) return pos;

            bool vert = vertical;
            const float laneGap = 300f;             // gutter before a consumer for its provider nodes
            const float provGap = 150f;             // vertical spacing between stacked providers
            float crossStep = vert ? 260f : 150f;   // spacing between siblings (X if vertical, else Y)
            float mainStep = vert ? 190f : 300f;    // spacing between depth layers (Y if vertical, else X)

            // 1. Longest-path depth assignment (flow nodes only).
            var col = new Dictionary<string, int>();
            var queue = new Queue<string>();
            string entry = graph.EntryNode != null ? graph.EntryNode.Id : graph.Nodes[0].Id;
            col[entry] = 0;
            queue.Enqueue(entry);
            int guard = 0, maxGuard = graph.Nodes.Count * graph.Nodes.Count + 16;
            while (queue.Count > 0 && guard++ < maxGuard)
            {
                var cur = queue.Dequeue();
                var node = graph.FindNode(cur);
                if (node == null) continue;
                foreach (var port in node.OutputPorts)
                    foreach (var next in graph.GetNextNodeIds(cur, port))
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
            foreach (var n in graph.Nodes) // orphan flow nodes → trailing column
                if (n != null && !IsValueNode(n) && !col.ContainsKey(n.Id)) col[n.Id] = maxCol + 1;
            foreach (var kv in col) if (kv.Value > maxCol) maxCol = kv.Value;

            // 2. Flow parents (for barycenter placement).
            var parents = new Dictionary<string, List<string>>();
            foreach (var e in graph.Edges)
            {
                if (!parents.TryGetValue(e.ToNodeId, out var list)) { list = new List<string>(); parents[e.ToNodeId] = list; }
                list.Add(e.FromNodeId);
            }

            // 3. Which columns need a provider lane (extra width only where actually needed).
            var colHasProviders = new HashSet<int>();
            foreach (var de in graph.DataEdges)
            {
                var producer = graph.FindNode(de.FromNodeId);
                if (producer != null && IsValueNode(producer) && col.TryGetValue(de.ToNodeId, out var c))
                    colHasProviders.Add(c);
            }

            // 4. Depth positions.
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

            // 5. Barycenter cross placement, layer by layer.
            var crossOf = new Dictionary<string, float>();
            var byCol = new Dictionary<int, List<string>>();
            foreach (var n in graph.Nodes)
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

            // 6. Providers sit to the LEFT of their consumer, stacked and ordered by the consumer's ports.
            var providersByConsumer = new Dictionary<string, List<(string id, int portIdx)>>();
            var dangling = new List<string>();
            foreach (var n in graph.Nodes)
            {
                if (n == null || !IsValueNode(n)) continue;
                string consumerId = null, toPort = null;
                foreach (var de in graph.DataEdges)
                    if (de.FromNodeId == n.Id) { consumerId = de.ToNodeId; toPort = de.ToPort; break; }

                if (consumerId != null && pos.ContainsKey(consumerId))
                {
                    if (!providersByConsumer.TryGetValue(consumerId, out var list))
                    { list = new List<(string, int)>(); providersByConsumer[consumerId] = list; }
                    list.Add((n.Id, PortIndex(graph, consumerId, toPort)));
                }
                else dangling.Add(n.Id);
            }

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
                int bucket = Mathf.RoundToInt(cp.y / provGap) + 1;
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

            return pos;
        }

        private static int PortIndex(TutorialGraph graph, string nodeId, string portName)
        {
            var node = graph.FindNode(nodeId);
            if (node != null && portName != null)
                for (int i = 0; i < node.InputDataPorts.Count; i++)
                    if (node.InputDataPorts[i].Name == portName) return i;
            return int.MaxValue;
        }
    }
}
