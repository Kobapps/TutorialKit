using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Anything a tutorial can highlight, point at, or anchor a text box to. Implemented by the
    /// <see cref="TutorialTarget"/> component, and by dynamic providers (<see cref="DynamicTutorialTarget"/>,
    /// <see cref="RectTutorialTarget"/>) that resolve an element via game logic at runtime.
    /// </summary>
    public interface ITutorialTarget
    {
        /// <summary>The target's current axis-aligned rect in screen pixels (origin bottom-left).</summary>
        bool TryGetScreenRect(out Rect screenRect);

        /// <summary>The target's current screen-space centre (pixels).</summary>
        Vector2 GetScreenCenter();
    }

    /// <summary>Shared screen-rect computation for UI (RectTransform) and world (Renderer/extents) elements.</summary>
    public static class TutorialTargetGeometry
    {
        private static readonly Vector3[] s_corners = new Vector3[4];

        public static bool TryGetScreenRect(Transform tr, Camera worldCamera, Vector2 worldExtents, out Rect screenRect)
        {
            screenRect = default;
            if (tr == null) return false;

            if (tr is RectTransform rect)
            {
                var canvas = rect.GetComponentInParent<Canvas>();
                Camera cam = null;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

                rect.GetWorldCorners(s_corners);
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, s_corners[i]);
                    min = Vector2.Min(min, sp);
                    max = Vector2.Max(max, sp);
                }
                screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                return true;
            }

            var camera = worldCamera != null ? worldCamera : Camera.main;
            if (camera == null) return false;

            var renderer = tr.GetComponent<Renderer>();
            Bounds b = renderer != null
                ? renderer.bounds
                : new Bounds(tr.position, new Vector3(worldExtents.x * 2f, worldExtents.y * 2f, 0f));

            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 mx = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 sp = camera.WorldToScreenPoint(corner);
                if (sp.z < 0f) continue;
                mn = Vector2.Min(mn, sp);
                mx = Vector2.Max(mx, sp);
            }
            if (mn.x > mx.x) return false;
            screenRect = Rect.MinMaxRect(mn.x, mn.y, mx.x, mx.y);
            return true;
        }
    }
}
