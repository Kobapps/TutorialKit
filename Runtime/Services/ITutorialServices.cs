using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>Draws and animates the dimming vignette with a highlight hole.</summary>
    public interface IVignetteService
    {
        bool IsVisible { get; }
        UniTask ShowAsync(HighlightRequest request, CancellationToken ct);
        UniTask HideAsync(CancellationToken ct);
        void HideImmediate();
    }

    /// <summary>Shows an animated hand/arrow pointer performing a gesture.</summary>
    public interface IPointerService
    {
        bool IsVisible { get; }
        UniTask ShowAsync(PointerRequest request, CancellationToken ct);
        UniTask HideAsync(CancellationToken ct);
        void HideImmediate();
    }

    /// <summary>Shows styled text boxes (default or custom prefabs).</summary>
    public interface ITextBoxService
    {
        /// <summary>Register a custom text-box prefab under a key referenced by <see cref="TextBoxRequest.PrefabKey"/>.</summary>
        void RegisterPrefab(string key, GameObject prefab);
        /// <summary>Shows a text box. If <see cref="TextBoxRequest.WaitForDismiss"/> is true, completes when dismissed.</summary>
        UniTask ShowAsync(TextBoxRequest request, CancellationToken ct);
        UniTask HideAsync(string id, CancellationToken ct);
        void HideAllImmediate();
    }

    /// <summary>Locks/unlocks game interaction. Games adapt this to their own input systems.</summary>
    public interface IInputLockService
    {
        /// <summary>Lock a named interaction group, or all interaction when <paramref name="group"/> is null/empty.</summary>
        void Lock(string group = null);
        void Unlock(string group = null);
        bool IsLocked(string group = null);
        /// <summary>Raised as (group, locked). Group is empty string for the global lock.</summary>
        event Action<string, bool> LockChanged;
    }

    /// <summary>Persists tutorial completion/checkpoints. Games can supply their own save backend.</summary>
    public interface IPersistenceService
    {
        bool IsTutorialCompleted(string tutorialId);
        void SetTutorialCompleted(string tutorialId, bool completed = true);
        bool IsCheckpointReached(string tutorialId, string checkpointId);
        void SetCheckpoint(string tutorialId, string checkpointId);
        string GetValue(string key, string defaultValue = null);
        void SetValue(string key, string value);
        void Save();
        void ResetTutorial(string tutorialId);
        void ResetAll();
    }

    /// <summary>Registry of named game commands a tutorial can invoke and await.</summary>
    public interface IGameCommandRegistry
    {
        void Register(string commandId, Func<TutorialCommandContext, CancellationToken, UniTask> handler);
        void Register(string commandId, Action<TutorialCommandContext> handler);
        void Unregister(string commandId);
        bool Has(string commandId);
        UniTask InvokeAsync(string commandId, TutorialCommandContext context, CancellationToken ct);
    }

    /// <summary>Decoupled signal bus. Game code emits; wait nodes await.</summary>
    public interface ITutorialSignalBus
    {
        void Emit(string signalId, object payload = null);
        UniTask WaitAsync(string signalId, CancellationToken ct);
        IDisposable Subscribe(string signalId, Action<object> handler);
    }

    /// <summary>Resolves target ids to elements. <see cref="TutorialTarget"/> self-registers here, and
    /// games can register dynamic providers via <see cref="TutorialTargets"/>.</summary>
    public interface ITutorialTargetRegistry
    {
        void Register(string id, ITutorialTarget target);
        void Unregister(string id);
        bool TryResolve(string id, out ITutorialTarget target);
    }

    /// <summary>Abstraction over the active input backend (Input System / legacy).</summary>
    public interface IInputProvider
    {
        /// <summary>True on the frame any key/button/touch went down.</summary>
        bool AnyInputDownThisFrame { get; }
        /// <summary>True on the frame a primary pointer (mouse/touch) went down; outputs its screen position.</summary>
        bool TryGetPointerDown(out Vector2 screenPosition);
        /// <summary>Current primary pointer screen position.</summary>
        Vector2 PointerPosition { get; }
    }
}
