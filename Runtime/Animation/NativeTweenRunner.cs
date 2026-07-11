using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// The built-in animation backend — no third-party dependency. Drives value/sequence animations on
    /// unscaled time via UniTask (one yield per frame). This is the default runner.
    /// </summary>
    public sealed class NativeTweenRunner : ITutorialTweenRunner
    {
        public string Id => TutorialTween.NativeId;

        public ITutorialTween Animate(float duration, TutorialEase ease, Action<float> onUpdate, float delay = 0f, Action onComplete = null)
            => new NativeTween(duration, ease, onUpdate, delay, onComplete);

        public ITutorialTweenSequence Sequence() => new NativeSequence();
    }

    internal static class TutorialEasing
    {
        public static float Apply(TutorialEase e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case TutorialEase.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case TutorialEase.OutQuad: return 1f - (1f - t) * (1f - t);
                case TutorialEase.InQuad: return t * t;
                case TutorialEase.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case TutorialEase.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case TutorialEase.OutBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                default: return t; // Linear
            }
        }

        // Runs a value from 0→1 (or 1→0 when reversed) over duration, eased, one step per frame.
        public static async UniTask RunValue(float duration, TutorialEase ease, Action<float> onUpdate, bool reverse, CancellationToken ct)
        {
            if (duration <= 0f)
            {
                onUpdate?.Invoke(Apply(ease, reverse ? 0f : 1f));
                return;
            }
            float t = 0f;
            while (t < duration)
            {
                ct.ThrowIfCancellationRequested();
                float p = t / duration;
                onUpdate?.Invoke(Apply(ease, reverse ? 1f - p : p));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                t += Time.unscaledDeltaTime;
            }
            onUpdate?.Invoke(Apply(ease, reverse ? 0f : 1f));
        }
    }

    internal sealed class NativeTween : ITutorialTween
    {
        private readonly float _duration, _delay;
        private readonly TutorialEase _ease;
        private readonly Action<float> _onUpdate;
        private readonly Action _onComplete;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly UniTaskCompletionSource _done = new UniTaskCompletionSource();
        private bool _active = true;

        public NativeTween(float duration, TutorialEase ease, Action<float> onUpdate, float delay, Action onComplete)
        {
            _duration = duration; _ease = ease; _onUpdate = onUpdate; _delay = delay; _onComplete = onComplete;
            Run().Forget();
        }

        public bool IsActive => _active;

        private async UniTaskVoid Run()
        {
            var ct = _cts.Token;
            try
            {
                if (_delay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(_delay), true, PlayerLoopTiming.Update, ct);
                await TutorialEasing.RunValue(_duration, _ease, _onUpdate, false, ct);
                _onComplete?.Invoke();
            }
            catch (OperationCanceledException) { }
            finally { _active = false; _done.TrySetResult(); }
        }

        public void Kill(bool complete)
        {
            if (!_active) return;
            _active = false;
            if (complete)
            {
                _onUpdate?.Invoke(TutorialEasing.Apply(_ease, 1f));
                _onComplete?.Invoke();
            }
            _cts.Cancel();
            _done.TrySetResult();
        }

        public async UniTask ToUniTask(CancellationToken ct)
        {
            if (!_active) return;
            using (ct.Register(() => Kill(false)))
                await _done.Task;
        }
    }

    internal sealed class NativeSequence : ITutorialTweenSequence
    {
        private struct AnimStep { public float Duration, Delay; public TutorialEase Ease; public Action<float> OnUpdate; }

        private abstract class Entry { }
        private sealed class AnimGroup : Entry { public readonly List<AnimStep> Steps = new List<AnimStep>(); }
        private sealed class CallbackEntry : Entry { public Action Cb; }
        private sealed class IntervalEntry : Entry { public float Seconds; }

        private readonly List<Entry> _entries = new List<Entry>();
        private int _loops = 1;
        private TutorialLoopMode _loopMode = TutorialLoopMode.Restart;

        private CancellationTokenSource _cts;
        private readonly UniTaskCompletionSource _done = new UniTaskCompletionSource();
        private bool _active;

        public bool IsActive => _active;

        public ITutorialTweenSequence Append(float duration, TutorialEase ease, Action<float> onUpdate, float delay = 0f)
        {
            var g = new AnimGroup();
            g.Steps.Add(new AnimStep { Duration = duration, Delay = delay, Ease = ease, OnUpdate = onUpdate });
            _entries.Add(g);
            return this;
        }

        public ITutorialTweenSequence Join(float duration, TutorialEase ease, Action<float> onUpdate, float delay = 0f)
        {
            if (_entries.Count > 0 && _entries[_entries.Count - 1] is AnimGroup g)
            {
                g.Steps.Add(new AnimStep { Duration = duration, Delay = delay, Ease = ease, OnUpdate = onUpdate });
                return this;
            }
            return Append(duration, ease, onUpdate, delay);
        }

        public ITutorialTweenSequence AppendCallback(Action callback) { _entries.Add(new CallbackEntry { Cb = callback }); return this; }
        public ITutorialTweenSequence AppendInterval(float seconds) { _entries.Add(new IntervalEntry { Seconds = seconds }); return this; }
        public ITutorialTweenSequence SetLoops(int loops, TutorialLoopMode mode = TutorialLoopMode.Restart) { _loops = loops; _loopMode = mode; return this; }

        public ITutorialTweenSequence Play()
        {
            if (_active) return this;
            _active = true;
            _cts = new CancellationTokenSource();
            Run(_cts.Token).Forget();
            return this;
        }

        private async UniTaskVoid Run(CancellationToken ct)
        {
            try
            {
                int i = 0;
                while (_loops < 0 || i < _loops)
                {
                    bool reverse = _loopMode == TutorialLoopMode.Yoyo && (i % 2 == 1);
                    for (int k = 0; k < _entries.Count; k++)
                    {
                        var entry = _entries[reverse ? _entries.Count - 1 - k : k];
                        ct.ThrowIfCancellationRequested();
                        switch (entry)
                        {
                            case CallbackEntry cb:
                                cb.Cb?.Invoke();
                                break;
                            case IntervalEntry iv:
                                if (iv.Seconds > 0f)
                                    await UniTask.Delay(TimeSpan.FromSeconds(iv.Seconds), true, PlayerLoopTiming.Update, ct);
                                break;
                            case AnimGroup g:
                                await RunGroup(g, reverse, ct);
                                break;
                        }
                    }
                    i++;
                }
            }
            catch (OperationCanceledException) { }
            finally { _active = false; _done.TrySetResult(); }
        }

        private static async UniTask RunGroup(AnimGroup g, bool reverse, CancellationToken ct)
        {
            if (g.Steps.Count == 1) { await RunStep(g.Steps[0], reverse, ct); return; }
            var tasks = new List<UniTask>(g.Steps.Count);
            foreach (var s in g.Steps) tasks.Add(RunStep(s, reverse, ct));
            await UniTask.WhenAll(tasks);
        }

        private static async UniTask RunStep(AnimStep s, bool reverse, CancellationToken ct)
        {
            if (s.Delay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(s.Delay), true, PlayerLoopTiming.Update, ct);
            await TutorialEasing.RunValue(s.Duration, s.Ease, s.OnUpdate, reverse, ct);
        }

        public void Kill(bool complete)
        {
            if (!_active) return;
            _active = false;
            _cts?.Cancel();
            _done.TrySetResult();
        }

        public async UniTask ToUniTask(CancellationToken ct)
        {
            if (!_active) return;
            using (ct.Register(() => Kill(false)))
                await _done.Task;
        }
    }
}
