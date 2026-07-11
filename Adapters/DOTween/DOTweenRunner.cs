// This whole assembly only compiles when the TUTORIALKIT_DOTWEEN define is set (see the asmdef's
// define constraint), which the user enables from TutorialKit Settings once DOTween is installed.
// It uses only DOTween's CORE api (DOTween.To) — never the extension "modules" (DOFade/DOMove/…) — so
// it doesn't require the DOTween module setup and can't break on a partial install.
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace TutorialKit.Adapters
{
    /// <summary>DOTween-backed animation runner for TutorialKit. Registered automatically when compiled.</summary>
    public sealed class DOTweenRunner : ITutorialTweenRunner
    {
        public const string AdapterId = "dotween";
        public string Id => AdapterId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOnLoad() => TutorialTween.Register(new DOTweenRunner());

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor() => TutorialTween.Register(new DOTweenRunner());
#endif

        public ITutorialTween Animate(float duration, TutorialEase ease, Action<float> onUpdate, float delay = 0f, Action onComplete = null)
        {
            var tween = MakeTween(duration, ease, onUpdate, delay);
            if (onComplete != null) tween.OnComplete(() => onComplete());
            return new DOTweenHandle(tween);
        }

        public ITutorialTweenSequence Sequence() => new DOTweenSequence();

        internal static Tween MakeTween(float duration, TutorialEase ease, Action<float> onUpdate, float delay)
        {
            float v = 0f;
            var t = DOTween.To(() => v, x => { v = x; onUpdate?.Invoke(x); }, 1f, duration)
                .SetEase(Map(ease))
                .SetUpdate(true); // unscaled — tutorials run while the game is paused
            if (delay > 0f) t.SetDelay(delay);
            return t;
        }

        internal static Ease Map(TutorialEase e)
        {
            switch (e)
            {
                case TutorialEase.InOutSine: return Ease.InOutSine;
                case TutorialEase.OutQuad: return Ease.OutQuad;
                case TutorialEase.InQuad: return Ease.InQuad;
                case TutorialEase.OutBack: return Ease.OutBack;
                case TutorialEase.OutCubic: return Ease.OutCubic;
                case TutorialEase.InOutQuad: return Ease.InOutQuad;
                default: return Ease.Linear;
            }
        }
    }

    internal sealed class DOTweenHandle : ITutorialTween
    {
        private readonly Tween _tween;
        public DOTweenHandle(Tween tween) { _tween = tween; }

        public bool IsActive => _tween != null && _tween.IsActive();

        public void Kill(bool complete = false)
        {
            if (_tween != null && _tween.IsActive()) _tween.Kill(complete);
        }

        public UniTask ToUniTask(CancellationToken ct) => Await(_tween, ct);

        internal static async UniTask Await(Tween tween, CancellationToken ct)
        {
            if (tween == null || !tween.IsActive()) return;
            var tcs = new UniTaskCompletionSource();
            tween.OnComplete(() => tcs.TrySetResult());
            tween.OnKill(() => tcs.TrySetResult());
            using (ct.Register(() => { tcs.TrySetCanceled(ct); if (tween.IsActive()) tween.Kill(false); }))
                await tcs.Task;
        }
    }

    internal sealed class DOTweenSequence : ITutorialTweenSequence
    {
        private readonly Sequence _seq;

        public DOTweenSequence()
        {
            _seq = DOTween.Sequence().SetUpdate(true);
            _seq.Pause(); // build while paused, then Play()
        }

        public bool IsActive => _seq != null && _seq.IsActive();

        public ITutorialTweenSequence Append(float duration, TutorialEase ease, Action<float> onUpdate, float delay = 0f)
        { _seq.Append(DOTweenRunner.MakeTween(duration, ease, onUpdate, delay)); return this; }

        public ITutorialTweenSequence Join(float duration, TutorialEase ease, Action<float> onUpdate, float delay = 0f)
        { _seq.Join(DOTweenRunner.MakeTween(duration, ease, onUpdate, delay)); return this; }

        public ITutorialTweenSequence AppendCallback(Action callback) { _seq.AppendCallback(() => callback?.Invoke()); return this; }
        public ITutorialTweenSequence AppendInterval(float seconds) { _seq.AppendInterval(seconds); return this; }

        public ITutorialTweenSequence SetLoops(int loops, TutorialLoopMode mode = TutorialLoopMode.Restart)
        { _seq.SetLoops(loops, mode == TutorialLoopMode.Yoyo ? LoopType.Yoyo : LoopType.Restart); return this; }

        public ITutorialTweenSequence Play() { _seq.Play(); return this; }

        public void Kill(bool complete = false) { if (_seq != null && _seq.IsActive()) _seq.Kill(complete); }

        public UniTask ToUniTask(CancellationToken ct) => DOTweenHandle.Await(_seq, ct);
    }
}
