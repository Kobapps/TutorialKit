using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Simple, standalone API for showing animated pointers (hand / arrow doing point / tap / swipe /
    /// drag / merge) anywhere in the game — no tutorial graph or running sequence required. Every call
    /// returns a <see cref="PointerHandle"/> you keep and later <see cref="PointerHandle.Hide"/>. Several
    /// pointers can be on screen at once. Pointers render on the shared TutorialKit overlay, above all
    /// game UI, and are independent of tutorial sequences (a playing tutorial never disturbs them).
    /// <code>
    /// var hint = TutorialPointers.Tap(myButton.transform);   // looping tap hint that follows the button
    /// // ...later...
    /// hint.Hide();                                           // fade it out
    ///
    /// TutorialPointers.Swipe(cardA.transform, cardB.transform);       // swipe hint between two elements
    /// TutorialPointers.ShowAt(new Vector2(400, 300), PointerGesture.Point);
    /// TutorialPointers.Arrow(chestTransform);                        // arrow pointing at a world object
    /// TutorialPointers.HideAll();
    /// </code>
    /// </summary>
    public static class TutorialPointers
    {
        // Dedicated pointer views (pooled), separate from the overlay's single tutorial pointer.
        private static readonly List<PointerView> _free = new List<PointerView>();
        private static readonly List<PointerHandle> _active = new List<PointerHandle>();

        /// <summary>True while at least one game pointer is on screen.</summary>
        public static bool IsAnyVisible
        {
            get
            {
                for (int i = 0; i < _active.Count; i++)
                    if (_active[i] != null && _active[i].IsVisible) return true;
                return false;
            }
        }

        // ---- Single-target pointers (point / tap follow the element) ------------------------------

        /// <summary>Shows a pointer on a target element (world Transform or UI RectTransform); it follows the element.</summary>
        public static PointerHandle Show(Transform target, PointerGesture gesture = PointerGesture.Tap,
            PointerKind kind = PointerKind.Hand, float speed = 1f)
            => Show(Target(target), kind, gesture, speed);

        /// <summary>Shows a pointer on a registered target id (see <see cref="TutorialTargets"/>).</summary>
        public static PointerHandle Show(string targetId, PointerGesture gesture = PointerGesture.Tap,
            PointerKind kind = PointerKind.Hand, float speed = 1f)
            => Show(Target(targetId), kind, gesture, speed);

        /// <summary>Shows a pointer at a fixed screen position (pixels, origin bottom-left).</summary>
        public static PointerHandle ShowAt(Vector2 screenPosition, PointerGesture gesture = PointerGesture.Tap,
            PointerKind kind = PointerKind.Hand, float speed = 1f)
            => Show(TargetAt(screenPosition), kind, gesture, speed);

        /// <summary>Convenience: an idle "point" hint on a target.</summary>
        public static PointerHandle Point(Transform target, PointerKind kind = PointerKind.Hand, float speed = 1f)
            => Show(Target(target), kind, PointerGesture.Point, speed);

        /// <summary>Convenience: a repeated "tap" hint on a target.</summary>
        public static PointerHandle Tap(Transform target, float speed = 1f)
            => Show(Target(target), PointerKind.Hand, PointerGesture.Tap, speed);

        /// <summary>Convenience: an arrow that points at a target.</summary>
        public static PointerHandle Arrow(Transform target, PointerGesture gesture = PointerGesture.Point, float speed = 1f)
            => Show(Target(target), PointerKind.Arrow, gesture, speed);

        // ---- Path pointers (swipe / drag / merge between two points) ------------------------------

        public static PointerHandle Swipe(Transform from, Transform to, PointerKind kind = PointerKind.Hand, float speed = 1f)
            => ShowPath(PointerGesture.Swipe, Target(from), Target(to), kind, speed);
        public static PointerHandle Swipe(Vector2 from, Vector2 to, PointerKind kind = PointerKind.Hand, float speed = 1f)
            => ShowPath(PointerGesture.Swipe, TargetAt(from), TargetAt(to), kind, speed);

        public static PointerHandle Drag(Transform from, Transform to, PointerKind kind = PointerKind.Hand, float speed = 1f)
            => ShowPath(PointerGesture.Drag, Target(from), Target(to), kind, speed);
        public static PointerHandle Drag(Vector2 from, Vector2 to, PointerKind kind = PointerKind.Hand, float speed = 1f)
            => ShowPath(PointerGesture.Drag, TargetAt(from), TargetAt(to), kind, speed);

        public static PointerHandle Merge(Transform a, Transform b, PointerKind kind = PointerKind.Hand, float speed = 1f)
            => ShowPath(PointerGesture.Merge, Target(a), Target(b), kind, speed);
        public static PointerHandle Merge(Vector2 a, Vector2 b, PointerKind kind = PointerKind.Hand, float speed = 1f)
            => ShowPath(PointerGesture.Merge, TargetAt(a), TargetAt(b), kind, speed);

        // ---- Core -------------------------------------------------------------------------------

        /// <summary>Most general entry point: show a pointer on any <see cref="ITutorialTarget"/>.</summary>
        public static PointerHandle Show(ITutorialTarget target, PointerKind kind = PointerKind.Hand,
            PointerGesture gesture = PointerGesture.Tap, float speed = 1f, Vector2 screenOffset = default)
        {
            var req = PointerRequest.Default;
            req.Kind = kind;
            req.Gesture = gesture;
            req.Target = target;
            req.Speed = speed;
            req.ScreenOffset = screenOffset;
            return ShowRequest(req);
        }

        /// <summary>Show a two-point gesture (swipe / drag / merge) between two targets.</summary>
        public static PointerHandle ShowPath(PointerGesture gesture, ITutorialTarget from, ITutorialTarget to,
            PointerKind kind = PointerKind.Hand, float speed = 1f)
        {
            var req = PointerRequest.Default;
            req.Kind = kind;
            req.Gesture = gesture;
            req.Target = from;
            req.SecondaryTarget = to;
            req.Speed = speed;
            return ShowRequest(req);
        }

        /// <summary>Hides every game pointer shown through this API (tutorial pointers are untouched).</summary>
        public static void HideAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                _active[i]?.Hide();
        }

        // ---- Target adapters --------------------------------------------------------------------

        /// <summary>Wraps a Transform (world object or UI RectTransform) as a target the pointer follows.</summary>
        public static ITutorialTarget Target(Transform t) => new DynamicTutorialTarget(() => t);

        /// <summary>A target at a fixed screen point (pixels).</summary>
        public static ITutorialTarget TargetAt(Vector2 screenPosition, float size = 48f)
            => new RectTutorialTarget(new Rect(screenPosition.x - size * 0.5f, screenPosition.y - size * 0.5f, size, size));

        /// <summary>A target resolved from the registry by id each frame (see <see cref="TutorialTargets"/>).</summary>
        public static ITutorialTarget Target(string targetId) => new RegistryTarget(targetId);

        // ---- internals --------------------------------------------------------------------------

        private static PointerHandle ShowRequest(PointerRequest req)
        {
            var view = Acquire();
            var handle = new PointerHandle(view);
            _active.Add(handle);
            view.ShowAsync(req, CancellationToken.None).Forget();
            return handle;
        }

        private static PointerView Acquire()
        {
            var overlay = TutorialDirector.EnsureExists().EnsureOverlay();
            while (_free.Count > 0)
            {
                var v = _free[_free.Count - 1];
                _free.RemoveAt(_free.Count - 1);
                if (v != null) return v; // skip destroyed views (e.g. after a domain reload)
            }
            return overlay.CreatePointer("GamePointer");
        }

        // Called by a handle once its pointer has fully hidden, so the view can be reused.
        internal static void ReturnToPool(PointerHandle handle, PointerView view)
        {
            _active.Remove(handle);
            if (view != null) _free.Add(view);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _free.Clear();
            _active.Clear();
        }

        // Re-resolves a registered target id on every query, so pointers work even if the target is
        // registered after the pointer is shown, or swapped out at runtime.
        private sealed class RegistryTarget : ITutorialTarget
        {
            private readonly string _id;
            public RegistryTarget(string id) { _id = id; }

            private bool TryResolve(out ITutorialTarget target)
            {
                target = null;
                var dir = TutorialDirector.Current;
                return dir != null && dir.Targets.TryResolve(_id, out target) && target != null;
            }

            public bool TryGetScreenRect(out Rect screenRect)
            {
                if (TryResolve(out var t)) return t.TryGetScreenRect(out screenRect);
                screenRect = default;
                return false;
            }

            public Vector2 GetScreenCenter()
                => TryResolve(out var t) ? t.GetScreenCenter() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }
    }

    /// <summary>
    /// Handle to a pointer shown via <see cref="TutorialPointers"/>. Keep it to hide the pointer later.
    /// </summary>
    public sealed class PointerHandle
    {
        private PointerView _view;

        internal PointerHandle(PointerView view) { _view = view; }

        /// <summary>True while this pointer is on screen.</summary>
        public bool IsVisible => _view != null && _view.IsVisible;

        /// <summary>Fades the pointer out and recycles it. Safe to call more than once.</summary>
        public void Hide()
        {
            var view = _view;
            _view = null;
            if (view == null) return;
            HideRoutine(this, view).Forget();
        }

        /// <summary>Hides the pointer instantly (no fade) and recycles it.</summary>
        public void HideImmediate()
        {
            var view = _view;
            _view = null;
            if (view == null) return;
            view.HideImmediate();
            TutorialPointers.ReturnToPool(this, view);
        }

        private static async UniTaskVoid HideRoutine(PointerHandle handle, PointerView view)
        {
            try { await view.HideAsync(CancellationToken.None); }
            catch (OperationCanceledException) { }
            TutorialPointers.ReturnToPool(handle, view);
        }
    }
}
