using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TutorialKit
{
    /// <summary>Default persistence backed by <see cref="PlayerPrefs"/>. Replaceable via an adapter.</summary>
    public sealed class PlayerPrefsPersistenceService : IPersistenceService
    {
        private const string Prefix = "tk.";
        private readonly HashSet<string> _tutorialIds = new HashSet<string>();

        private static string DoneKey(string tid) => $"{Prefix}{tid}.done";
        private static string CpKey(string tid, string cid) => $"{Prefix}{tid}.cp.{cid}";
        private static string ValKey(string key) => $"{Prefix}kv.{key}";

        public bool IsTutorialCompleted(string tutorialId) => PlayerPrefs.GetInt(DoneKey(tutorialId), 0) == 1;

        public void SetTutorialCompleted(string tutorialId, bool completed = true)
        {
            _tutorialIds.Add(tutorialId);
            PlayerPrefs.SetInt(DoneKey(tutorialId), completed ? 1 : 0);
            Save();
        }

        public bool IsCheckpointReached(string tutorialId, string checkpointId) =>
            PlayerPrefs.GetInt(CpKey(tutorialId, checkpointId), 0) == 1;

        public void SetCheckpoint(string tutorialId, string checkpointId)
        {
            _tutorialIds.Add(tutorialId);
            PlayerPrefs.SetInt(CpKey(tutorialId, checkpointId), 1);
            Save();
        }

        public string GetValue(string key, string defaultValue = null) => PlayerPrefs.GetString(ValKey(key), defaultValue);
        public void SetValue(string key, string value) { PlayerPrefs.SetString(ValKey(key), value); Save(); }

        public void Save() => PlayerPrefs.Save();

        public void ResetTutorial(string tutorialId)
        {
            PlayerPrefs.DeleteKey(DoneKey(tutorialId));
            // Checkpoint keys are per-checkpoint; callers that need a full wipe should use ResetAll.
            Save();
        }

        public void ResetAll()
        {
            // Only clears keys we can enumerate this session; a hard wipe would need a key registry.
            foreach (var tid in _tutorialIds)
                PlayerPrefs.DeleteKey(DoneKey(tid));
            Save();
        }
    }

    /// <summary>Default input-lock: tracks locked groups and raises events. Games hook <see cref="LockChanged"/>.</summary>
    public sealed class DefaultInputLockService : IInputLockService
    {
        private const string GlobalGroup = "";
        private readonly HashSet<string> _locked = new HashSet<string>();

        public event Action<string, bool> LockChanged;

        public void Lock(string group = null)
        {
            group ??= GlobalGroup;
            if (_locked.Add(group))
                LockChanged?.Invoke(group, true);
        }

        public void Unlock(string group = null)
        {
            group ??= GlobalGroup;
            if (_locked.Remove(group))
                LockChanged?.Invoke(group, false);
        }

        public bool IsLocked(string group = null)
        {
            group ??= GlobalGroup;
            return _locked.Contains(GlobalGroup) || _locked.Contains(group);
        }
    }

    /// <summary>Default command registry mapping ids to async handlers.</summary>
    public sealed class DefaultGameCommandRegistry : IGameCommandRegistry
    {
        private readonly Dictionary<string, Func<TutorialCommandContext, CancellationToken, UniTask>> _handlers =
            new Dictionary<string, Func<TutorialCommandContext, CancellationToken, UniTask>>();

        public void Register(string commandId, Func<TutorialCommandContext, CancellationToken, UniTask> handler)
        {
            if (string.IsNullOrEmpty(commandId) || handler == null) return;
            _handlers[commandId] = handler;
        }

        public void Register(string commandId, Action<TutorialCommandContext> handler)
        {
            if (handler == null) return;
            Register(commandId, (ctx, _) => { handler(ctx); return UniTask.CompletedTask; });
        }

        public void Unregister(string commandId) => _handlers.Remove(commandId);

        public bool Has(string commandId) => !string.IsNullOrEmpty(commandId) && _handlers.ContainsKey(commandId);

        public UniTask InvokeAsync(string commandId, TutorialCommandContext context, CancellationToken ct)
        {
            if (_handlers.TryGetValue(commandId, out var h))
                return h(context, ct);

            Debug.LogWarning($"[TutorialKit] No game command registered for id '{commandId}'. Skipping.");
            return UniTask.CompletedTask;
        }
    }

    /// <summary>Default in-memory signal bus.</summary>
    public sealed class DefaultTutorialSignalBus : ITutorialSignalBus
    {
        private readonly Dictionary<string, Action<object>> _handlers = new Dictionary<string, Action<object>>();

        public void Emit(string signalId, object payload = null)
        {
            if (string.IsNullOrEmpty(signalId)) return;
            if (_handlers.TryGetValue(signalId, out var h))
                h?.Invoke(payload);
        }

        public IDisposable Subscribe(string signalId, Action<object> handler)
        {
            if (string.IsNullOrEmpty(signalId) || handler == null) return Disposable.Empty;
            _handlers.TryGetValue(signalId, out var existing);
            _handlers[signalId] = existing + handler;
            return new Subscription(this, signalId, handler);
        }

        private void Remove(string signalId, Action<object> handler)
        {
            if (_handlers.TryGetValue(signalId, out var existing))
            {
                existing -= handler;
                if (existing == null) _handlers.Remove(signalId);
                else _handlers[signalId] = existing;
            }
        }

        public async UniTask WaitAsync(string signalId, CancellationToken ct)
        {
            var tcs = new UniTaskCompletionSource();
            Action<object> handler = _ => tcs.TrySetResult();
            var sub = Subscribe(signalId, handler);
            using (ct.Register(() => tcs.TrySetCanceled()))
            {
                try { await tcs.Task; }
                finally { sub.Dispose(); }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private DefaultTutorialSignalBus _bus;
            private readonly string _id;
            private readonly Action<object> _handler;
            public Subscription(DefaultTutorialSignalBus bus, string id, Action<object> handler)
            { _bus = bus; _id = id; _handler = handler; }
            public void Dispose()
            {
                if (_bus == null) return;
                _bus.Remove(_id, _handler);
                _bus = null;
            }
        }

        private sealed class EmptyDisposable : IDisposable { public void Dispose() { } }
        private static class Disposable { public static readonly IDisposable Empty = new EmptyDisposable(); }
    }

    /// <summary>Default target registry keyed by id.</summary>
    public sealed class DefaultTutorialTargetRegistry : ITutorialTargetRegistry
    {
        private readonly Dictionary<string, ITutorialTarget> _targets = new Dictionary<string, ITutorialTarget>();

        public void Register(string id, ITutorialTarget target)
        {
            if (string.IsNullOrEmpty(id) || target == null) return;
            _targets[id] = target;
        }

        public void Unregister(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _targets.Remove(id);
        }

        public bool TryResolve(string id, out ITutorialTarget target)
        {
            target = null;
            if (string.IsNullOrEmpty(id)) return false;
            return _targets.TryGetValue(id, out target) && !IsDestroyedUnityObject(target);
        }

        private static bool IsDestroyedUnityObject(ITutorialTarget target)
        {
            // A TutorialTarget component may have been destroyed; Unity's fake-null needs a cast check.
            return target is UnityEngine.Object obj && obj == null;
        }
    }

    /// <summary>Default input provider using the Input System.</summary>
    public sealed class InputSystemInputProvider : IInputProvider
    {
        public bool AnyInputDownThisFrame
        {
            get
            {
                var kb = Keyboard.current;
                if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
                var m = Mouse.current;
                if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)) return true;
                var ts = Touchscreen.current;
                if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) return true;
                return false;
            }
        }

        public bool TryGetPointerDown(out Vector2 screenPosition)
        {
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame)
            {
                screenPosition = m.position.ReadValue();
                return true;
            }
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = ts.primaryTouch.position.ReadValue();
                return true;
            }
            screenPosition = default;
            return false;
        }

        public Vector2 PointerPosition
        {
            get
            {
                var m = Mouse.current;
                if (m != null) return m.position.ReadValue();
                var ts = Touchscreen.current;
                if (ts != null) return ts.primaryTouch.position.ReadValue();
                return Vector2.zero;
            }
        }
    }
}
