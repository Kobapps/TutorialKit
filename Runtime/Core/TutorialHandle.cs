using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// A running (or finished) tutorial. Lets callers observe progress, wait for completion,
    /// and control playback (skip / abort). Returned by <see cref="TutorialDirector.Play"/>.
    /// </summary>
    public sealed class TutorialHandle
    {
        private readonly CancellationTokenSource _cts;
        private bool _skipRequested;

        public TutorialGraph Graph { get; }
        public TutorialStatus Status { get; private set; } = TutorialStatus.Idle;
        public TutorialNode CurrentNode { get; private set; }

        /// <summary>Completes when the tutorial finishes for any reason. Never throws.</summary>
        public UniTask Completion => _completionSource.Task;

        /// <summary>Raised on the main thread when a node begins.</summary>
        public event Action<TutorialNode> NodeEntered;
        /// <summary>
        /// Raised when the tutorial actually begins. For a tutorial held behind
        /// <see cref="TutorialBusyPolicy.Queue"/> this fires later than <c>Play</c> returns, so callers
        /// that react to a start should use this rather than checking <see cref="IsRunning"/> immediately.
        /// </summary>
        public event Action<TutorialHandle> Started;
        /// <summary>Raised when the tutorial finishes, with the final status.</summary>
        public event Action<TutorialHandle> Finished;

        private readonly UniTaskCompletionSource _completionSource = new UniTaskCompletionSource();

        internal TutorialHandle(TutorialGraph graph, CancellationTokenSource cts)
        {
            Graph = graph;
            _cts = cts;
        }

        internal void MarkRunning()
        {
            Status = TutorialStatus.Running;
            Started?.Invoke(this);
        }

        /// <summary>True while this tutorial is waiting in the busy queue for its turn to start.</summary>
        public bool IsQueued => Status == TutorialStatus.Idle;

        internal void RaiseNodeEntered(TutorialNode node)
        {
            CurrentNode = node;
            NodeEntered?.Invoke(node);
        }

        internal void Complete(TutorialStatus status)
        {
            if (Status == TutorialStatus.Completed || Status == TutorialStatus.Aborted ||
                Status == TutorialStatus.Skipped || Status == TutorialStatus.Faulted)
                return;

            Status = status;
            CurrentNode = null;
            Finished?.Invoke(this);
            _completionSource.TrySetResult();
        }

        internal bool SkipRequested => _skipRequested;

        /// <summary>True when <see cref="Skip"/> would do anything: still live, and the graph allows skipping.</summary>
        public bool CanSkip => !IsFinished && (Graph == null || Graph.Settings.AllowSkip);

        /// <summary>
        /// Stops the tutorial and marks it complete in persistence (as if the user finished).
        /// Does nothing when the graph's settings have <c>Allow Skip</c> turned off.
        /// </summary>
        public void Skip()
        {
            if (IsFinished) return;
            if (Graph != null && !Graph.Settings.AllowSkip)
            {
                Debug.LogWarning($"[TutorialKit] '{Graph.TutorialId}' cannot be skipped (its settings have Allow Skip off).");
                return;
            }
            _skipRequested = true;
            _cts.Cancel();
        }

        /// <summary>Stops the tutorial without marking it complete (it may replay later).</summary>
        public void Abort()
        {
            if (IsFinished) return;
            _cts.Cancel();
        }

        public bool IsRunning => Status == TutorialStatus.Running;

        public bool IsFinished =>
            Status == TutorialStatus.Completed || Status == TutorialStatus.Aborted ||
            Status == TutorialStatus.Skipped || Status == TutorialStatus.Faulted;
    }
}
