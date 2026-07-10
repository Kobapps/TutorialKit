using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace TutorialKit
{
    /// <summary>
    /// Awaits a DOTween <see cref="Tween"/> as a <see cref="UniTask"/> without depending on the
    /// optional UniTask-DOTween integration. Cancellation kills the tween and throws.
    /// </summary>
    public static class TweenExtensions
    {
        public static async UniTask ToUniTaskSafe(this Tween tween, CancellationToken ct)
        {
            if (tween == null || !tween.IsActive())
                return;

            var tcs = new UniTaskCompletionSource();
            tween.OnComplete(() => tcs.TrySetResult());
            tween.OnKill(() => tcs.TrySetResult());

            using (ct.Register(() =>
            {
                tcs.TrySetCanceled(ct);
                if (tween.IsActive()) tween.Kill(false);
            }))
            {
                await tcs.Task;
            }
        }
    }
}
