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

        private TutorialOverlay _overlay;
        private RectTransform _follow;   // moved to the target
        private RectTransform _inner;    // animated relative to _follow
        private Image _pointerImage;
        private Image _ring;

        private ITutorialTweenSequence _sequence;
        private ITutorialTween _hideFade;
        private PointerRequest _request;
        private bool _followTarget;

        // Project-wide appearance & timings, resolved from settings on each Show so editor tweaks
        // apply live. Values below mirror the resolved defaults for use between Init and the first Show.
        private TutorialPointerDefaults _defaults = new TutorialPointerDefaults();
        private Color tint = Color.white;
        private float pointerSize = 110f;
        private float artScale = 1f;   // project-wide multiplier for the sprite only (custom art compensation)

        public bool IsVisible { get; private set; }

        internal void Init(TutorialOverlay overlay)
        {
            _overlay = overlay;
            ResolveDefaults();

            _follow = CreateChild("Follow", transform as RectTransform);
            _inner = CreateChild("Inner", _follow);

            _ring = CreateImage("Ring", _inner, TutorialSpriteFactory.Ring, pointerSize * 1.4f);
            _ring.color = new Color(tint.r, tint.g, tint.b, 0f);

            Sprite hand = handSprite != null ? handSprite : TutorialSpriteFactory.Hand;
            _pointerImage = CreateImage("Pointer", _inner, hand, pointerSize);
            _pointerImage.rectTransform.sizeDelta = SpriteSize(hand);
            _pointerImage.color = tint;

            gameObject.SetActive(false);
        }

        // Pull the current project defaults and re-apply the sprite/ring sizes so changes made in the
        // settings window (even during play) take effect the next time a pointer is shown.
        private void ResolveDefaults()
        {
            _defaults = TutorialKitSettings.ResolvePointerDefaults();
            tint = _defaults.Tint;
            pointerSize = _defaults.Size;
            artScale = TutorialKitSettings.ResolvePointerArtScale();
            if (_pointerImage != null) _pointerImage.rectTransform.sizeDelta = SpriteSize(_pointerImage.sprite);
            if (_ring != null) _ring.rectTransform.sizeDelta = new Vector2(pointerSize * 1.4f, pointerSize * 1.4f);
        }

        // Rect size for a pointer sprite: fit its longest side to pointerSize * artScale and keep its aspect,
        // so non-square custom art isn't squashed into a square box. artScale is the project-wide sprite
        // multiplier — custom art with transparent padding reads much smaller than the bundled hand at the
        // same box size, and this is the knob that brings it back up.
        private Vector2 SpriteSize(Sprite sprite)
        {
            float box = pointerSize * artScale;
            if (sprite == null) return new Vector2(box, box);
            Vector2 px = sprite.rect.size;
            float longest = Mathf.Max(px.x, px.y);
            if (longest <= 0f) return new Vector2(box, box);
            return px * (box / longest);
        }

        // Assign the sprite and re-fit the rect + tip hotspot to it. Used on Show and on the drag's
        // open/closed hand swaps, where the two sprites can have different aspects.
        private void SetPointerSprite(Sprite sprite)
        {
            _pointerImage.sprite = sprite;
            Vector2 size = SpriteSize(sprite);
            _pointerImage.rectTransform.sizeDelta = size;
            // Offset the sprite so its tip (not its centre) is the hotspot at the target; the ring stays
            // centred so the tap ripples from the fingertip while the palm/arrow body clears the target.
            Vector2 hot = _request.Kind == PointerKind.Arrow ? _defaults.ArrowHotspot : _defaults.HandHotspot;
            _pointerImage.rectTransform.anchoredPosition = new Vector2(hot.x * size.x, hot.y * size.y);
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
            ResolveDefaults();
            _request = request;
            SetPointerSprite(request.Kind == PointerKind.Arrow
                ? (arrowSprite != null ? arrowSprite : TutorialSpriteFactory.Arrow)
                : (handSprite != null ? handSprite : TutorialSpriteFactory.Hand));

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
            var cfg = _defaults.Point;
            float t = cfg.BobDuration / Speed;
            float dip = -Mathf.Abs(cfg.BobDistance);
            _sequence = TutorialTween.Sequence()
                .Append(t, TutorialEase.InOutSine, p => SetInner(Mathf.LerpUnclamped(0f, dip, p), Mathf.LerpUnclamped(1f, cfg.BobScale, p)))
                .Append(t, TutorialEase.InOutSine, p => SetInner(Mathf.LerpUnclamped(dip, 0f, p), Mathf.LerpUnclamped(cfg.BobScale, 1f, p)))
                .SetLoops(-1)
                .Play();
        }

        private void BuildTap(PointerRequest r)
        {
            var cfg = _defaults.Tap;
            float d = cfg.PressDuration / Speed;
            _sequence = TutorialTween.Sequence()
                .Append(d, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(1f, cfg.DipScale, p)))
                .Join(d, TutorialEase.Linear, p => SetRingAlpha(Mathf.LerpUnclamped(0f, cfg.RingAlpha, p)))
                .Join(d * 2f, TutorialEase.Linear, p => SetRingScale(Mathf.LerpUnclamped(cfg.RingFromScale, cfg.RingToScale, p)))
                .Append(d, TutorialEase.OutBack, p => SetInnerScale(Mathf.LerpUnclamped(cfg.DipScale, 1f, p)))
                .Join(d, TutorialEase.Linear, p => SetRingAlpha(Mathf.LerpUnclamped(cfg.RingAlpha, 0f, p)))
                .AppendInterval(cfg.RestDuration / Speed)
                .SetLoops(-1)
                .Play();
        }

        private void BuildDoubleTap(PointerRequest r)
        {
            // Two quick presses (each: dip + expanding ring) back-to-back, then a longer rest so the
            // "double" reads clearly. One press = the Tap dip/ring, but tighter and with no rest between.
            var cfg = _defaults.DoubleTap;
            float d = cfg.PressDuration / Speed;
            var seq = TutorialTween.Sequence();
            for (int i = 0; i < 2; i++)
            {
                seq
                    .Append(d, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(1f, cfg.DipScale, p)))
                    .Join(d, TutorialEase.Linear, p => SetRingAlpha(Mathf.LerpUnclamped(0f, cfg.RingAlpha, p)))
                    .Join(d * 2f, TutorialEase.Linear, p => SetRingScale(Mathf.LerpUnclamped(cfg.RingFromScale, cfg.RingToScale, p)))
                    .Append(d, TutorialEase.OutBack, p => SetInnerScale(Mathf.LerpUnclamped(cfg.DipScale, 1f, p)))
                    .Join(d, TutorialEase.Linear, p => SetRingAlpha(Mathf.LerpUnclamped(cfg.RingAlpha, 0f, p)))
                    .AppendCallback(() => SetRingScale(cfg.RingFromScale))
                    .AppendInterval(cfg.GapDuration / Speed); // tiny gap between the two taps
            }
            _sequence = seq
                .AppendInterval(cfg.RestDuration / Speed) // longer rest before the pair repeats
                .SetLoops(-1)
                .Play();
        }

        private void BuildSwipe(PointerRequest r)
        {
            var cfg = _defaults.Swipe;
            Vector2 start = ResolvePosition(r.Target, r.ScreenOffset);
            Vector2 end = ResolvePosition(r.SecondaryTarget, r.ScreenOffset);
            _follow.anchoredPosition = start;
            float move = cfg.MoveDuration / Speed;
            _sequence = TutorialTween.Sequence()
                .AppendCallback(() => { _follow.anchoredPosition = start; _pointerImage.color = tint; })
                .Append(move, TutorialEase.InOutQuad, p => _follow.anchoredPosition = Vector2.LerpUnclamped(start, end, p))
                .Join(move, TutorialEase.InQuad, p => SetPointerAlpha(Mathf.LerpUnclamped(1f, 0f, p)), delay: move * 0.6f)
                .AppendInterval(cfg.RestDuration / Speed)
                .SetLoops(-1)
                .Play();
        }

        private void BuildDrag(PointerRequest r)
        {
            var cfg = _defaults.Drag;
            Vector2 start = ResolvePosition(r.Target, r.ScreenOffset);
            Vector2 end = ResolvePosition(r.SecondaryTarget, r.ScreenOffset);
            _follow.anchoredPosition = start;
            // Only the bundled hand art has distinct open/closed poses; skip swaps for arrow/custom art.
            Sprite open = handSprite != null ? handSprite : TutorialSpriteFactory.HandOpen;
            Sprite closed = handSprite != null ? handSprite : TutorialSpriteFactory.HandClosed;
            float move = cfg.MoveDuration / Speed;
            _sequence = TutorialTween.Sequence()
                .AppendCallback(() => { _follow.anchoredPosition = start; SetInnerScale(1f); SetPointerSprite(open); })
                .Append(cfg.GrabDuration / Speed, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(1f, cfg.GrabScale, p)))   // grab
                .AppendCallback(() => SetPointerSprite(closed))                                               // fist closes
                .Append(move, TutorialEase.InOutSine, p => _follow.anchoredPosition = Vector2.LerpUnclamped(start, end, p))
                .AppendCallback(() => SetPointerSprite(open))                                                 // release, hand opens
                .Append(cfg.ReleaseDuration / Speed, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(cfg.GrabScale, 1f, p)))    // release
                .AppendInterval(cfg.RestDuration / Speed)
                .SetLoops(-1)
                .Play();
        }

        private void BuildMerge(PointerRequest r)
        {
            var cfg = _defaults.Merge;
            Vector2 a = ResolvePosition(r.Target, r.ScreenOffset);
            Vector2 b = ResolvePosition(r.SecondaryTarget, r.ScreenOffset);
            Vector2 mid = (a + b) * 0.5f;
            float move = cfg.MoveDuration / Speed;
            float pulse = cfg.PulseDuration / Speed;
            // Just the hand: it sweeps from the first target to the midpoint, then pulses (no circle indicator).
            _sequence = TutorialTween.Sequence()
                .AppendCallback(() =>
                {
                    _follow.anchoredPosition = a;
                    _pointerImage.color = tint;
                })
                .Append(move, TutorialEase.InOutQuad, p => _follow.anchoredPosition = Vector2.LerpUnclamped(a, mid, p))
                .Append(pulse, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(1f, cfg.PulseScale, p)))   // pulse up
                .Append(pulse, TutorialEase.OutQuad, p => SetInnerScale(Mathf.LerpUnclamped(cfg.PulseScale, 1f, p)))   // pulse down
                .AppendInterval(cfg.RestDuration / Speed)
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
            float fade = Mathf.Max(0f, _defaults.HideFadeDuration);
            _hideFade = TutorialTween.Animate(fade, TutorialEase.Linear, p => SetPointerAlpha(Mathf.LerpUnclamped(from, 0f, p)));
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
