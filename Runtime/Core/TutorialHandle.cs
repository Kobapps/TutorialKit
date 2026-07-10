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
        /// <summary>Raised when the tutorial finishes, with the final status.</summary>
        public event Action<TutorialHandle> Finished;

        private readonly UniTaskCompletionSource _completionSource = new UniTaskCompletionSource();

        internal TutorialHandle(TutorialGraph graph, CancellationTokenSource cts)
        {
            Graph = graph;
            _cts = cts;
        }

        internal void MarkRunning() => Status = TutorialStatus.Running;

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

        /// <summary>Stops the tutorial and marks it complete in persistence (as if the user finished).</summary>
        public void Skip()
        {
            if (IsFinished) return;
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
