using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TutorialKit
{
    /// <summary>
    /// Shows an animated hand/arrow pointer performing a gesture (point, tap, swipe, drag, merge).
    /// The pointer root follows a moving target for point/tap; path gestures animate between two
    /// captured endpoints. Real art can be assigned via <see cref="handSprite"/>/<see cref="arrowSprite"/>.
    /// </summary>
    public sealed class PointerView : MonoBehaviour, IPointerService
    {
        [SerializeField] private Sprite handSprite;
        [SerializeField] private Sprite arrowSprite;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private float pointerSize = 110f;

        private TutorialOverlay _overlay;
        private RectTransform _follow;   // moved to the target
        private RectTransform _inner;    // animated relative to _follow
        private Image _pointerImage;
        private Image _ring;
        private RectTransform _secondary; // for merge
        private Image _secondaryImage;

        private Sequence _sequence;
        private PointerRequest _request;
        private bool _followTarget;

        public bool IsVisible { get; private set; }

        internal void Init(TutorialOverlay overlay)
        {
            _overlay = overlay;

            _follow = CreateChild("Follow", transform as RectTransform);
            _inner = CreateChild("Inner", _follow);

            _ring = CreateImage("Ring", _inner, TutorialSpriteFactory.Ring, pointerSize * 1.4f);
            _ring.color = new Color(tint.r, tint.g, tint.b, 0f);

            _pointerImage = CreateImage("Pointer", _inner,
                handSprite != null ? handSprite : TutorialSpriteFactory.Hand, pointerSize);
            _pointerImage.color = tint;

            _secondary = CreateChild("Secondary", transform as RectTransform);
            _secondaryImage = CreateImage("SecondaryDot", _secondary, TutorialSpriteFactory.Dot, pointerSize * 0.7f);
            _secondaryImage.color = tint;

            gameObject.SetActive(false);
        }

        private static RectTransform CreateChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static Image CreateImage(string name, RectTransform parent, Sprite sprite, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        public UniTask ShowAsync(PointerRequest request, CancellationToken ct)
        {
            KillSequence();
            _request = request;
            _pointerImage.sprite = request.Kind == PointerKind.Arrow
                ? (arrowSprite != null ? arrowSprite : TutorialSpriteFactory.Arrow)
                : (handSprite != null ? handSprite : TutorialSpriteFactory.Hand);

            gameObject.SetActive(true);
            IsVisible = true;
            _inner.anchoredPosition = Vector2.zero;
            _inner.localScale = Vector3.one;
            _secondary.gameObject.SetActive(false);
            _ring.color = new Color(tint.r, tint.g, tint.b, 0f);

            Vector2 start = ResolvePosition(request.Target, request.ScreenOffset);
            _follow.anchoredPosition = start;

            _followTarget = request.Gesture == PointerGesture.Point || request.Gesture == PointerGesture.Tap;

            switch (request.Gesture)
            {
                case PointerGesture.Point: BuildPoint(request); break;
                case PointerGesture.Tap: BuildTap(request); break;
                case PointerGesture.Swipe: BuildSwipe(request); break;
                case PointerGesture.Drag: BuildDrag(request); break;
                case PointerGesture.Merge: BuildMerge(request); break;
            }
            return UniTask.CompletedTask;
        }

        private float Speed => Mathf.Max(0.1f, _request.Speed);

        private void BuildPoint(PointerRequest r)
        {
            _sequence = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Restart);
            float t = 0.6f / Speed;
            _sequence.Append(_inner.DOAnchorPosY(-18f, t).SetEase(Ease.InOutSine));
            _sequence.Join(_inner.DOScale(1.08f, t).SetEase(Ease.InOutSine));
            _sequence.Append(_inner.DOAnchorPosY(0f, t).SetEase(Ease.InOutSine));
            _sequence.Join(_inner.DOScale(1f, t).SetEase(Ease.InOutSine));
        }

        private void BuildTap(PointerRequest r)
        {
            _sequence = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Restart);
            float d = 0.28f / Speed;
            _sequence.Append(_inner.DOScale(0.82f, d).SetEase(Ease.OutQuad));
            _sequence.Join(_ring.DOFade(0.6f, d).From(0f));
            _sequence.Join(_ring.rectTransform.DOScale(1.4f, d * 2f).From(Vector3.one * 0.6f));
            _sequence.Append(_inner.DOScale(1f, d).SetEase(Ease.OutBack));
            _sequence.Join(_ring.DOFade(0f, d));
            _sequence.AppendInterval(0.35f / Speed);
        }

        private void BuildSwipe(PointerRequest r)
        {
            Vector2 start = ResolvePosition(r.Target, r.ScreenOffset);
            Vector2 end = ResolvePosition(r.SecondaryTarget, r.ScreenOffset);
            _follow.anchoredPosition = start;
            float move = 0.5f / Speed;
            _sequence = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Restart);
            _sequence.AppendCallback(() => { _follow.anchoredPosition = start; _pointerImage.color = tint; });
            _sequence.Append(_follow.DOAnchorPos(end, move).SetEase(Ease.InOutQuad));
            _sequence.Join(_pointerImage.DOFade(0f, move).SetEase(Ease.InQuad).SetDelay(move * 0.6f));
            _sequence.AppendInterval(0.25f / Speed);
        }

        private void BuildDrag(PointerRequest r)
        {
            Vector2 start = ResolvePosition(r.Target, r.ScreenOffset);
            Vector2 end = ResolvePosition(r.SecondaryTarget, r.ScreenOffset);
            _follow.anchoredPosition = start;
            float move = 0.9f / Speed;
            _sequence = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Restart);
            _sequence.AppendCallback(() => { _follow.anchoredPosition = start; _inner.localScale = Vector3.one; });
            _sequence.Append(_inner.DOScale(0.85f, 0.2f)); // grab
            _sequence.Append(_follow.DOAnchorPos(end, move).SetEase(Ease.InOutSine));
            _sequence.Append(_inner.DOScale(1f, 0.2f));     // release
            _sequence.AppendInterval(0.35f / Speed);
        }

        private void BuildMerge(PointerRequest r)
        {
            Vector2 a = ResolvePosition(r.Target, r.ScreenOffset);
            Vector2 b = ResolvePosition(r.SecondaryTarget, r.ScreenOffset);
            Vector2 mid = (a + b) * 0.5f;
            _secondary.gameObject.SetActive(true);
            float move = 0.6f / Speed;
            _sequence = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Restart);
            _sequence.AppendCallback(() =>
            {
                _follow.anchoredPosition = a;
                _secondary.anchoredPosition = b;
                _pointerImage.color = tint;
                _secondaryImage.color = tint;
            });
            _sequence.Append(_follow.DOAnchorPos(mid, move).SetEase(Ease.InOutQuad));
            _sequence.Join(_secondary.DOAnchorPos(mid, move).SetEase(Ease.InOutQuad));
            _sequence.Append(_inner.DOScale(1.25f, 0.15f).SetLoops(2, LoopType.Yoyo));
            _sequence.AppendInterval(0.3f / Speed);
        }

        private Vector2 ResolvePosition(ITutorialTarget target, Vector2 offset)
        {
            Vector2 screen = target != null ? target.GetScreenCenter() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            screen += offset;
            return _overlay != null ? _overlay.ScreenToOverlay(screen) : screen;
        }

        private void LateUpdate()
        {
            if (!IsVisible || !_followTarget) return;
            _follow.anchoredPosition = ResolvePosition(_request.Target, _request.ScreenOffset);
        }

        public async UniTask HideAsync(CancellationToken ct)
        {
            if (!IsVisible) return;
            KillSequence();
            await _pointerImage.DOFade(0f, 0.15f).SetUpdate(true).ToUniTaskSafe(ct);
            HideImmediate();
        }

        public void HideImmediate()
        {
            KillSequence();
            IsVisible = false;
            if (_pointerImage != null) _pointerImage.color = tint;
            gameObject.SetActive(false);
        }

        private void KillSequence()
        {
            if (_sequence != null) { _sequence.Kill(false); _sequence = null; }
            _inner?.DOKill();
            _follow?.DOKill();
            _secondary?.DOKill();
            _ring?.DOKill();
        }

        private void OnDestroy() => KillSequence();
    }
}
