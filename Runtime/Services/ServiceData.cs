using System;
using System.Collections.Generic;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Reference to a scene element by stable id. Resolved at runtime through
    /// <see cref="ITutorialTargetRegistry"/> so authored graphs never hold scene references.
    /// </summary>
    [Serializable]
    public struct TutorialTargetRef
    {
        [Tooltip("Id of a TutorialTarget component in the scene.")]
        public string TargetId;

        public bool HasValue => !string.IsNullOrEmpty(TargetId);

        public TutorialTargetRef(string id) { TargetId = id; }

        public static implicit operator TutorialTargetRef(string id) => new TutorialTargetRef(id);
        public override string ToString() => TargetId;
    }

    /// <summary>Everything the vignette service needs to draw and follow a highlight.</summary>
    public struct HighlightRequest
    {
        /// <summary>Resolved targets — one hole per target (multi-highlight). Empty = full-screen dim.</summary>
        public ITutorialTarget[] Targets;
        public HighlightShape Shape;
        [Range(0f, 1f)] public float Softness;   // 0 = hard edge, 1 = very soft falloff
        public float Padding;                     // extra pixels around the target bounds
        public float CornerRadius;                // for Rectangle shape
        public float FadeDuration;
        public Color OverlayColor;                // dimmed area colour (with alpha)
        public bool BlockRaycasts;                // block taps outside the hole
        public bool BlockHoles;                   // also block taps INSIDE the holes (pure-visual lock)

        public static HighlightRequest Default => new HighlightRequest
        {
            Shape = HighlightShape.Circle,
            Softness = 0.15f,
            Padding = 12f,
            CornerRadius = 24f,
            FadeDuration = 0.25f,
            OverlayColor = new Color(0f, 0f, 0f, 0.78f),
            BlockRaycasts = true,
        };
    }

    /// <summary>Describes a pointer gesture to display.</summary>
    public struct PointerRequest
    {
        public PointerKind Kind;
        public PointerGesture Gesture;
        public ITutorialTarget Target;          // primary / start
        public ITutorialTarget SecondaryTarget; // end target for swipe/drag/merge
        public Vector2 ScreenOffset;            // pixel nudge applied to resolved position
        public float Speed;                     // gesture cycle speed multiplier (1 = default)
        public bool Loop;

        public static PointerRequest Default => new PointerRequest
        {
            Kind = PointerKind.Hand,
            Gesture = PointerGesture.Tap,
            Speed = 1f,
            Loop = true,
        };
    }

    /// <summary>Describes a text box to display.</summary>
    public struct TextBoxRequest
    {
        public string Id;               // logical id so it can be hidden later; default "main"
        public string PrefabKey;        // null/empty => default styled text box
        public string Title;
        public string Body;
        public TextBoxPlacement Placement;
        public Vector2 NormalizedPosition; // used when Placement == Custom (0..1 screen)
        public ITutorialTarget Target;     // used for AboveTarget / BelowTarget
        public bool ShowContinueButton;
        public string ContinueLabel;
        public bool WaitForDismiss;     // if true, ShowAsync completes when user taps continue
        public bool Typewriter;
        public float TypewriterCps;     // characters per second

        public static TextBoxRequest Default => new TextBoxRequest
        {
            Id = "main",
            Placement = TextBoxPlacement.Bottom,
            NormalizedPosition = new Vector2(0.5f, 0.2f),
            ShowContinueButton = true,
            ContinueLabel = "Next",
            WaitForDismiss = true,
            Typewriter = true,
            TypewriterCps = 45f,
        };
    }

    /// <summary>Context passed to a registered game command handler.</summary>
    public sealed class TutorialCommandContext
    {
        public string CommandId { get; }
        /// <summary>Free-form string argument authored on the node.</summary>
        public string Argument { get; }
        /// <summary>Parsed key=value parameters (from the node's argument, if any).</summary>
        public IReadOnlyDictionary<string, string> Parameters { get; }
        /// <summary>The running tutorial's shared blackboard.</summary>
        public IDictionary<string, object> Blackboard { get; }

        public TutorialCommandContext(string commandId, string argument,
            IReadOnlyDictionary<string, string> parameters, IDictionary<string, object> blackboard)
        {
            CommandId = commandId;
            Argument = argument;
            Parameters = parameters;
            Blackboard = blackboard;
        }
    }
}
