using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Runtime entry point and composition root. Owns the overlay and the adapter services,
    /// installs defaults for any adapter a game doesn't provide, and plays graphs via
    /// <see cref="Play(TutorialGraph, bool)"/>. Auto-created on demand and persists across scenes.
    /// </summary>
    [AddComponentMenu("TutorialKit/Tutorial Director")]
    [DisallowMultipleComponent]
    public sealed class TutorialDirector : MonoBehaviour
    {
        public static TutorialDirector Current { get; private set; }

        // ---- Services (defaults installed in Initialize; override before first Play) ----
        private IPersistenceService _persistence;
        private IInputLockService _inputLock;
        private IGameCommandRegistry _commands;
        private ITutorialSignalBus _signals;
        private ITutorialTargetRegistry _targets;
        private IInputProvider _input;
        private TutorialOverlay _overlay;

        private bool _initialized;
        private TutorialHandle _activeHandle;
        private TutorialRunContext _activeContext;

        public IPersistenceService Persistence => EnsureInitialized()._persistence;
        public IInputLockService InputLock => EnsureInitialized()._inputLock;
        public IGameCommandRegistry Commands => EnsureInitialized()._commands;
        public ITutorialSignalBus Signals => EnsureInitialized()._signals;
        public ITutorialTargetRegistry Targets => EnsureInitialized()._targets;
        public TutorialOverlay Overlay => _overlay;
        public TutorialHandle ActiveHandle => _activeHandle;
        public bool IsPlaying => _activeHandle != null && _activeHandle.IsRunning;

        /// <summary>The running tutorial's context (services + blackboard), or null. For the live debugger.</summary>
        public TutorialRunContext ActiveContext => _activeContext;
        /// <summary>Live view of the running blackboard, or null.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, object> ActiveBlackboard => _activeContext?.Blackboard;

        /// <summary>Raised when any tutorial starts.</summary>
        public event Action<TutorialHandle> Started;
        /// <summary>
        /// Raised (statically, across every director) whenever a tutorial starts. The editor uses this
        /// to auto-open the graph window on the running tutorial, even if no director instance is known
        /// to it yet. Runtime code should prefer the instance <see cref="Started"/> event.
        /// </summary>
        public static event Action<TutorialHandle> AnyStarted;
        /// <summary>Raised when any tutorial finishes.</summary>
        public event Action<TutorialHandle> Finished;
        /// <summary>Raised when a node begins (graph, node). Used by the editor live debugger.</summary>
        public event Action<TutorialGraph, TutorialNode> NodeEntered;
        /// <summary>Raised when a node finishes, with the chosen output port. Used to trace traversed edges.</summary>
        public event Action<TutorialGraph, TutorialNode, string> NodeExited;

        public static TutorialDirector EnsureExists()
        {
            if (Current == null)
            {
                var go = new GameObject("TutorialDirector");
                Current = go.AddComponent<TutorialDirector>();
            }
            return Current;
        }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }
            Current = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        private TutorialDirector EnsureInitialized()
        {
            if (_initialized) return this;
            _initialized = true;
            _persistence ??= new PlayerPrefsPersistenceService();
            _inputLock ??= new DefaultInputLockService();
            _commands ??= new DefaultGameCommandRegistry();
            _signals ??= new DefaultTutorialSignalBus();
            _targets ??= new DefaultTutorialTargetRegistry();
            _input ??= new InputSystemInputProvider();
            return this;
        }

        private void EnsureOverlay()
        {
            if (_overlay != null) return;
            _overlay = TutorialOverlay.Create(_targets, transform);
        }

        // ---- Adapter overrides (call before the first Play) ----
        public void SetPersistence(IPersistenceService s) { if (s != null) _persistence = s; }
        public void SetInputLock(IInputLockService s) { if (s != null) _inputLock = s; }
        public void SetCommandRegistry(IGameCommandRegistry s) { if (s != null) _commands = s; }
        public void SetSignalBus(ITutorialSignalBus s) { if (s != null) _signals = s; }
        public void SetTargetRegistry(ITutorialTargetRegistry s)
        {
            if (s == null) return;
            _targets = s;
            if (_overlay != null) _overlay.SetTargetRegistry(s);
        }
        public void SetInputProvider(IInputProvider s) { if (s != null) _input = s; }

        // ---- Playback ----

        /// <summary>Plays a graph. Skips (returns a completed handle) if already completed and not forced.</summary>
        public TutorialHandle Play(TutorialGraph graph, bool force = false)
        {
            if (graph == null) { Debug.LogWarning("[TutorialKit] Play called with a null graph."); return null; }
            EnsureInitialized();

            if (!force && !graph.Repeatable && _persistence.IsTutorialCompleted(graph.TutorialId))
            {
                var done = new TutorialHandle(graph, new CancellationTokenSource());
                done.Complete(TutorialStatus.Completed);
                return done;
            }

            if (_activeHandle != null && _activeHandle.IsRunning)
            {
                Debug.LogWarning($"[TutorialKit] Starting '{graph.TutorialId}' while '{_activeHandle.Graph.TutorialId}' is running; aborting the previous one.");
                _activeHandle.Abort();
            }

            EnsureOverlay();

            var cts = new CancellationTokenSource();
            var handle = new TutorialHandle(graph, cts);
            var ctx = BuildContext();
            RunInternal(handle, graph, ctx, cts).Forget();
            return handle;
        }

        private TutorialRunContext BuildContext()
        {
            return new TutorialRunContext(
                _overlay.Vignette, _overlay.Pointer, _overlay.TextBox,
                _inputLock, _persistence, _commands, _signals, _targets, _input);
        }

        private async UniTaskVoid RunInternal(TutorialHandle handle, TutorialGraph graph, TutorialRunContext ctx, CancellationTokenSource cts)
        {
            var player = new TutorialPlayer();
            player.NodeEntered += node =>
            {
                handle.RaiseNodeEntered(node);
                NodeEntered?.Invoke(graph, node);
            };
            player.NodeExited += (node, port) => NodeExited?.Invoke(graph, node, port);

            _activeHandle = handle;
            _activeContext = ctx;
            handle.MarkRunning();
            Started?.Invoke(handle);
            AnyStarted?.Invoke(handle);

            try
            {
                await player.RunAsync(graph, ctx, cts.Token);
                if (!graph.Repeatable) _persistence.SetTutorialCompleted(graph.TutorialId, true);
                handle.Complete(TutorialStatus.Completed);
            }
            catch (OperationCanceledException)
            {
                if (handle.SkipRequested)
                {
                    if (!graph.Repeatable) _persistence.SetTutorialCompleted(graph.TutorialId, true);
                    handle.Complete(TutorialStatus.Skipped);
                }
                else handle.Complete(TutorialStatus.Aborted);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                handle.Complete(TutorialStatus.Faulted);
            }
            finally
            {
                CleanupOverlay();
                if (_activeHandle == handle) _activeHandle = null;
                _activeContext = null;
                Finished?.Invoke(handle);
                cts.Dispose();
            }
        }

        private void CleanupOverlay()
        {
            if (_overlay == null) return;
            _overlay.Vignette.HideImmediate();
            _overlay.Pointer.HideImmediate();
            _overlay.TextBox.HideAllImmediate();
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
