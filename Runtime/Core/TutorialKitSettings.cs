using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Project-level TutorialKit configuration, loaded at runtime from a <c>Resources</c> folder
    /// (create/edit it from <b>Window ▸ TutorialKit ▸ Settings</b>). Currently holds optional overrides
    /// for the default pointer art — leave a field empty to fall back to the bundled sprite, then to a
    /// procedural shape. Games that want per-instance art can still assign the pointer view's fields.
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

        public Sprite PointerHand => pointerHand;
        public Sprite PointerHandOpen => pointerHandOpen;
        public Sprite PointerHandClosed => pointerHandClosed;
        public Sprite PointerArrow => pointerArrow;

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
