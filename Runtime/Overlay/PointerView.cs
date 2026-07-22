using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TutorialKit
{
    /// <summary>
    /// Shows an animated hand/arrow pointer performing a gesture (point, tap, swipe, drag, merge).
    /// The pointer root follows a moving target for point/tap; path gestures animate between two
    /// captured endpoints. Animations go through <see cref="TutorialTween"/> so the backend is swappable.
    /// Real art can be assigned via <see cref="handSprite"/>/<see cref="arrowSprite"/>.
    /// </summary>
    public sealed class PointerView : MonoBehaviour, IPointerService
    {
        [SerializeField] private Sprite handSprite;
        [SerializeField] private Sprite arrowSprite;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private float pointerSize = 110f;

        [Header("Hotspot offset (fraction of size) so the finger/arrow tip — not the sprite centre —")]
        [Tooltip("sits at the target, leaving the target (e.g. a button's label) visible.")]
        [SerializeField] private Vector2 handHotspot = new Vector2(0.40f, -0.42f);
        [SerializeField] private Vector2 arrowHotspot = new Vector2(0f, -0.45f);

        private TutorialOverlay _overlay;
        private RectTransform _follow;   // moved to the target
        private RectTransform _inner;    // animated relative to _follow
        private Image _pointerImage;
        private Image _ring;

        private ITutorialTweenSequence _sequence;
        private ITutorialTween _hideFade;
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
            KillTweens();
            _request = request;
            _pointerImage.sprite = request.Kind == PointerKind.Arrow
                ? (arrowSprite != null ? arrowSprite : TutorialSpriteFactory.Arrow)
                : (handSprite != null ? handSprite : TutorialSpriteFactory.Hand);

            // Offset the sprite so its tip (not its centre) is the hotspot at the target; the ring stays
            // centred so the tap ripples from the fingertip while the palm/arrow body clears the target.
            Vector2 hot = request.Kind == PointerKind.Arrow ? arrowHotspot : handHotspot;
            _pointerImage.rectTransform.anchoredPosition = hot * pointerSize;

            gameObject.SetActive(true);
            IsVisible = true;
            _inner.anchoredPosition = Vector2.zero;
            _inner.localScale = Vector3.one;
            _ring.rectTransform.localScale = Vector3.one;
            _pointerImage.color = tint;
            SetRingAlpha(0f);

            Vector2 start = ResolvePosition(request.Target, request.ScreenOffset);
            _follow.anchoredPosition = start;

            _followTarget = request.Gesture == PointerGesture.Point
                || request.Gesture == PointerGesture.Tap
                || request.Gesture == PointerGesture.DoubleTap;

            switch (request.Gesture)
            {
                case PointerGesture.Point: BuildPoint(request); break;
                case PointerGesture.Tap: BuildTap(request); break;
                case PointerGesture.DoubleTap: BuildDoubleTap(request); break;
                case PointerGesture.Swipe: BuildSwipe(request); break;
                case PointerGesture.Drag: BuildDrag(request); break;
                case PointerGesture.Merge: BuildMerge(request); break;
            }
            return UniTask.CompletedTask;
        }

        private float Speed => Mathf.Max(0.1f, _request.Speed);

        private void BuildPoint(PointerRequest r)
        {
            float t = 0.6f / Speed;
            _sequence = TutorialTween.Sequence()
                .Append(t, TutorialEase.InOutSine, p => SetInner(Mathf.LerpUnclamped(0f, -18f, p), Mathf.LerpUnclamped(1f, 1.08f, p)))
                .Append(t, TutorialEase.InOutSine, p => SetInner(Mathf.LerpUnclamped(-18f, 0f, p), Mathf.LerpUnclamped(1.08f, 1f, p)))
                .SetLoops(-1)
                .Play();
        }

        private void BuildTap(PointerRequest r)
        {
            float d = 0.28f / Speed;
            _sequence = TutorialTween.Sequence()
                .Append(d, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(1f, 0.82f, p)))
                .Join(d, TutorialEase.Linear, p => SetRingAlpha(Mathf.LerpUnclamped(0f, 0.6f, p)))
                .Join(d * 2f, TutorialEase.Linear, p => SetRingScale(Mathf.LerpUnclamped(0.6f, 1.4f, p)))
                .Append(d, TutorialEase.OutBack, p => SetInnerScale(Mathf.LerpUnclamped(0.82f, 1f, p)))
                .Join(d, TutorialEase.Linear, p => SetRingAlpha(Mathf.LerpUnclamped(0.6f, 0f, p)))
                .AppendInterval(0.35f / Speed)
                .SetLoops(-1)
                .Play();
        }

        private void BuildDoubleTap(PointerRequest r)
        {
            // Two quick presses (each: dip + expanding ring) back-to-back, then a longer rest so the
            // "double" reads clearly. One press = the Tap dip/ring, but tighter and with no rest between.
            float d = 0.16f / Speed;
            var seq = TutorialTween.Sequence();
            for (int i = 0; i < 2; i++)
            {
                seq
                    .Append(d, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(1f, 0.8f, p)))
                    .Join(d, TutorialEase.Linear, p => SetRingAlpha(Mathf.LerpUnclamped(0f, 0.55f, p)))
                    .Join(d * 2f, TutorialEase.Linear, p => SetRingScale(Mathf.LerpUnclamped(0.55f, 1.35f, p)))
                    .Append(d, TutorialEase.OutBack, p => SetInnerScale(Mathf.LerpUnclamped(0.8f, 1f, p)))
                    .Join(d, TutorialEase.Linear, p => SetRingAlpha(Mathf.LerpUnclamped(0.55f, 0f, p)))
                    .AppendCallback(() => SetRingScale(0.55f))
                    .AppendInterval(0.06f / Speed); // tiny gap between the two taps
            }
            _sequence = seq
                .AppendInterval(0.55f / Speed) // longer rest before the pair repeats
                .SetLoops(-1)
                .Play();
        }

        private void BuildSwipe(PointerRequest r)
        {
            Vector2 start = ResolvePosition(r.Target, r.ScreenOffset);
            Vector2 end = ResolvePosition(r.SecondaryTarget, r.ScreenOffset);
            _follow.anchoredPosition = start;
            float move = 0.5f / Speed;
            _sequence = TutorialTween.Sequence()
                .AppendCallback(() => { _follow.anchoredPosition = start; _pointerImage.color = tint; })
                .Append(move, TutorialEase.InOutQuad, p => _follow.anchoredPosition = Vector2.LerpUnclamped(start, end, p))
                .Join(move, TutorialEase.InQuad, p => SetPointerAlpha(Mathf.LerpUnclamped(1f, 0f, p)), delay: move * 0.6f)
                .AppendInterval(0.25f / Speed)
                .SetLoops(-1)
                .Play();
        }

        private void BuildDrag(PointerRequest r)
        {
            Vector2 start = ResolvePosition(r.Target, r.ScreenOffset);
            Vector2 end = ResolvePosition(r.SecondaryTarget, r.ScreenOffset);
            _follow.anchoredPosition = start;
            // Only the bundled hand art has distinct open/closed poses; skip swaps for arrow/custom art.
            Sprite open = handSprite != null ? handSprite : TutorialSpriteFactory.HandOpen;
            Sprite closed = handSprite != null ? handSprite : TutorialSpriteFactory.HandClosed;
            float move = 0.9f / Speed;
            _sequence = TutorialTween.Sequence()
                .AppendCallback(() => { _follow.anchoredPosition = start; SetInnerScale(1f); _pointerImage.sprite = open; })
                .Append(0.2f, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(1f, 0.85f, p)))   // grab
                .AppendCallback(() => _pointerImage.sprite = closed)                                          // fist closes
                .Append(move, TutorialEase.InOutSine, p => _follow.anchoredPosition = Vector2.LerpUnclamped(start, end, p))
                .AppendCallback(() => _pointerImage.sprite = open)                                            // release, hand opens
                .Append(0.2f, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(0.85f, 1f, p)))    // release
                .AppendInterval(0.35f / Speed)
                .SetLoops(-1)
                .Play();
        }

        private void BuildMerge(PointerRequest r)
        {
            Vector2 a = ResolvePosition(r.Target, r.ScreenOffset);
            Vector2 b = ResolvePosition(r.SecondaryTarget, r.ScreenOffset);
            Vector2 mid = (a + b) * 0.5f;
            float move = 0.6f / Speed;
            // Just the hand: it sweeps from the first target to the midpoint, then pulses (no circle indicator).
            _sequence = TutorialTween.Sequence()
                .AppendCallback(() =>
                {
                    _follow.anchoredPosition = a;
                    _pointerImage.color = tint;
                })
                .Append(move, TutorialEase.InOutQuad, p => _follow.anchoredPosition = Vector2.LerpUnclamped(a, mid, p))
                .Append(0.15f, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(1f, 1.25f, p)))   // pulse up
                .Append(0.15f, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(1.25f, 1f, p)))   // pulse down
                .AppendInterval(0.3f / Speed)
                .SetLoops(-1)
                .Play();
        }

        // --- value setters the animations drive ---
        private void SetInner(float y, float scale) { _inner.anchoredPosition = new Vector2(0f, y); _inner.localScale = Vector3.one * scale; }
        private void SetInnerScale(float s) => _inner.localScale = Vector3.one * s;
        private void SetRingAlpha(float a) => _ring.color = new Color(tint.r, tint.g, tint.b, a);
        private void SetRingScale(float s) => _ring.rectTransform.localScale = Vector3.one * s;
        private void SetPointerAlpha(float a) => _pointerImage.color = new Color(tint.r, tint.g, tint.b, a);

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
            KillTweens();
            float from = _pointerImage.color.a;
            _hideFade = TutorialTween.Animate(0.15f, TutorialEase.Linear, p => SetPointerAlpha(Mathf.LerpUnclamped(from, 0f, p)));
            await _hideFade.ToUniTask(ct);
            HideImmediate();
        }

        public void HideImmediate()
        {
            KillTweens();
            IsVisible = false;
            if (_pointerImage != null) _pointerImage.color = tint;
            gameObject.SetActive(false);
        }

        private void KillTweens()
        {
            if (_sequence != null) { _sequence.Kill(); _sequence = null; }
            TutorialTween.Kill(ref _hideFade);
        }

        private void OnDestroy() => KillTweens();
    }
}
