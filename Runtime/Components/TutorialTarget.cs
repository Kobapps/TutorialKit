using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Drop this on any UI element or world object to make it addressable from a tutorial graph
    /// by a stable <see cref="Id"/>. It self-registers with the active target registry so authored
    /// graphs never hold direct scene references. Computes a screen-space rect for highlights/pointers.
    /// </summary>
    [AddComponentMenu("TutorialKit/Tutorial Target")]
    [DisallowMultipleComponent]
    public class TutorialTarget : MonoBehaviour, ITutorialTarget
    {
        [Tooltip("Stable id referenced by tutorial nodes. Defaults to the GameObject name if empty.")]
        [SerializeField] private string id;

        [Tooltip("Camera used to project a WORLD target to the screen. Defaults to Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("For world targets without a Renderer: half-size (world units) of the highlight box.")]
        [SerializeField] private Vector2 worldExtents = new Vector2(0.5f, 0.5f);

        private RectTransform _rect;
        private bool _rectCached;

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public RectTransform Rect
        {
            get
            {
                if (!_rectCached) { _rect = transform as RectTransform; _rectCached = true; }
                return _rect;
            }
        }

        public bool IsUI => Rect != null;
        public Transform WorldTransform => transform;
        public Camera ResolveCamera() => worldCamera != null ? worldCamera : Camera.main;

        protected virtual void OnEnable()
        {
            TutorialDirector.EnsureExists().Targets.Register(Id, this);
        }

        protected virtual void OnDisable()
        {
            var dir = TutorialDirector.Current;
            if (dir != null) dir.Targets.Unregister(Id);
        }

        /// <summary>
        /// Computes the target's axis-aligned bounding rect in screen pixels (origin bottom-left).
        /// Handles UGUI RectTransforms (any canvas render mode) and world objects (via Renderer bounds
        /// or the configured extents).
        /// </summary>
        public bool TryGetScreenRect(out Rect screenRect)
        {
            if (this == null) { screenRect = default; return false; }
            return TutorialTargetGeometry.TryGetScreenRect(transform, ResolveCamera(), worldExtents, out screenRect);
        }

        /// <summary>Screen-space centre of the target (pixels), or screen centre if unresolved.</summary>
        public Vector2 GetScreenCenter()
        {
            return TryGetScreenRect(out var r) ? r.center : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }
    }
}
