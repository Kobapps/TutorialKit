using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TutorialKit
{
    /// <summary>
    /// Starts a <see cref="TutorialGraph"/> from a game trigger, gated by conditions. Attach to a
    /// scene object; choose when it fires (component lifecycle, a signal, or a manual API call).
    /// </summary>
    [AddComponentMenu("TutorialKit/Tutorial Trigger")]
    public class TutorialTrigger : MonoBehaviour
    {
        [Header("What to play")]
        [SerializeField] private TutorialGraph graph;

        [Header("When")]
        [SerializeField] private TutorialStartMode startMode = TutorialStartMode.OnStart;
        [Tooltip("Signal id that triggers playback when Start Mode = OnSignal.")]
        [SerializeField] private string startSignal;
        [Tooltip("Delay (seconds) before playing once triggered.")]
        [SerializeField, Min(0f)] private float delay = 0f;

        [Header("Gating")]
        [Tooltip("Don't replay if the tutorial is already marked completed in persistence.")]
        [SerializeField] private bool onlyIfNotCompleted = true;
        [Tooltip("Force replay even if completed (useful for testing).")]
        [SerializeField] private bool forceReplay = false;
        [SerializeField] private List<TutorialCondition> conditions = new List<TutorialCondition>();

        [Header("Events")]
        public UnityEvent onPlayStarted;
        public UnityEvent onPlayFinished;

        private System.IDisposable _signalSub;

        public TutorialGraph Graph { get => graph; set => graph = value; }

        protected virtual void OnEnable()
        {
            if (startMode == TutorialStartMode.OnEnable) Schedule();
            else if (startMode == TutorialStartMode.OnSignal && !string.IsNullOrEmpty(startSignal))
                _signalSub = TutorialDirector.EnsureExists().Signals.Subscribe(startSignal, _ => TryPlay());
        }

        protected virtual void Start()
        {
            if (startMode == TutorialStartMode.OnStart) Schedule();
        }

        protected virtual void OnDisable()
        {
            _signalSub?.Dispose();
            _signalSub = null;
        }

        private void Schedule()
        {
            if (delay <= 0f) TryPlay();
            else DelayedPlay(delay).Forget();
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid DelayedPlay(float seconds)
        {
            await Cysharp.Threading.Tasks.UniTask.Delay(System.TimeSpan.FromSeconds(seconds), true);
            if (this != null && isActiveAndEnabled) TryPlay();
        }

        /// <summary>Evaluates gating and, if allowed, plays the graph. Returns the handle (or null).</summary>
        public TutorialHandle TryPlay()
        {
            if (graph == null)
            {
                Debug.LogWarning($"[TutorialKit] TutorialTrigger on '{name}' has no graph assigned.", this);
                return null;
            }

            var director = TutorialDirector.EnsureExists();

            if (!forceReplay && onlyIfNotCompleted && director.Persistence.IsTutorialCompleted(graph.TutorialId))
                return null;

            for (int i = 0; i < conditions.Count; i++)
                if (conditions[i] != null && !conditions[i].IsSatisfied(director))
                    return null;

            var handle = director.Play(graph, force: forceReplay);
            if (handle != null && handle.IsRunning)
            {
                onPlayStarted?.Invoke();
                handle.Finished += _ => onPlayFinished?.Invoke();
            }
            return handle;
        }
    }
}
