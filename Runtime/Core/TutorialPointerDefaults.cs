using System;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Project-wide look and timing for the animated pointers (hand / arrow doing point, tap,
    /// double-tap, swipe, drag, merge). Every <see cref="PointerView"/> — the tutorial pointer and
    /// every standalone <see cref="TutorialPointers"/> pointer — reads these values, so a game's whole
    /// pointer feel can be tuned from one place (Settings ▸ TutorialKit ▸ Pointer Animation).
    /// All durations are in seconds at speed 1; a pointer's per-call <c>speed</c> multiplier still
    /// divides them at runtime (higher speed = shorter, snappier cycles).
    /// </summary>
    [Serializable]
    public sealed class TutorialPointerDefaults
    {
        [Header("Appearance")]
        [Tooltip("Pointer sprite size in pixels.")]
        public float Size = 110f;
        [Tooltip("Tint applied to the pointer and its tap ring.")]
        public Color Tint = Color.white;
        [Tooltip("Hand tip offset (fraction of size) so the fingertip — not the sprite centre — sits on the target.")]
        public Vector2 HandHotspot = new Vector2(0.40f, -0.42f);
        [Tooltip("Arrow tip offset (fraction of size) so the arrow point sits on the target.")]
        public Vector2 ArrowHotspot = new Vector2(0f, -0.45f);
        [Tooltip("Fade-out duration (seconds) when a pointer is hidden.")]
        public float HideFadeDuration = 0.15f;

        [Header("Point (idle bob)")]
        public TutorialPointTiming Point = new TutorialPointTiming();

        [Header("Tap")]
        public TutorialTapTiming Tap = new TutorialTapTiming();

        [Header("Double Tap")]
        public TutorialDoubleTapTiming DoubleTap = new TutorialDoubleTapTiming();

        [Header("Swipe")]
        public TutorialSwipeTiming Swipe = new TutorialSwipeTiming();

        [Header("Drag")]
        public TutorialDragTiming Drag = new TutorialDragTiming();

        [Header("Merge")]
        public TutorialMergeTiming Merge = new TutorialMergeTiming();
    }

    /// <summary>Timing for the idle "point" bob over a single target.</summary>
    [Serializable]
    public sealed class TutorialPointTiming
    {
        [Tooltip("Duration (seconds) of one half of the up/down bob.")]
        public float BobDuration = 0.6f;
        [Tooltip("How far the pointer dips downward (pixels) at the bottom of the bob.")]
        public float BobDistance = 18f;
        [Tooltip("Scale at the top of the bob (1 = no scaling).")]
        public float BobScale = 1.08f;
    }

    /// <summary>Timing for the repeated single tap over a target.</summary>
    [Serializable]
    public sealed class TutorialTapTiming
    {
        [Tooltip("Duration (seconds) of the press dip / release.")]
        public float PressDuration = 0.28f;
        [Tooltip("Scale the pointer shrinks to on press (1 = none).")]
        public float DipScale = 0.82f;
        [Tooltip("Peak alpha of the expanding tap ring.")]
        [Range(0f, 1f)] public float RingAlpha = 0.6f;
        [Tooltip("Ring scale at the start of the ripple.")]
        public float RingFromScale = 0.6f;
        [Tooltip("Ring scale at the end of the ripple.")]
        public float RingToScale = 1.4f;
        [Tooltip("Idle pause (seconds) between taps.")]
        public float RestDuration = 0.35f;
    }

    /// <summary>Timing for the two quick taps of a double tap. Defaults are tuned snappy.</summary>
    [Serializable]
    public sealed class TutorialDoubleTapTiming
    {
        [Tooltip("Duration (seconds) of each press dip / release — kept short so the pair reads as one gesture.")]
        public float PressDuration = 0.10f;
        [Tooltip("Scale the pointer shrinks to on each press (1 = none).")]
        public float DipScale = 0.8f;
        [Tooltip("Peak alpha of each expanding tap ring.")]
        [Range(0f, 1f)] public float RingAlpha = 0.55f;
        [Tooltip("Ring scale at the start of each ripple.")]
        public float RingFromScale = 0.55f;
        [Tooltip("Ring scale at the end of each ripple.")]
        public float RingToScale = 1.35f;
        [Tooltip("Tiny gap (seconds) between the two taps.")]
        public float GapDuration = 0.03f;
        [Tooltip("Longer rest (seconds) before the pair repeats.")]
        public float RestDuration = 0.40f;
    }

    /// <summary>Timing for the quick swipe (flick) from A to B.</summary>
    [Serializable]
    public sealed class TutorialSwipeTiming
    {
        [Tooltip("Duration (seconds) of the travel from A to B.")]
        public float MoveDuration = 0.5f;
        [Tooltip("Idle pause (seconds) before the swipe repeats.")]
        public float RestDuration = 0.25f;
    }

    /// <summary>Timing for the press-and-hold drag from A to B.</summary>
    [Serializable]
    public sealed class TutorialDragTiming
    {
        [Tooltip("Duration (seconds) of the grab (hand closes).")]
        public float GrabDuration = 0.2f;
        [Tooltip("Duration (seconds) of the held move from A to B.")]
        public float MoveDuration = 0.9f;
        [Tooltip("Duration (seconds) of the release (hand opens).")]
        public float ReleaseDuration = 0.2f;
        [Tooltip("Scale the hand shrinks to while grabbing (1 = none).")]
        public float GrabScale = 0.85f;
        [Tooltip("Idle pause (seconds) before the drag repeats.")]
        public float RestDuration = 0.35f;
    }

    /// <summary>Timing for the merge sweep + pulse toward the midpoint.</summary>
    [Serializable]
    public sealed class TutorialMergeTiming
    {
        [Tooltip("Duration (seconds) of the sweep from the first target to the midpoint.")]
        public float MoveDuration = 0.6f;
        [Tooltip("Duration (seconds) of each half of the pulse.")]
        public float PulseDuration = 0.15f;
        [Tooltip("Scale at the peak of the pulse (1 = none).")]
        public float PulseScale = 1.25f;
        [Tooltip("Idle pause (seconds) before the merge repeats.")]
        public float RestDuration = 0.3f;
    }
}
</content>
</invoke>
