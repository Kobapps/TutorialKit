using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Base class for every step in a tutorial graph. Nodes are stored polymorphically on the
    /// <see cref="TutorialGraph"/> via <c>[SerializeReference]</c>.
    /// <para>
    /// A node exposes named <see cref="OutputPorts"/> and implements <see cref="ExecuteAsync"/>,
    /// returning the name of the port to follow next (or <c>null</c> to end the tutorial).
    /// </para>
    /// </summary>
    [Serializable]
    public abstract class TutorialNode
    {
        /// <summary>The conventional single output port name.</summary>
        public const string OutPort = "Out";

        [SerializeField, HideInInspector] private string id;
        [SerializeField, HideInInspector] private Vector2 editorPosition;

        /// <summary>Unique id within the graph. Assigned on creation.</summary>
        public string Id
        {
            get => id;
            set => id = value;
        }

        /// <summary>Node position in the graph editor canvas.</summary>
        public Vector2 EditorPosition
        {
            get => editorPosition;
            set => editorPosition = value;
        }

        private static readonly string[] SingleOut = { OutPort };

        /// <summary>Names of this node's output ports, left→right. Default: a single "Out".</summary>
        public virtual IReadOnlyList<string> OutputPorts => SingleOut;

        /// <summary>Whether this node accepts an incoming connection. Entry nodes may return false.</summary>
        public virtual bool HasInput => true;

        private static readonly TutorialDataPort[] NoData = Array.Empty<TutorialDataPort>();

        /// <summary>Typed data input ports (e.g. an optional injected "Target"). Default: none.</summary>
        public virtual IReadOnlyList<TutorialDataPort> InputDataPorts => NoData;

        /// <summary>Typed data output ports produced by this node (e.g. a "Target" from a provider). Default: none.</summary>
        public virtual IReadOnlyList<TutorialDataPort> OutputDataPorts => NoData;

        /// <summary>
        /// Produces the value for one of this node's <see cref="OutputDataPorts"/>, pulled on demand by
        /// a connected consumer. Override in provider nodes (may run game logic). Default: null.
        /// </summary>
        public virtual object EvaluatePort(string outputPort, TutorialRunContext ctx) => null;

        /// <summary>Title shown on the node in the editor. Defaults to the type name (minus "Node").</summary>
        public virtual string DisplayName
        {
            get
            {
                string n = GetType().Name;
                return n.EndsWith("Node", StringComparison.Ordinal) ? n.Substring(0, n.Length - 4) : n;
            }
        }

        /// <summary>Short one-line summary shown under the title in the editor (optional).</summary>
        public virtual string GetSummary(TutorialGraph graph) => null;

        /// <summary>
        /// Runs the step. Return the output port to follow, or <c>null</c> to end the tutorial.
        /// Must honour <paramref name="ct"/> and clean up any transient state it created.
        /// </summary>
        public abstract UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct);

        /// <summary>Assigns a fresh unique id if the node doesn't have one.</summary>
        public void EnsureId()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");
        }
    }
}
