using System.Collections.Generic;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Everything a node needs while a tutorial runs: overlay services, game adapters, the
    /// currently playing graph, and a shared blackboard for passing values between nodes.
    /// Constructed by <see cref="TutorialDirector"/> and handed to every node's <c>ExecuteAsync</c>.
    /// </summary>
    public sealed class TutorialRunContext
    {
        public IVignetteService Vignette { get; }
        public IPointerService Pointer { get; }
        public ITextBoxService TextBox { get; }
        public IInputLockService InputLock { get; }
        public IPersistenceService Persistence { get; }
        public IGameCommandRegistry Commands { get; }
        public ITutorialSignalBus Signals { get; }
        public ITutorialTargetRegistry Targets { get; }
        public IInputProvider Input { get; }

        public TutorialGraph Graph { get; internal set; }

        /// <summary>Per-run key/value store for sharing state between nodes.</summary>
        public Dictionary<string, object> Blackboard { get; } = new Dictionary<string, object>();

        public TutorialRunContext(
            IVignetteService vignette,
            IPointerService pointer,
            ITextBoxService textBox,
            IInputLockService inputLock,
            IPersistenceService persistence,
            IGameCommandRegistry commands,
            ITutorialSignalBus signals,
            ITutorialTargetRegistry targets,
            IInputProvider input)
        {
            Vignette = vignette;
            Pointer = pointer;
            TextBox = textBox;
            InputLock = inputLock;
            Persistence = persistence;
            Commands = commands;
            Signals = signals;
            Targets = targets;
            Input = input;
        }

        /// <summary>Resolves a target reference to its provider, or null if not present.</summary>
        public ITutorialTarget Resolve(TutorialTargetRef reference)
        {
            if (!reference.HasValue) return null;
            return Targets != null && Targets.TryResolve(reference.TargetId, out var t) ? t : null;
        }

        /// <summary>Pulls the value feeding a node's data input port from its connected producer, or null.</summary>
        public object EvaluateInput(TutorialNode node, string inputPort)
        {
            if (Graph == null || node == null) return null;
            if (Graph.TryGetDataSource(node.Id, inputPort, out var fromId, out var fromPort))
            {
                var producer = Graph.FindNode(fromId);
                return producer != null ? producer.EvaluatePort(fromPort, this) : null;
            }
            return null;
        }

        /// <summary>
        /// Resolves a target for a consumer node: if its <paramref name="inputPort"/> is connected to a
        /// target provider node, that provider's target is used; otherwise the node's own reference.
        /// </summary>
        public ITutorialTarget ResolveTargetInput(TutorialNode node, string inputPort, TutorialTargetRef fallback)
        {
            var value = EvaluateInput(node, inputPort);
            if (value is ITutorialTarget target) return target;
            if (value is TutorialTargetRef reference) return Resolve(reference);
            return Resolve(fallback);
        }

        /// <summary>Resolves ALL targets connected to a multi-capacity input port (e.g. the vignette's).</summary>
        public List<ITutorialTarget> ResolveTargetInputs(TutorialNode node, string inputPort)
        {
            var result = new List<ITutorialTarget>();
            if (Graph == null || node == null) return result;
            foreach (var (fromId, fromPort) in Graph.GetDataSources(node.Id, inputPort))
            {
                var producer = Graph.FindNode(fromId);
                var value = producer != null ? producer.EvaluatePort(fromPort, this) : null;
                if (value is ITutorialTarget target) result.Add(target);
                else if (value is TutorialTargetRef reference)
                {
                    var t = Resolve(reference);
                    if (t != null) result.Add(t);
                }
            }
            return result;
        }
    }
}
