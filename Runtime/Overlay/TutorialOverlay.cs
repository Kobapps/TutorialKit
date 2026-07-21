using UnityEngine;
using UnityEngine.UI;

namespace TutorialKit
{
    /// <summary>
    /// Builds and owns the top-most tutorial UI canvas and the three overlay services
    /// (<see cref="Vignette"/>, <see cref="Pointer"/>, <see cref="TextBox"/>). Renders above all
    /// game UI regardless of render pipeline (Screen-Space Overlay canvas).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialOverlay : MonoBehaviour
    {
        private RectTransform _root;
        private ITutorialTargetRegistry _targets;

        public Canvas Canvas { get; private set; }
        public VignetteView Vignette { get; private set; }
        public PointerView Pointer { get; private set; }
        public TextBoxView TextBox { get; private set; }

        public int SortingOrder
        {
            get => Canvas != null ? Canvas.sortingOrder : 0;
            set { if (Canvas != null) Canvas.sortingOrder = value; }
        }

        public static TutorialOverlay Create(ITutorialTargetRegistry targets, Transform parent = null)
        {
            var go = new GameObject("TutorialOverlay",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TutorialOverlay));
            if (parent != null) go.transform.SetParent(parent, false);

            var overlay = go.GetComponent<TutorialOverlay>();
            overlay._targets = targets;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            canvas.pixelPerfect = false;
            overlay.Canvas = canvas;
            overlay._root = (RectTransform)go.transform;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Vignette (back), Text box (middle), Pointer (front).
            overlay.Vignette = overlay.CreateFullScreenView<VignetteView>("Vignette", withImage: true);
            overlay.TextBox = overlay.CreateFullScreenView<TextBoxView>("TextBoxes", withImage: false);
            overlay.Pointer = overlay.CreateFullScreenView<PointerView>("Pointer", withImage: false);

            overlay.Vignette.Init(overlay);
            overlay.TextBox.Init(overlay);
            overlay.Pointer.Init(overlay);

            // Pointer must always render above the vignette dim and text boxes.
            overlay.Pointer.transform.SetAsLastSibling();

            return overlay;
        }

        /// <summary>
        /// Creates an extra full-screen <see cref="PointerView"/> layered above the others. Used by
        /// <see cref="TutorialPointers"/> to show ambient game pointers independently of the tutorial one,
        /// so several can be on screen at once and tutorials never disturb them.
        /// </summary>
        public PointerView CreatePointer(string name = "Pointer")
        {
            var view = CreateFullScreenView<PointerView>(name, withImage: false);
            view.Init(this);
            view.transform.SetAsLastSibling();
            return view;
        }

        private T CreateFullScreenView<T>(string name, bool withImage) where T : Component
        {
            var go = withImage
                ? new GameObject(name, typeof(RectTransform), typeof(Image))
                : new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            if (withImage)
            {
                var img = go.GetComponent<Image>();
                img.color = new Color(0, 0, 0, 0);
            }
            return go.AddComponent<T>();
        }

        /// <summary>Resolves a target reference through the registry.</summary>
        public ITutorialTarget Resolve(TutorialTargetRef reference)
        {
            if (!reference.HasValue || _targets == null) return null;
            return _targets.TryResolve(reference.TargetId, out var t) ? t : null;
        }

        /// <summary>Converts a screen-pixel point to an anchored position in the overlay canvas.</summary>
        public Vector2 ScreenToOverlay(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPoint, null, out var local);
            return local;
        }

        internal void SetTargetRegistry(ITutorialTargetRegistry targets) => _targets = targets;
    }
}
