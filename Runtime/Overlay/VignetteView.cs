using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TutorialKit
{
    /// <summary>
    /// Full-screen dimming overlay with one or more soft-edged highlight holes, driven by the
    /// <c>TutorialKit/UIVignette</c> shader. Follows moving targets each frame and lets taps pass
    /// through any hole (via <see cref="ICanvasRaycastFilter"/>) while blocking the rest.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class VignetteView : MonoBehaviour, IVignetteService, ICanvasRaycastFilter
    {
        private const int MaxHoles = 8;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SoftId = Shader.PropertyToID("_Softness");
        private static readonly int ScreenId = Shader.PropertyToID("_ScreenSize");
        private static readonly int CountId = Shader.PropertyToID("_HoleCount");
        private static readonly int CentersId = Shader.PropertyToID("_Centers");
        private static readonly int SizesId = Shader.PropertyToID("_Sizes");

        private TutorialOverlay _overlay;
        private Image _image;
        private Material _material;

        private HighlightRequest _request;
        private ITutorialTarget[] _targets;
        private bool _blockRaycasts = true;
        private bool _blockHoles;
        private bool _snapNext = true;
        private int _prevCount;
        private ITutorialTween _fade;

        private readonly Vector4[] _centers = new Vector4[MaxHoles];
        private readonly Vector4[] _sizes = new Vector4[MaxHoles];
        private readonly Vector2[] _curCenter = new Vector2[MaxHoles];
        private readonly Vector2[] _curSize = new Vector2[MaxHoles];
        private int _holeCount;

        [SerializeField] private float followLerp = 16f;

        public bool IsVisible { get; private set; }

        internal void Init(TutorialOverlay overlay)
        {
            _overlay = overlay;
            _image = GetComponent<Image>();
            // Prefer the shader referenced by the settings asset (in Resources → survives build stripping);
            // fall back to Shader.Find for setups without a settings asset (e.g. editor / play mode).
            var settings = TutorialKitSettings.Instance;
            var shader = settings != null ? settings.VignetteShader : null;
            if (shader == null) shader = Shader.Find("TutorialKit/UIVignette");
            _material = new Material(shader) { name = "TK_Vignette (Instance)" };
            _image.material = _material;
            _image.color = new Color(1, 1, 1, 0f);
            _image.raycastTarget = true;
            _material.SetFloat(CountId, 0f);
            gameObject.SetActive(false);
        }

        public async UniTask ShowAsync(HighlightRequest request, CancellationToken ct)
        {
            bool wasHighlighting = IsVisible && _holeCount > 0;
            _request = request;
            _targets = request.Targets ?? System.Array.Empty<ITutorialTarget>();
            _blockRaycasts = request.BlockRaycasts;
            _blockHoles = request.BlockHoles;
            _material.SetColor(ColorId, request.OverlayColor);
            _material.SetFloat(SoftId, Mathf.Clamp01(request.Softness));
            _snapNext = !wasHighlighting; // glide when moving between highlights, snap on first show

            gameObject.SetActive(true);
            IsVisible = true;
            UpdateHoles();

            TutorialTween.Kill(ref _fade);
            float dur = Mathf.Max(0f, request.FadeDuration);
            if (dur <= 0f) { _image.color = Color.white; return; }
            await Fade(1f, dur, ct);
        }

        public async UniTask HideAsync(CancellationToken ct)
        {
            if (!IsVisible) return;
            TutorialTween.Kill(ref _fade);
            float dur = Mathf.Max(0f, _request.FadeDuration);
            if (dur > 0f) await Fade(0f, dur, ct);
            HideImmediate();
        }

        private UniTask Fade(float to, float dur, CancellationToken ct)
        {
            float from = _image.color.a;
            _fade = TutorialTween.Animate(dur, TutorialEase.Linear, p => SetAlpha(Mathf.LerpUnclamped(from, to, p)));
            return _fade.ToUniTask(ct);
        }

        private void SetAlpha(float a) { var c = _image.color; c.a = a; _image.color = c; }

        public void HideImmediate()
        {
            TutorialTween.Kill(ref _fade);
            IsVisible = false;
            _holeCount = 0;
            _prevCount = 0;
            if (_material != null) _material.SetFloat(CountId, 0f);
            if (_image != null) _image.color = new Color(1, 1, 1, 0f);
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (IsVisible) UpdateHoles();
        }

        private void UpdateHoles()
        {
            _material.SetVector(ScreenId, new Vector4(Screen.width, Screen.height, 0, 0));
            if (_targets == null || _targets.Length == 0)
            {
                _holeCount = 0;
                _material.SetFloat(CountId, 0f);
                return;
            }

            float shape = _request.Shape == HighlightShape.Circle ? 0f : 1f;
            float pad = _request.Padding;
            float corner = _request.CornerRadius;
            float t = 1f - Mathf.Exp(-followLerp * Mathf.Max(Time.unscaledDeltaTime, 0.0001f));

            int count = 0;
            for (int i = 0; i < _targets.Length && count < MaxHoles; i++)
            {
                var target = _targets[i];
                if (target == null || !target.TryGetScreenRect(out var rect)) continue;

                Vector2 desiredCenter = rect.center;
                Vector2 desiredSize = new Vector2(rect.width * 0.5f + pad, rect.height * 0.5f + pad);

                // Snap on first show, or when this hole index is newly appearing (avoids gliding a
                // fresh hole out of a stale position).
                if (_snapNext || count >= _prevCount)
                {
                    _curCenter[count] = desiredCenter;
                    _curSize[count] = desiredSize;
                }
                else
                {
                    _curCenter[count] = Vector2.Lerp(_curCenter[count], desiredCenter, t);
                    _curSize[count] = Vector2.Lerp(_curSize[count], desiredSize, t);
                }

                _centers[count] = new Vector4(_curCenter[count].x, _curCenter[count].y, 0, 0);
                _sizes[count] = new Vector4(_curSize[count].x, _curSize[count].y, shape, corner);
                count++;
            }

            _snapNext = false;
            _prevCount = count;
            _holeCount = count;
            _material.SetVectorArray(CentersId, _centers);
            _material.SetVectorArray(SizesId, _sizes);
            _material.SetFloat(CountId, count);
        }

        // Ray passes through any hole (returns false) and is blocked elsewhere (returns true).
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!IsVisible || !_blockRaycasts) return false;
            if (_holeCount == 0) return true;

            bool circle = _request.Shape == HighlightShape.Circle;
            for (int i = 0; i < _holeCount; i++)
            {
                Vector2 p = screenPoint - _curCenter[i];
                bool inside;
                if (circle)
                {
                    float rr = Mathf.Max(_curSize[i].x, _curSize[i].y);
                    inside = p.sqrMagnitude <= rr * rr;
                }
                else
                {
                    inside = Mathf.Abs(p.x) <= _curSize[i].x && Mathf.Abs(p.y) <= _curSize[i].y;
                }
                // Inside a hole: pass through (return false) unless holes are locked too.
                if (inside) return _blockHoles;
            }
            return true;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
