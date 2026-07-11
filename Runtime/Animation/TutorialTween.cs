using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace TutorialKit
{
    /// <summary>Easing curves TutorialKit animations use (a small, backend-agnostic set).</summary>
    public enum TutorialEase { Linear, InOutSine, OutQuad, InQuad, OutBack, OutCubic, InOutQuad }

    /// <summary>How a looping sequence repeats.</summary>
    public enum TutorialLoopMode { Restart, Yoyo }

    /// <summary>A running animation (a single value tween or a sequence). Kill to stop; await to wait.</summary>
    public interface ITutorialTween
    {
        bool IsActive { get; }
        void Kill(bool complete = false);
        UniTask ToUniTask(CancellationToken ct);
    }

    /// <summary>
    /// A composable animation. <c>Append</c> runs after the previous step; <c>Join</c> runs alongside
    /// the previous step. Each animated step reports normalized, eased progress (0→1) via its callback,
    /// so the caller lerps whatever it likes. Call <c>Play</c> to start.
    /// </summary>
    public interface ITutorialTweenSequence : ITutorialTween
    {
        ITutorialTweenSequence Append(float duration, TutorialEase ease, Action<float> onUpdate, float delay = 0f);
        ITutorialTweenSequence Join(float duration, TutorialEase ease, Action<float> onUpdate, float delay = 0f);
        ITutorialTweenSequence AppendCallback(Action callback);
        ITutorialTweenSequence AppendInterval(float seconds);
        ITutorialTweenSequence SetLoops(int loops, TutorialLoopMode mode = TutorialLoopMode.Restart);
        ITutorialTweenSequence Play();
    }

    /// <summary>
    /// An animation backend. Implement this and <see cref="TutorialTween.Register"/> it to plug in a
    /// custom tween library; the built-in one and the optional DOTween adapter both implement it.
    /// All animations use unscaled time (tutorials run while the game is paused).
    /// </summary>
    public interface ITutorialTweenRunner
    {
        /// <summary>Stable id used to select this backend in settings (e.g. "native", "dotween").</summary>
        string Id { get; }

        /// <summary>Animate a value from 0→1 (eased) over <paramref name="duration"/>, auto-playing.</summary>
        ITutorialTween Animate(float duration, TutorialEase ease, Action<float> onUpdate, float delay = 0f, Action onComplete = null);

        /// <summary>A composable sequence — build with Append/Join/… then call Play.</summary>
        ITutorialTweenSequence Sequence();
    }

    /// <summary>
    /// Animation façade for the whole package. Every overlay animates through here, so the backend is
    /// swappable: a simple built-in runner (no third-party dependency), the DOTween adapter, or your own.
    /// The package never hard-depends on any tween library — the built-in runner is used unless another
    /// backend is registered and selected (via <c>TutorialKitSettings.TweenAdapterId</c>, set in Settings).
    /// </summary>
    public static class TutorialTween
    {
        public const string NativeId = "native";

        private static readonly Dictionary<string, ITutorialTweenRunner> _runners =
            new Dictionary<string, ITutorialTweenRunner>(StringComparer.OrdinalIgnoreCase);
        private static readonly ITutorialTweenRunner _native = new NativeTweenRunner();

        static TutorialTween() { Register(_native); }

        /// <summary>Register a backend so it can be selected by its <see cref="ITutorialTweenRunner.Id"/>.</summary>
        public static void Register(ITutorialTweenRunner runner)
        {
            if (runner != null && !string.IsNullOrEmpty(runner.Id)) _runners[runner.Id] = runner;
        }

        /// <summary>Whether a backend with this id is registered (e.g. is the DOTween adapter compiled in).</summary>
        public static bool IsAvailable(string id) => !string.IsNullOrEmpty(id) && _runners.ContainsKey(id);

        public static IEnumerable<string> AvailableIds => _runners.Keys;

        /// <summary>The active backend: the one selected in settings if registered, else the built-in one.</summary>
        public static ITutorialTweenRunner Active
        {
            get
            {
                var settings = TutorialKitSettings.Instance;
                var id = settings != null ? settings.TweenAdapterId : null;
                return !string.IsNullOrEmpty(id) && _runners.TryGetValue(id, out var r) ? r : _native;
            }
        }

        public static ITutorialTween Animate(float duration, TutorialEase ease, Action<float> onUpdate, float delay = 0f, Action onComplete = null)
            => Active.Animate(duration, ease, onUpdate, delay, onComplete);

        public static ITutorialTweenSequence Sequence() => Active.Sequence();

        /// <summary>Kill and clear a stored handle (no-op if null/finished).</summary>
        public static void Kill(ref ITutorialTween tween, bool complete = false)
        {
            tween?.Kill(complete);
            tween = null;
        }
    }
}
