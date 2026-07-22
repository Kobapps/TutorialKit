using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TutorialKit
{
    /// <summary>
    /// Driver for a text-box instance (the built-in default one, or a custom prefab).
    /// A custom text-box prefab only needs this component with its fields wired; the overlay
    /// binds content and handles show/hide/continue through it.
    /// </summary>
    [AddComponentMenu("TutorialKit/Tutorial Text Box")]
    public class TutorialTextBox : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;

        [Header("Show / hide animation")]
        [Tooltip("How this box animates in and out.\n" +
                 "• Script — code/tween-driven (built-in slide + fade, or override PlayShowScript / PlayHideScript).\n" +
                 "• Legacy — play clips on a Unity Animation component (see fields below).\n" +
                 "• None — appear / disappear instantly.")]
        [SerializeField] private TextBoxAnimation animationMode = TextBoxAnimation.Script;

        [Tooltip("Legacy Animation component to drive (Legacy mode). Empty = one on this GameObject. " +
                 "Note: legacy clips use scaled time, so they won't visibly play while the game is paused.")]
        [SerializeField] private Animation legacyAnimation;
        [Tooltip("Clip name played on show (Legacy mode).")]
        [SerializeField] private string showClip = "Show";
        [Tooltip("Clip name played on hide (Legacy mode).")]
        [SerializeField] private string hideClip = "Hide";

        [Header("Script animation tuning (Script mode)")]
        [Tooltip("Slide-up distance in pixels for the built-in show animation.")]
        [SerializeField] private float slideDistance = 28f;
        [Tooltip("Show (slide + fade in) duration in seconds.")]
        [SerializeField] private float showDuration = 0.24f;
        [Tooltip("Hide (fade out) duration in seconds.")]
        [SerializeField] private float hideDuration = 0.15f;

        /// <summary>Show / hide animation style used by this box.</summary>
        public TextBoxAnimation AnimationMode { get => animationMode; set => animationMode = value; }

        /// <summary>Raised when the continue button is pressed.</summary>
        public event Action Continued;

        public CanvasGroup CanvasGroup => canvasGroup != null ? canvasGroup : canvasGroup = GetComponent<CanvasGroup>();
        public RectTransform Panel => panel != null ? panel : panel = transform as RectTransform;
        public TMP_Text BodyLabel => bodyLabel;

        // Show/hide animation handles owned by this box (killed before a re-show or on hide).
        internal ITutorialTween SlideTween;
        internal ITutorialTween FadeTween;

        internal void KillTweens()
        {
            TutorialTween.Kill(ref SlideTween);
            TutorialTween.Kill(ref FadeTween);
        }

        /// <summary>
        /// Plays the show animation. Called by the overlay after the box is positioned and bound; the
        /// overlay does not await it, so a longer animation overlaps the typewriter. Override this (or
        /// <see cref="PlayShowScript"/>) on a custom box for a fully bespoke script animation.
        /// </summary>
        public virtual UniTask PlayShow(CancellationToken ct)
        {
            KillTweens();
            switch (animationMode)
            {
                case TextBoxAnimation.None:
                    if (CanvasGroup != null) CanvasGroup.alpha = 1f;
                    return UniTask.CompletedTask;
                case TextBoxAnimation.Legacy:
                    return PlayLegacy(showClip, entering: true, ct);
                default:
                    return PlayShowScript(ct);
            }
        }

        /// <summary>Plays the hide animation. The overlay awaits this, then deactivates the box.</summary>
        public virtual UniTask PlayHide(CancellationToken ct)
        {
            KillTweens();
            switch (animationMode)
            {
                case TextBoxAnimation.None:
                    return UniTask.CompletedTask;
                case TextBoxAnimation.Legacy:
                    return PlayLegacy(hideClip, entering: false, ct);
                default:
                    return PlayHideScript(ct);
            }
        }

        /// <summary>Built-in show: slide up + fade in. Not awaited by the overlay (overlaps typewriter).</summary>
        protected virtual UniTask PlayShowScript(CancellationToken ct)
        {
            var panel = Panel;
            Vector2 target = panel.anchoredPosition; // resting position set by the overlay before Show
            Vector2 from = target + new Vector2(0f, -slideDistance);
            panel.anchoredPosition = from;
            SlideTween = TutorialTween.Animate(showDuration, TutorialEase.OutCubic,
                p => panel.anchoredPosition = Vector2.LerpUnclamped(from, target, p));

            var group = CanvasGroup;
            if (group != null)
            {
                group.alpha = 0f;
                FadeTween = TutorialTween.Animate(showDuration, TutorialEase.Linear, p => group.alpha = p);
            }
            return UniTask.CompletedTask;
        }

        /// <summary>Built-in hide: fade out. Awaited by the overlay before the box is deactivated.</summary>
        protected virtual UniTask PlayHideScript(CancellationToken ct)
        {
            var group = CanvasGroup;
            if (group == null) return UniTask.CompletedTask;
            float start = group.alpha;
            FadeTween = TutorialTween.Animate(hideDuration, TutorialEase.Linear,
                p => group.alpha = Mathf.LerpUnclamped(start, 0f, p));
            return FadeTween.ToUniTask(ct);
        }

        // Plays a named clip on the legacy Animation component, awaiting its length. Falls back to the
        // script animation when no component/clip is available (e.g. the built-in procedural box).
        private UniTask PlayLegacy(string clip, bool entering, CancellationToken ct)
        {
            var anim = legacyAnimation != null ? legacyAnimation : GetComponent<Animation>();
            AnimationClip ac = (anim != null && !string.IsNullOrEmpty(clip)) ? anim.GetClip(clip) : null;
            if (anim == null || ac == null)
                return entering ? PlayShowScript(ct) : PlayHideScript(ct);

            if (entering && CanvasGroup != null) CanvasGroup.alpha = 1f; // the clip drives the visuals
            anim.Play(clip);
            return UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, ac.length)), true, cancellationToken: ct);
        }

        protected virtual void Awake()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(() => Continued?.Invoke());
        }

        public void Wire(CanvasGroup group, RectTransform panelRect, TMP_Text title, TMP_Text body,
            Button button, TMP_Text buttonLabel)
        {
            canvasGroup = group;
            panel = panelRect;
            titleLabel = title;
            bodyLabel = body;
            continueButton = button;
            continueLabel = buttonLabel;
            if (continueButton != null)
                continueButton.onClick.AddListener(() => Continued?.Invoke());
        }

        public virtual void Bind(string title, string body, bool showContinue, string continueText)
        {
            if (titleLabel != null)
            {
                bool hasTitle = !string.IsNullOrEmpty(title);
                titleLabel.gameObject.SetActive(hasTitle);
                titleLabel.text = title;
            }
            if (bodyLabel != null) bodyLabel.text = body;
            if (continueButton != null) continueButton.gameObject.SetActive(showContinue);
            if (continueLabel != null && !string.IsNullOrEmpty(continueText)) continueLabel.text = continueText;
        }

        /// <summary>Sets the visible portion of the body for a typewriter effect.</summary>
        public virtual void SetVisibleCharacters(int count)
        {
            if (bodyLabel != null) bodyLabel.maxVisibleCharacters = count;
        }

        /// <summary>
        /// Sets the horizontal alignment of the body text. <see cref="TutorialTextAlignment.Default"/>
        /// leaves the label's authored alignment untouched (the overlay resolves Default beforehand).
        /// </summary>
        public virtual void SetBodyAlignment(TutorialTextAlignment alignment)
        {
            if (bodyLabel == null || alignment == TutorialTextAlignment.Default) return;
            bodyLabel.horizontalAlignment = ToHorizontal(alignment);
        }

        internal static HorizontalAlignmentOptions ToHorizontal(TutorialTextAlignment a)
        {
            switch (a)
            {
                case TutorialTextAlignment.Center: return HorizontalAlignmentOptions.Center;
                case TutorialTextAlignment.Right: return HorizontalAlignmentOptions.Right;
                case TutorialTextAlignment.Justified: return HorizontalAlignmentOptions.Justified;
                default: return HorizontalAlignmentOptions.Left;
            }
        }
    }
}
