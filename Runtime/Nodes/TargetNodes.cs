using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Base class for nodes that PRODUCE a target on a data output port. Not part of the execution
    /// flow — its <see cref="ResolveTarget"/> is pulled on demand when a consumer (vignette / pointer /
    /// text box) has its target input connected. Subclass it to inject a target using game logic.
    /// </summary>
    [Serializable]
    public abstract class TargetNodeBase : TutorialNode
    {
        private static readonly string[] NoFlowPorts = Array.Empty<string>();
        private static readonly TutorialDataPort[] OutPorts = { new TutorialDataPort("Target", TutorialPortTypes.Target) };

        public override bool HasInput => false;                       // not in the flow
        public override IReadOnlyList<string> OutputPorts => NoFlowPorts; // no flow output
        public override IReadOnlyList<TutorialDataPort> OutputDataPorts => OutPorts;

        public override object EvaluatePort(string outputPort, TutorialRunContext ctx) => ResolveTarget(ctx);

        // Never executed in the flow; present only to satisfy the base contract.
        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
            => UniTask.FromResult<string>(null);

        /// <summary>Produce the target (may use game logic / the run context).</summary>
        protected abstract ITutorialTarget ResolveTarget(TutorialRunContext ctx);
    }

    /// <summary>Provides a target from a registered id (a TutorialTarget or a dynamic target).</summary>
    [Serializable]
    [TutorialNode("Target/By Id", "Outputs a target resolved from a registered id.", Color = "#00897B")]
    public sealed class TargetByIdNode : TargetNodeBase
    {
        [Tooltip("Id of a registered TutorialTarget or dynamic target.")]
        public TutorialTargetRef Target;

        public override string DisplayName => "Target: Id";
        public override string GetSummary(TutorialGraph graph) => Target.HasValue ? Target.TargetId : "(none)";

        protected override ITutorialTarget ResolveTarget(TutorialRunContext ctx) => ctx.Resolve(Target);
    }

    /// <summary>Provides a target at an explicit normalized screen position and pixel size.</summary>
    [Serializable]
    [TutorialNode("Target/By Screen Position", "Outputs a target at a normalized screen position/size.", Color = "#00897B")]
    public sealed class TargetByPositionNode : TargetNodeBase
    {
        [Tooltip("Screen position in 0..1 (x=left→right, y=bottom→top).")]
        public Vector2 NormalizedPosition = new Vector2(0.5f, 0.5f);
        [Tooltip("Size of the target area in pixels.")]
        public Vector2 PixelSize = new Vector2(140f, 140f);

        public override string DisplayName => "Target: Position";
        public override string GetSummary(TutorialGraph graph) => $"({NormalizedPosition.x:0.##}, {NormalizedPosition.y:0.##})";

        protected override ITutorialTarget ResolveTarget(TutorialRunContext ctx)
        {
            Vector2 pos = NormalizedPosition;
            Vector2 size = PixelSize;
            return new RectTutorialTarget(() =>
            {
                float cx = pos.x * Screen.width;
                float cy = pos.y * Screen.height;
                return new Rect(cx - size.x * 0.5f, cy - size.y * 0.5f, size.x, size.y);
            });
        }
    }
}
