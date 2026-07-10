using System;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// A target resolved by game logic at runtime. Register a resolver that finds the element (e.g. a
    /// dynamically spawned grid cell) each time the tutorial needs its position — so highlights and
    /// pointers follow elements that are created, moved, or re-selected at runtime.
    /// </summary>
    public sealed class DynamicTutorialTarget : ITutorialTarget
    {
        private readonly Func<Transform> _resolver;
        private readonly Camera _camera;
        private readonly Vector2 _worldExtents;

        public DynamicTutorialTarget(Func<Transform> resolver, Camera camera = null, Vector2 worldExtents = default)
        {
            _resolver = resolver;
            _camera = camera;
            _worldExtents = worldExtents == default ? new Vector2(0.5f, 0.5f) : worldExtents;
        }

        public bool TryGetScreenRect(out Rect screenRect)
        {
            var t = _resolver != null ? _resolver.Invoke() : null;
            if (t == null) { screenRect = default; return false; }
            return TutorialTargetGeometry.TryGetScreenRect(t, _camera, _worldExtents, out screenRect);
        }

        public Vector2 GetScreenCenter() =>
            TryGetScreenRect(out var r) ? r.center : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    /// <summary>A target defined by an explicit screen rect (or one computed each frame). Use to control
    /// position/size directly, e.g. anchor a hint to an arbitrary point on screen.</summary>
    public sealed class RectTutorialTarget : ITutorialTarget
    {
        private readonly Func<Rect> _rect;

        public RectTutorialTarget(Rect fixedRect) : this(() => fixedRect) { }
        public RectTutorialTarget(Func<Rect> rectProvider) { _rect = rectProvider; }

        public bool TryGetScreenRect(out Rect screenRect)
        {
            if (_rect == null) { screenRect = default; return false; }
            screenRect = _rect();
            return true;
        }

        public Vector2 GetScreenCenter() =>
            TryGetScreenRect(out var r) ? r.center : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    /// <summary>Convenience API for registering dynamic targets from game code.</summary>
    public static class TutorialTargets
    {
        /// <summary>Registers a target whose element is found by <paramref name="resolver"/> each time.</summary>
        public static void RegisterDynamic(string id, Func<Transform> resolver, Camera camera = null) =>
            TutorialDirector.EnsureExists().Targets.Register(id, new DynamicTutorialTarget(resolver, camera));

        /// <summary>Registers a target at an explicit (or per-frame computed) screen rect.</summary>
        public static void RegisterRect(string id, Func<Rect> rectProvider) =>
            TutorialDirector.EnsureExists().Targets.Register(id, new RectTutorialTarget(rectProvider));

        /// <summary>Registers any custom target provider.</summary>
        public static void Register(string id, ITutorialTarget target) =>
            TutorialDirector.EnsureExists().Targets.Register(id, target);

        public static void Unregister(string id) => TutorialDirector.Current?.Targets.Unregister(id);
    }
}
