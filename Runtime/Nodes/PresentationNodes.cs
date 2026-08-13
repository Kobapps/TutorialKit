using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>Shows the dimming vignette with a highlight hole over a target.</summary>
    [Serializable]
    [TutorialNode("Highlight/Show Vignette", "Dim the screen and cut a hole over a target.", Color = "#C2185B")]
    public class ShowVignetteNode : TutorialNode
    {
        [Tooltip("Id of the TutorialTarget to cut a hole around. Empty = dim the whole screen.")]
        public TutorialTargetRef Target;
        [Tooltip("Extra targets — cut an additional hole for each (multi-highlight). All holes share the shape/softness below.")]
        public List<TutorialTargetRef> AdditionalTargets = new List<TutorialTargetRef>();
        [Tooltip("Draw with the project-wide vignette style (Settings ▸ TutorialKit) instead of the fields " +
                 "below. Turn off to use this node's own shape/colour/softness.")]
        public bool UseGlobalStyle = true;
        [Tooltip("Circle, or rounded Rectangle hole.")]
        public HighlightShape Shape = HighlightShape.Circle;
        [Tooltip("Edge softness of the hole. 0 = hard edge, 1 = very soft falloff.")]
        [Range(0f, 1f)] public float Softness = 0.15f;
        [Tooltip("Extra pixels of padding around the target bounds.")]
        public float Padding = 12f;
        [Tooltip("Corner radius for the Rectangle shape (pixels).")]
        public float CornerRadius = 24f;
        [Tooltip("Fade-in/out duration in seconds.")]
        public float FadeDuration = 0.25f;
        [Tooltip("Colour and opacity of the dimmed area (alpha = how dark).")]
        public Color OverlayColor = new Color(0f, 0f, 0f, 0.78f);
        [Tooltip("Block taps on the dimmed area while letting taps pass through the hole.")]
        public bool BlockRaycastsOutsideHole = true;
        [Tooltip("If false, the holes are locked too (pure-visual highlight — the player can't tap through them).")]
        public bool AllowClicksThroughHoles = true;

        public override string GetSummary(TutorialGraph graph)
        {
            int extra = AdditionalTargets != null ? AdditionalTargets.Count : 0;
            if (extra > 0) return $"{extra + 1} holes";
            return Target.HasValue ? $"{Shape} → {Target}" : "full dim";
        }

        // Multi-capacity: connect several target provider nodes to cut several holes.
        /// <summary>The multi-capacity "Target" data input — reuse when a subclass keeps the same port shape.</summary>
        protected static readonly TutorialDataPort[] TargetInputPort = { new TutorialDataPort("Target", TutorialPortTypes.Target, multi: true) };
        public override IReadOnlyList<TutorialDataPort> InputDataPorts => TargetInputPort;

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await ctx.Vignette.ShowAsync(BuildRequest(ctx), ct);
            return OutPort;
        }

        /// <summary>
        /// Every hole this node cuts: the connected target providers, the <see cref="Target"/> field and
        /// <see cref="AdditionalTargets"/>. Override to add or filter holes.
        /// </summary>
        protected virtual List<ITutorialTarget> CollectTargets(TutorialRunContext ctx)
        {
            var list = new List<ITutorialTarget>();
            list.AddRange(ctx.ResolveTargetInputs(this, "Target")); // all connected target providers
            var fieldTarget = ctx.Resolve(Target);
            if (fieldTarget != null) list.Add(fieldTarget);
            if (AdditionalTargets != null)
                foreach (var t in AdditionalTargets)
                {
                    var it = ctx.Resolve(t);
                    if (it != null) list.Add(it);
                }
            return list;
        }

        /// <summary>
        /// Builds the request handed to <see cref="IVignetteService"/>. Override (usually as
        /// <c>var req = base.BuildRequest(ctx); … ; return req;</c>) to adjust the look per node.
        /// </summary>
        protected virtual HighlightRequest BuildRequest(TutorialRunContext ctx)
        {
            // Style comes from either the project-wide defaults (Use Global Style) or this node's fields.
            var s = UseGlobalStyle ? TutorialKitSettings.Instance : null;
            var g = s != null ? s.VignetteDefaults : null;

            return new HighlightRequest
            {
                Targets = CollectTargets(ctx).ToArray(),
                Shape = g != null ? g.Shape : Shape,
                Softness = g != null ? g.Softness : Softness,
                Padding = g != null ? g.Padding : Padding,
                CornerRadius = g != null ? g.CornerRadius : CornerRadius,
                FadeDuration = g != null ? g.FadeDuration : FadeDuration,
                OverlayColor = g != null ? g.OverlayColor : OverlayColor,
                BlockRaycasts = BlockRaycastsOutsideHole,
                BlockHoles = !AllowClicksThroughHoles,
            };
        }
    }

    /// <summary>Hides the vignette.</summary>
    [Serializable]
    [TutorialNode("Highlight/Hide Vignette", "Fade out the vignette.", Color = "#C2185B")]
    public class HideVignetteNode : TutorialNode
    {
        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await ctx.Vignette.HideAsync(ct);
            return OutPort;
        }
    }

    /// <summary>Shows an animated hand/arrow pointer performing a gesture.</summary>
    [Serializable]
    [TutorialNode("Pointer/Show Pointer", "Show an animated pointer gesture.", Color = "#EF6C00")]
    public class ShowPointerNode : TutorialNode
    {
        [Tooltip("Hand or arrow pointer sprite.")]
        public PointerKind Kind = PointerKind.Hand;
        [Tooltip("Point, Tap, Double Tap, Swipe, Drag, or Merge animation.")]
        public PointerGesture Gesture = PointerGesture.Tap;
        [Tooltip("Target the pointer starts on / points at.")]
        public TutorialTargetRef Target;
        [Tooltip("End target for Swipe / Drag / Merge gestures.")]
        public TutorialTargetRef SecondaryTarget;
        [Tooltip("Pixel nudge applied to the resolved position.")]
        public Vector2 ScreenOffset;
        [Tooltip("Gesture speed multiplier (1 = default).")]
        [Min(0.1f)] public float Speed = 1f;

        public override string GetSummary(TutorialGraph graph) => $"{Gesture} @ {Target}";

        /// <summary>The "Target"/"Secondary" data inputs — reuse when a subclass keeps the same port shape.</summary>
        protected static readonly TutorialDataPort[] PointerInputs =
        {
            new TutorialDataPort("Target", TutorialPortTypes.Target),
            new TutorialDataPort("Secondary", TutorialPortTypes.Target),
        };
        public override IReadOnlyList<TutorialDataPort> InputDataPorts => PointerInputs;

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await ctx.Pointer.ShowAsync(BuildRequest(ctx), ct);
            return OutPort;
        }

        /// <summary>
        /// Builds the request handed to <see cref="IPointerService"/>. Override (usually as
        /// <c>var req = base.BuildRequest(ctx); … ; return req;</c>) to change the gesture, targets or timing.
        /// </summary>
        protected virtual PointerRequest BuildRequest(TutorialRunContext ctx) => new PointerRequest
        {
            Kind = Kind,
            Gesture = Gesture,
            Target = ctx.ResolveTargetInput(this, "Target", Target),
            SecondaryTarget = ctx.ResolveTargetInput(this, "Secondary", SecondaryTarget),
            ScreenOffset = ScreenOffset,
            Speed = Speed,
            Loop = true,
        };
    }

    /// <summary>Hides the pointer.</summary>
    [Serializable]
    [TutorialNode("Pointer/Hide Pointer", "Fade out the pointer.", Color = "#EF6C00")]
    public class HidePointerNode : TutorialNode
    {
        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await ctx.Pointer.HideAsync(ct);
            return OutPort;
        }
    }

    /// <summary>Shows a text box (default styled or a registered custom prefab).</summary>
    [Serializable]
    [TutorialNode("Text/Show Text Box", "Display a styled text box.", Color = "#1565C0")]
    public class ShowTextBoxNode : TutorialNode
    {
        [Tooltip("Logical id so this box can be hidden later by a Hide Text Box node.")]
        public string BoxId = "main";
        [Tooltip("Empty = built-in default box. Otherwise a key registered via ITextBoxService.RegisterPrefab.")]
        public string PrefabKey = "";
        public string Title = "";
        [TextArea(2, 6)] public string Body = "Tutorial text...";
        public TextBoxPlacement Placement = TextBoxPlacement.Bottom;
        public Vector2 NormalizedPosition = new Vector2(0.5f, 0.2f);
        [Tooltip("Target used for AboveTarget / BelowTarget placement.")]
        public TutorialTargetRef Target;
        public bool ShowContinueButton = true;
        public string ContinueLabel = "Next";
        [Tooltip("Horizontal alignment of the body text. Default = follow the project setting.")]
        public TutorialTextAlignment BodyAlignment = TutorialTextAlignment.Default;
        [Tooltip("If true, this node blocks until the player presses continue.")]
        public bool WaitForDismiss = true;
        public bool Typewriter = true;
        [Tooltip("Characters per second. 0 = use the project default typewriter speed.")]
        [Min(0f)] public float TypewriterCps = 45f;

        public override string GetSummary(TutorialGraph graph)
        {
            if (string.IsNullOrEmpty(Body)) return Placement.ToString();
            return Body.Length <= 28 ? Body : Body.Substring(0, 27) + "…";
        }

        /// <summary>The single "Target" data input — reuse when a subclass keeps the same port shape.</summary>
        protected static readonly TutorialDataPort[] TargetInputPort = { new TutorialDataPort("Target", TutorialPortTypes.Target) };
        public override IReadOnlyList<TutorialDataPort> InputDataPorts => TargetInputPort;

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await ctx.TextBox.ShowAsync(BuildRequest(ctx), ct);
            return OutPort;
        }

        /// <summary>
        /// Builds the request handed to <see cref="ITextBoxService"/>. Override (usually as
        /// <c>var req = base.BuildRequest(ctx); … ; return req;</c>) to localize the text, swap the
        /// prefab key, or reposition the box.
        /// </summary>
        protected virtual TextBoxRequest BuildRequest(TutorialRunContext ctx) => new TextBoxRequest
        {
            Id = BoxId,
            PrefabKey = PrefabKey,
            Title = Title,
            Body = Body,
            Placement = Placement,
            NormalizedPosition = NormalizedPosition,
            Target = ctx.ResolveTargetInput(this, "Target", Target),
            ShowContinueButton = ShowContinueButton,
            ContinueLabel = ContinueLabel,
            WaitForDismiss = WaitForDismiss,
            Typewriter = Typewriter,
            TypewriterCps = TypewriterCps,
            BodyAlignment = BodyAlignment,
        };
    }

    /// <summary>Hides a text box by id.</summary>
    [Serializable]
    [TutorialNode("Text/Hide Text Box", "Hide a text box by id.", Color = "#1565C0")]
    public class HideTextBoxNode : TutorialNode
    {
        [Tooltip("Logical id of the text box to hide (matches a Show Text Box node's Box Id).")]
        public string BoxId = "main";

        public override string GetSummary(TutorialGraph graph) => BoxId;

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await ctx.TextBox.HideAsync(BoxId, ct);
            return OutPort;
        }
    }
}
