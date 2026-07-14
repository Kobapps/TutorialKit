using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Runtime entry point and composition root. Owns the overlay and the adapter services,
    /// installs defaults for any adapter a game doesn't provide, and plays graphs via
    /// <see cref="Play(TutorialGraph, bool)"/>. Auto-created on demand and persists across scenes.
    /// Enforces each graph's <see cref="TutorialSettings"/> (play mode, busy policy, lock, pause).
    /// </summary>
    [AddComponentMenu("TutorialKit/Tutorial Director")]
    [DisallowMultipleComponent]
    public sealed class TutorialDirector : MonoBehaviour
    {
        public static TutorialDirector Current { get; private set; }

        /// <summary>Tutorials completed since the app launched. Backs <see cref="TutorialPlayMode.OncePerSession"/>.</summary>
        private static readonly HashSet<string> _playedThisSession = new HashSet<string>();

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
        private readonly List<PendingPlay> _queue = new List<PendingPlay>();

        /// <summary>A tutorial waiting for the running one to finish (<see cref="TutorialBusyPolicy.Queue"/>).</summary>
        private sealed class PendingPlay
        {
            public TutorialGraph Graph;
            public TutorialHandle Handle;
            public CancellationTokenSource Cts;
        }

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

        // ---- Play-history bookkeeping (keys live in the generic persistence key/value store) ----

        /// <summary>Persistence key holding how many times a tutorial has been played to completion.</summary>
        public static string PlayCountKey(string tutorialId) => $"{tutorialId}.plays";
        /// <summary>Persistence key holding when a tutorial last finished (UTC ticks), for cooldowns.</summary>
        public static string LastPlayedKey(string tutorialId) => $"{tutorialId}.lastPlayed";

        /// <summary>How many times a tutorial has been played to completion (or skipped).</summary>
        public static int GetPlayCount(IPersistenceService persistence, string tutorialId)
        {
            var raw = persistence?.GetValue(PlayCountKey(tutorialId), "0");
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        }

        /// <summary>Real time since a tutorial last finished, or null if it has never finished.</summary>
        public static TimeSpan? GetTimeSinceLastPlay(IPersistenceService persistence, string tutorialId)
        {
            var raw = persistence?.GetValue(LastPlayedKey(tutorialId), null);
            if (string.IsNullOrEmpty(raw) ||
                !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
                return null;
            var elapsed = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            // A clock rolled backwards (timezone change, manual set) would give a negative span; treat it as "long ago".
            return elapsed < TimeSpan.Zero ? TimeSpan.MaxValue : elapsed;
        }

        /// <summary>How many times this tutorial has been played to completion.</summary>
        public int GetPlayCount(string tutorialId) => GetPlayCount(EnsureInitialized()._persistence, tutorialId);

        /// <summary>True if this tutorial has already played during this app session.</summary>
        public static bool PlayedThisSession(string tutorialId) => _playedThisSession.Contains(tutorialId);

        // ---- Gating ----

        /// <summary>Whether this graph's <see cref="TutorialSettings"/> currently allow it to play.</summary>
        public bool CanPlay(TutorialGraph graph) => CanPlay(graph, out _);

        /// <summary>
        /// Whether this graph's <see cref="TutorialSettings"/> currently allow it to play, with a
        /// human-readable <paramref name="reason"/> when it doesn't. Does not consider whether another
        /// tutorial is running — that's the busy policy, applied by <see cref="Play"/>.
        /// </summary>
        public bool CanPlay(TutorialGraph graph, out string reason)
        {
            if (graph == null) { reason = "no graph"; return false; }
            EnsureInitialized();
            graph.Validate();

            var s = graph.Settings;
            var id = graph.TutorialId;

            switch (s.PlayMode)
            {
                case TutorialPlayMode.SingleUse:
                    if (_persistence.IsTutorialCompleted(id))
                    {
                        reason = "already completed (Play Mode is Single Use)";
                        return false;
                    }
                    break;

                case TutorialPlayMode.OncePerSession:
                    if (_playedThisSession.Contains(id))
                    {
                        reason = "already played this session";
                        return false;
                    }
                    break;

                case TutorialPlayMode.Recurring:
                    if (s.MaxPlays > 0)
                    {
                        int plays = GetPlayCount(_persistence, id);
                        if (plays >= s.MaxPlays)
                        {
                            reason = $"reached its {s.MaxPlays}-play limit";
                            return false;
                        }
                    }
                    if (s.CooldownSeconds > 0f)
                    {
                        var since = GetTimeSinceLastPlay(_persistence, id);
                        if (since.HasValue && since.Value.TotalSeconds < s.CooldownSeconds)
                        {
                            reason = $"cooling down ({s.CooldownSeconds - since.Value.TotalSeconds:0.#}s left)";
                            return false;
                        }
                    }
                    break;
            }

            reason = null;
            return true;
        }

        // ---- Playback ----

        /// <summary>
        /// Plays a graph, honouring its <see cref="TutorialSettings"/>. Returns an already-completed handle
        /// if the settings currently disallow it, null if the busy policy dropped it, or a handle that is
        /// still idle if the busy policy queued it (watch <see cref="TutorialHandle.Started"/> for that one).
        /// <paramref name="force"/> bypasses both the play-mode gating and the busy policy, always starting
        /// immediately — it's what the editor's Test button uses.
        /// </summary>
        public TutorialHandle Play(TutorialGraph graph, bool force = false)
        {
            if (graph == null) { Debug.LogWarning("[TutorialKit] Play called with a null graph."); return null; }
            EnsureInitialized();

            if (!force && !CanPlay(graph, out _))
            {
                var done = new TutorialHandle(graph, new CancellationTokenSource());
                done.Complete(TutorialStatus.Completed);
                return done;
            }

            if (_activeHandle != null && _activeHandle.IsRunning)
            {
                // "force" means play it now (the editor's Test button), so it always wins the tie-break.
                switch (force ? TutorialBusyPolicy.Interrupt : graph.Settings.WhenBusy)
                {
                    case TutorialBusyPolicy.Ignore:
                        Debug.Log($"[TutorialKit] Dropped '{graph.TutorialId}' — '{_activeHandle.Graph.TutorialId}' is playing and its When Busy setting is Ignore.");
                        return null;

                    case TutorialBusyPolicy.Queue:
                    {
                        var queuedCts = new CancellationTokenSource();
                        var queuedHandle = new TutorialHandle(graph, queuedCts);
                        _queue.Add(new PendingPlay { Graph = graph, Handle = queuedHandle, Cts = queuedCts });
                        return queuedHandle;
                    }

                    default:
                        Debug.LogWarning($"[TutorialKit] Starting '{graph.TutorialId}' while '{_activeHandle.Graph.TutorialId}' is running; aborting the previous one.");
                        _activeHandle.Abort();
                        break;
                }
            }

            var cts = new CancellationTokenSource();
            var handle = new TutorialHandle(graph, cts);
            StartRun(handle, graph, cts);
            return handle;
        }

        private void StartRun(TutorialHandle handle, TutorialGraph graph, CancellationTokenSource cts)
        {
            EnsureOverlay();
            RunInternal(handle, graph, BuildContext(), cts).Forget();
        }

        /// <summary>Starts the next tutorial queued behind the one that just finished, if any.</summary>
        private void StartNextQueued()
        {
            // Something interrupted us and is running now; it will drain the queue when it ends.
            if (_activeHandle != null && _activeHandle.IsRunning) return;

            while (_queue.Count > 0)
            {
                var pending = _queue[0];
                _queue.RemoveAt(0);

                // A queued tutorial can be aborted (or its graph unloaded) while it waits.
                if (pending.Graph == null || pending.Handle.IsFinished || pending.Cts.IsCancellationRequested)
                {
                    pending.Handle.Complete(TutorialStatus.Aborted);
                    pending.Cts.Dispose();
                    continue;
                }

                StartRun(pending.Handle, pending.Graph, pending.Cts);
                return;
            }
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
                ApplyRunState(graph.Settings);
                await player.RunAsync(graph, ctx, cts.Token);
                RecordPlay(graph);
                handle.Complete(TutorialStatus.Completed);
            }
            catch (OperationCanceledException)
            {
                if (handle.SkipRequested)
                {
                    RecordPlay(graph);
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
                // An interrupting tutorial has already taken ownership of the overlay, lock, timeScale and
                // context by the time this runs, so only the tutorial that is still current tears them down.
                bool stillCurrent = _activeHandle == handle;
                if (stillCurrent)
                {
                    ReleaseRunState();
                    CleanupOverlay();
                    _activeHandle = null;
                    _activeContext = null;
                }
                Finished?.Invoke(handle);
                cts.Dispose();
                if (stillCurrent) StartNextQueued();
            }
        }

        // ---- Game state owned by whichever tutorial is currently active ----

        private bool _pausedByTutorial;
        private float _timeScaleBeforeTutorial = 1f;
        private bool _lockedByTutorial;
        private string _lockedGroup;

        /// <summary>
        /// Brings the game state in line with the active tutorial's settings. Held at director level, not
        /// per run, so handing over to an interrupting or queued tutorial neither double-captures
        /// <see cref="Time.timeScale"/> (which would strand the game paused) nor drops a lock it still wants.
        /// </summary>
        private void ApplyRunState(TutorialSettings settings)
        {
            if (settings.PauseGameWhilePlaying)
            {
                if (!_pausedByTutorial)
                {
                    _timeScaleBeforeTutorial = Time.timeScale;
                    _pausedByTutorial = true;
                }
                Time.timeScale = 0f;
            }
            else ReleasePause();

            if (settings.LockInputWhilePlaying)
            {
                if (_lockedByTutorial && _lockedGroup != settings.InputLockGroup)
                    ReleaseLock();
                if (!_lockedByTutorial)
                {
                    _inputLock.Lock(settings.InputLockGroup);
                    _lockedGroup = settings.InputLockGroup;
                    _lockedByTutorial = true;
                }
            }
            else ReleaseLock();
        }

        private void ReleaseRunState()
        {
            ReleasePause();
            ReleaseLock();
        }

        private void ReleasePause()
        {
            if (!_pausedByTutorial) return;
            Time.timeScale = _timeScaleBeforeTutorial;
            _pausedByTutorial = false;
        }

        private void ReleaseLock()
        {
            if (!_lockedByTutorial) return;
            _inputLock.Unlock(_lockedGroup);
            _lockedByTutorial = false;
            _lockedGroup = null;
        }

        /// <summary>
        /// Records that a tutorial ran to its end (completed or skipped): marks it done for Single Use,
        /// remembers it for this session, and updates the play count / timestamp the Recurring limits use.
        /// An aborted tutorial doesn't get here, so it never burns a play.
        /// </summary>
        private void RecordPlay(TutorialGraph graph)
        {
            var id = graph.TutorialId;
            _playedThisSession.Add(id);

            if (graph.Settings.PersistsCompletion)
                _persistence.SetTutorialCompleted(id, true);

            _persistence.SetValue(PlayCountKey(id),
                (GetPlayCount(_persistence, id) + 1).ToString(CultureInfo.InvariantCulture));
            _persistence.SetValue(LastPlayedKey(id),
                DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
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
            // Never leave the game paused or input-locked because the director went away mid-tutorial.
            ReleaseRunState();

            // Never leave a queued caller awaiting a Completion that can no longer happen.
            foreach (var pending in _queue)
            {
                pending.Handle.Complete(TutorialStatus.Aborted);
                pending.Cts.Dispose();
            }
            _queue.Clear();
            if (Current == this) Current = null;
        }

        /// <summary>Forgets which tutorials played this session (backs <see cref="TutorialPlayMode.OncePerSession"/>).
        /// Runs automatically on app start, including when Enter Play Mode options skip the domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearSessionHistory() => _playedThisSession.Clear();
    }
}
