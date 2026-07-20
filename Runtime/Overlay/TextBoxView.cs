using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TutorialKit
{
    /// <summary>
    /// Shows styled text boxes. Ships a built-in default box and lets games register custom prefabs
    /// (any prefab whose root has a <see cref="TutorialTextBox"/>) under a key.
    /// </summary>
    public sealed class TextBoxView : MonoBehaviour, ITextBoxService
    {
        [SerializeField] private Color panelColor = new Color(0.10f, 0.12f, 0.16f, 0.96f);
        [SerializeField] private Color accentColor = new Color(0.23f, 0.48f, 0.84f, 1f);
        [SerializeField] private Color textColor = Color.white;

        [Tooltip("Show / hide animation used by the built-in default box. Custom prefabs set their own on " +
                 "their TutorialTextBox component. (Legacy needs an Animation component, so the procedural " +
                 "default box treats Legacy as Script.)")]
        [SerializeField] private TextBoxAnimation defaultAnimation = TextBoxAnimation.Script;

        private TutorialOverlay _overlay;
        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, TutorialTextBox> _active = new Dictionary<string, TutorialTextBox>();
        private GameObject _defaultPrefabInstanceTemplate;

        internal void Init(TutorialOverlay overlay) => _overlay = overlay;

        public void RegisterPrefab(string key, GameObject prefab)
        {
            if (string.IsNullOrEmpty(key) || prefab == null) return;
            _prefabs[key] = prefab;
        }

        public async UniTask ShowAsync(TextBoxRequest request, CancellationToken ct)
        {
            string id = string.IsNullOrEmpty(request.Id) ? "main" : request.Id;
            var box = GetOrCreate(id, request.PrefabKey);
            box.gameObject.SetActive(true);
            box.Bind(request.Title, request.Body, request.ShowContinueButton, request.ContinueLabel);
            Position(box, request);

            // The box owns its show animation (script / legacy / none). Don't await it, so a longer
            // animation overlaps the typewriter — matching the built-in slide-up + fade-in feel.
            box.PlayShow(ct).Forget();

            if (request.Typewriter && box.BodyLabel != null)
                await RunTypewriter(box, request.Body, request.TypewriterCps, ct);

            if (request.WaitForDismiss && request.ShowContinueButton)
            {
                var tcs = new UniTaskCompletionSource();
                void OnContinue() => tcs.TrySetResult();
                box.Continued += OnContinue;
                using (ct.Register(() => tcs.TrySetCanceled(ct)))
                {
                    try { await tcs.Task; }
                    finally { box.Continued -= OnContinue; }
                }
                await HideAsync(id, ct);
            }
        }

        private static async UniTask RunTypewriter(TutorialTextBox box, string body, float cps, CancellationToken ct)
        {
            int total = string.IsNullOrEmpty(body) ? 0 : body.Length;
            box.SetVisibleCharacters(0);
            float perChar = cps > 0f ? 1f / cps : 0f;
            for (int i = 0; i <= total; i++)
            {
                box.SetVisibleCharacters(i);
                if (perChar > 0f)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(perChar), true, cancellationToken: ct);
            }
            box.SetVisibleCharacters(total);
        }

        public async UniTask HideAsync(string id, CancellationToken ct)
        {
            id = string.IsNullOrEmpty(id) ? "main" : id;
            if (!_active.TryGetValue(id, out var box) || box == null) return;
            await box.PlayHide(ct);
            if (box != null) box.gameObject.SetActive(false);
        }

        public void HideAllImmediate()
        {
            foreach (var kv in _active)
                if (kv.Value != null)
                {
                    kv.Value.KillTweens();
                    kv.Value.gameObject.SetActive(false);
                }
        }

        private TutorialTextBox GetOrCreate(string id, string prefabKey)
        {
            if (_active.TryGetValue(id, out var existing) && existing != null)
                return existing;

            TutorialTextBox box;
            if (!string.IsNullOrEmpty(prefabKey) && _prefabs.TryGetValue(prefabKey, out var prefab))
            {
                var go = Instantiate(prefab, transform);
                box = go.GetComponent<TutorialTextBox>();
                if (box == null) box = go.AddComponent<TutorialTextBox>();
            }
            else
            {
                box = BuildDefault(id);
            }
            _active[id] = box;
            return box;
        }

        private void Position(TutorialTextBox box, TextBoxRequest request)
        {
            var panel = box.Panel;
            Vector2 screen;
            switch (request.Placement)
            {
                case TextBoxPlacement.Custom:
                    screen = new Vector2(request.NormalizedPosition.x * Screen.width,
                                         request.NormalizedPosition.y * Screen.height);
                    break;
                case TextBoxPlacement.Top: screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.84f); break;
                case TextBoxPlacement.Center: screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f); break;
                case TextBoxPlacement.Bottom: screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.18f); break;
                case TextBoxPlacement.AboveTarget:
                case TextBoxPlacement.BelowTarget:
                {
                    var target = request.Target;
                    if (target != null && target.TryGetScreenRect(out var rect))
                    {
                        float y = request.Placement == TextBoxPlacement.AboveTarget
                            ? rect.yMax + panel.sizeDelta.y * 0.5f + 24f
                            : rect.yMin - panel.sizeDelta.y * 0.5f - 24f;
                        screen = new Vector2(rect.center.x, y);
                    }
                    else screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    break;
                }
                default: screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.2f); break;
            }
            panel.anchoredPosition = _overlay != null ? _overlay.ScreenToOverlay(screen) : screen;
        }

        private TutorialTextBox BuildDefault(string id)
        {
            var root = new GameObject($"TextBox_{id}", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 200f);

            var panelImg = root.AddComponent<Image>();
            panelImg.sprite = TutorialSpriteFactory.RoundedPanel;
            panelImg.type = Image.Type.Sliced;
            panelImg.color = panelColor;
            panelImg.raycastTarget = true;

            var title = CreateText("Title", rt, 30f, FontStyles.Bold, textColor,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -18), new Vector2(-24, -18), TextAlignmentOptions.TopLeft, 40f);

            var body = CreateText("Body", rt, 24f, FontStyles.Normal, new Color(textColor.r, textColor.g, textColor.b, 0.92f),
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(24, 64), new Vector2(-24, -64), TextAlignmentOptions.TopLeft, 0f);

            // Continue button
            var btnGo = new GameObject("Continue", typeof(RectTransform), typeof(Image), typeof(Button));
            var btnRt = (RectTransform)btnGo.transform;
            btnRt.SetParent(rt, false);
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(1, 0);
            btnRt.pivot = new Vector2(1, 0);
            btnRt.anchoredPosition = new Vector2(-20, 18);
            btnRt.sizeDelta = new Vector2(150, 52);
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.sprite = TutorialSpriteFactory.RoundedPanel;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = accentColor;
            var btn = btnGo.GetComponent<Button>();

            var btnLabel = CreateText("Label", btnRt, 22f, FontStyles.Bold, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, 0f);
            btnLabel.text = "Next";

            var box = root.AddComponent<TutorialTextBox>();
            box.Wire(root.GetComponent<CanvasGroup>(), rt, title, body, btn, btnLabel);
            box.AnimationMode = defaultAnimation;
            return box;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, float size, FontStyles style,
            Color color, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, TextAlignmentOptions align, float fixedHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }
    }
}
