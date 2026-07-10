using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TutorialKit;
using UnityEngine;

namespace TutorialKitDemo
{
    /// <summary>
    /// Example CUSTOM node living in a game assembly (not the package). It appears in the graph
    /// editor's "Add Node ▸ Demo ▸ Confetti" menu and in the JSON format automatically, purely by
    /// carrying the <see cref="TutorialNodeAttribute"/>. Demonstrates the custom-node wrapper.
    /// </summary>
    [Serializable]
    [TutorialNode("Demo/Confetti", "Fire a celebratory confetti burst via a game command.", Color = "#E91E63")]
    public sealed class DemoConfettiNode : TutorialNode
    {
        [Min(1)] public int burst = 30;

        public override string GetSummary(TutorialGraph graph) => $"{burst} pieces";

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await ctx.Commands.InvokeAsync("demo.confetti",
                new TutorialCommandContext("demo.confetti", burst.ToString(), null, ctx.Blackboard), ct);
            return OutPort;
        }
    }
}
