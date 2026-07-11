using System;
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
    }
}
