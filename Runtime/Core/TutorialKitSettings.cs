using System;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Project-wide default look for the dimming vignette. A <see cref="ShowVignetteNode"/> with
    /// "Use Global Style" enabled draws itself with these values instead of its own fields, so a whole
    /// game's highlights can be restyled from one place (Settings ▸ TutorialKit).
    /// </summary>
    [Serializable]
    public sealed class TutorialVignetteDefaults
    {
        [Tooltip("Circle, or rounded Rectangle hole.")]
        public HighlightShape Shape = HighlightShape.Circle;
        [Tooltip("Colour and opacity of the dimmed area (alpha = how dark).")]
        public Color OverlayColor = new Color(0f, 0f, 0f, 0.78f);
        [Tooltip("Edge softness of the hole. 0 = hard edge, 1 = very soft falloff.")]
        [Range(0f, 1f)] public float Softness = 0.15f;
        [Tooltip("Extra pixels of padding around the target bounds.")]
        public float Padding = 12f;
        [Tooltip("Corner radius for the Rectangle shape (pixels).")]
        public float CornerRadius = 24f;
        [Tooltip("Fade-in/out duration in seconds.")]
        public float FadeDuration = 0.25f;
    }

    /// <summary>
    /// Project-wide default look/behaviour for text boxes. Applied to the built-in default box, and used
    /// to resolve a node's alignment / typewriter speed when it's left on "Default" (Settings ▸ TutorialKit).
    /// </summary>
    [Serializable]
    public sealed class TutorialTextBoxDefaults
    {
        [Tooltip("Background colour of the default text box panel.")]
        public Color PanelColor = new Color(0.10f, 0.12f, 0.16f, 0.96f);
        [Tooltip("Accent colour (the continue button) of the default text box.")]
        public Color AccentColor = new Color(0.23f, 0.48f, 0.84f, 1f);
        [Tooltip("Text colour of the default text box.")]
        public Color TextColor = Color.white;
        [Tooltip("Default horizontal alignment of body text (used when a node's alignment is 'Default').")]
        public TutorialTextAlignment BodyAlignment = TutorialTextAlignment.Left;
        [Tooltip("Default typewriter speed in characters/second (used when a node leaves speed at 0).")]
        public float TypewriterCps = 45f;
        [Tooltip("Default show/hide animation for the built-in text box.")]
        public TextBoxAnimation Animation = TextBoxAnimation.Script;
    }

    /// <summary>
    /// Project-level TutorialKit configuration, loaded at runtime from a <c>Resources</c> folder
    /// (create/edit it from <b>Tools ▸ TutorialKit ▸ Settings</b>). Holds optional overrides for the
    /// default pointer art (leave a field empty to fall back to the bundled sprite, then a procedural
    /// shape), the bundled shader, the animation backend, and project-wide default styling for vignettes
    /// and text boxes. Games that want per-instance art can still assign the pointer view's fields.
    /// </summary>
    public sealed class TutorialKitSettings : ScriptableObject
    {
        /// <summary>Resource name (the asset must live in a <c>Resources</c> folder as this name).</summary>
        public const string ResourceName = "TutorialKitSettings";

        [Header("Default pointer art")]
        [Tooltip("Pointing hand used for point / tap / swipe / merge. Empty = bundled default.")]
        [SerializeField] private Sprite pointerHand;
        [Tooltip("Open hand shown while hovering / releasing during a drag. Empty = bundled default.")]
        [SerializeField] private Sprite pointerHandOpen;
        [Tooltip("Closed fist shown while grabbing during a drag. Empty = bundled default.")]
        [SerializeField] private Sprite pointerHandClosed;
        [Tooltip("Arrow used by the Arrow pointer kind. Empty = bundled default.")]
        [SerializeField] private Sprite pointerArrow;

        [Tooltip("Uniform scale for the pointer sprites, project-wide. 1 = drawn at the pointer size set under " +
                 "Pointer Animation. Raise it when custom art reads smaller than the bundled hand/arrow (art with " +
                 "transparent padding usually needs 1.5–3). Only the sprites scale — the tap ring and the gesture " +
                 "distances keep following the pointer size.")]
        [Range(0.1f, 8f)]
        [SerializeField] private float pointerArtScale = 1f;

        public Sprite PointerHand => pointerHand;
        public Sprite PointerHandOpen => pointerHandOpen;
        public Sprite PointerHandClosed => pointerHandClosed;
        public Sprite PointerArrow => pointerArrow;

        /// <summary>Project-wide multiplier applied to every pointer sprite (see <c>pointerArtScale</c>).
        /// A non-positive value (e.g. an asset serialized before this field existed) resolves to 1.</summary>
        public float PointerArtScale => pointerArtScale > 0f ? pointerArtScale : 1f;

        [Header("Bundled shaders")]
        [Tooltip("The overlay vignette shader (TutorialKit/UIVignette). The overlay looks it up at runtime, " +
                 "but a shader referenced only via Shader.Find gets stripped from a build — assigning it here " +
                 "keeps it in the build because this asset lives in Resources. Leave set to the bundled shader.")]
        [SerializeField] private Shader vignetteShader;

        /// <summary>The vignette overlay shader, if assigned. Null → the overlay falls back to <c>Shader.Find</c>.</summary>
        public Shader VignetteShader => vignetteShader;

        [Header("Animation")]
        [Tooltip("Which animation backend the overlays use. \"native\" (built-in, no dependency) or the " +
                 "id of a registered adapter such as \"dotween\". Empty = native.")]
        [SerializeField] private string tweenAdapterId = TutorialTween.NativeId;

        /// <summary>Selected animation backend id (see <see cref="TutorialTween"/>). Empty/unknown → built-in.</summary>
        public string TweenAdapterId => tweenAdapterId;

        [Header("Default vignette style")]
        [Tooltip("Project-wide vignette look. A Show Vignette node with 'Use Global Style' on draws with these.")]
        [SerializeField] private TutorialVignetteDefaults vignetteDefaults = new TutorialVignetteDefaults();

        /// <summary>Project-wide default vignette look (never null on a loaded asset).</summary>
        public TutorialVignetteDefaults VignetteDefaults => vignetteDefaults ?? (vignetteDefaults = new TutorialVignetteDefaults());

        [Header("Default text box style")]
        [Tooltip("Project-wide text box look/behaviour. Applied to the built-in default box.")]
        [SerializeField] private TutorialTextBoxDefaults textBoxDefaults = new TutorialTextBoxDefaults();

        /// <summary>Project-wide default text box look/behaviour (never null on a loaded asset).</summary>
        public TutorialTextBoxDefaults TextBoxDefaults => textBoxDefaults ?? (textBoxDefaults = new TutorialTextBoxDefaults());

        [Header("Pointer animation")]
        [Tooltip("Project-wide pointer look and per-gesture timings for every animated pointer.")]
        [SerializeField] private TutorialPointerDefaults pointerDefaults = new TutorialPointerDefaults();

        /// <summary>Project-wide pointer appearance and gesture timings (never null on a loaded asset).</summary>
        public TutorialPointerDefaults PointerDefaults => pointerDefaults ?? (pointerDefaults = new TutorialPointerDefaults());

        /// <summary>Pointer defaults from the loaded settings asset, or a fresh default set if none exists.</summary>
        public static TutorialPointerDefaults ResolvePointerDefaults()
        {
            var s = Instance;
            return s != null ? s.PointerDefaults : new TutorialPointerDefaults();
        }

        /// <summary>Pointer sprite scale from the loaded settings asset, or 1 if none exists.</summary>
        public static float ResolvePointerArtScale()
        {
            var s = Instance;
            return s != null ? s.PointerArtScale : 1f;
        }

        private static TutorialKitSettings _instance;
        private static bool _looked;

        /// <summary>The project's settings asset, or null if none exists (then defaults are used).</summary>
        public static TutorialKitSettings Instance
        {
            get
            {
                if (!_looked)
                {
                    _instance = Resources.Load<TutorialKitSettings>(ResourceName);
                    _looked = true;
                }
                return _instance;
            }
        }

        /// <summary>Forget the cached instance (call after creating/editing the asset in the editor).</summary>
        public static void ClearCache()
        {
            _instance = null;
            _looked = false;
        }
    }
}
