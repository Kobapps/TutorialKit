using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Interprets a <see cref="TutorialGraph"/>: starting at the entry node, it executes each node,
    /// follows the returned output port to the next node, and stops when a node returns
    /// <c>null</c> (or there is no matching edge). Fully driven by <see cref="CancellationToken"/>.
    /// </summary>
    public sealed class TutorialPlayer
    {
        /// <summary>Raised just before a node executes (used by the live debugger).</summary>
        public event Action<TutorialNode> NodeEntered;

        /// <summary>Raised after a node executes, with the chosen output port (may be null).</summary>
        public event Action<TutorialNode, string> NodeExited;

        /// <summary>Guards against runaway graphs (cycles with no waits).</summary>
        public int MaxSteps { get; set; } = 100_000;

        public async UniTask RunAsync(TutorialGraph graph, TutorialRunContext ctx, CancellationToken ct)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            graph.Validate();
            ctx.Graph = graph;
            graph.SeedBlackboard(ctx.Blackboard);

            var entry = graph.EntryNode;
            if (entry == null) return;
            await RunBranchAsync(entry.Id, graph, ctx, ct);
        }

        /// <summary>
        /// Walks a linear chain, tail-looping to avoid recursion. When an output port fans out to more
        /// than one node, each target runs as a concurrent branch and this call awaits them all (join).
        /// </summary>
        private async UniTask RunBranchAsync(string nodeId, TutorialGraph graph, TutorialRunContext ctx, CancellationToken ct)
        {
            var node = graph.FindNode(nodeId);
            int steps = 0;

            while (node != null)
            {
                ct.ThrowIfCancellationRequested();

                if (++steps > MaxSteps)
                {
                    Debug.LogError($"[TutorialKit] Graph '{graph.TutorialId}' branch exceeded {MaxSteps} steps; aborting to avoid an infinite loop.");
                    return;
                }

                NodeEntered?.Invoke(node);

                string port;
                try
                {
                    port = await node.ExecuteAsync(ctx, ct);
                }
                catch (OperationCanceledException)
                {
                    NodeExited?.Invoke(node, null);
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Debug.LogError($"[TutorialKit] Node '{node.DisplayName}' ({node.Id}) threw; aborting tutorial '{graph.TutorialId}'.");
                    throw;
                }

                NodeExited?.Invoke(node, port);

                if (port == null) return;

                var nexts = graph.GetNextNodeIds(node.Id, port);
                if (nexts.Count == 0) return;
                if (nexts.Count == 1)
                {
                    node = graph.FindNode(nexts[0]);
                    continue;
                }

                // Fan-out: run each connected branch concurrently, then join.
                var tasks = new List<UniTask>(nexts.Count);
                for (int i = 0; i < nexts.Count; i++)
                    tasks.Add(RunBranchAsync(nexts[i], graph, ctx, ct));
                await UniTask.WhenAll(tasks);
                return;
            }
        }
    }
}
