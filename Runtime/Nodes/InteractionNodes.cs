using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>Locks or unlocks game interaction (all, or a named group).</summary>
    [Serializable]
    [TutorialNode("Interaction/Set Input Lock", "Lock or unlock game interaction.", Color = "#AD1457")]
    public sealed class SetInputLockNode : TutorialNode
    {
        public InputLockMode Mode = InputLockMode.Lock;
        [Tooltip("Interaction group to affect. Empty = all interaction.")]
        public string Group = "";

        public override string GetSummary(TutorialGraph graph) =>
            $"{Mode} {(string.IsNullOrEmpty(Group) ? "ALL" : Group)}";

        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            string group = string.IsNullOrEmpty(Group) ? null : Group;
            if (Mode == InputLockMode.Lock) ctx.InputLock.Lock(group);
            else ctx.InputLock.Unlock(group);
            return UniTask.FromResult(OutPort);
        }
    }

    /// <summary>Invokes a named game command registered by the game (play animation, open popup…).</summary>
    [Serializable]
    [TutorialNode("Interaction/Game Command", "Invoke a registered game command.", Color = "#5D4037")]
    public sealed class GameCommandNode : TutorialNode
    {
        public string CommandId = "command.id";
        [Tooltip("Free-form argument, or key=value;key2=value2 pairs parsed into Parameters.")]
        public string Argument = "";
        [Tooltip("If true, wait for the command's async handler to finish before continuing.")]
        public bool WaitForCompletion = true;

        public override string GetSummary(TutorialGraph graph) => CommandId;

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            var cmdCtx = new TutorialCommandContext(CommandId, Argument, ParseParameters(Argument), ctx.Blackboard);
            if (WaitForCompletion)
                await ctx.Commands.InvokeAsync(CommandId, cmdCtx, ct);
            else
                ctx.Commands.InvokeAsync(CommandId, cmdCtx, ct).Forget();
            return OutPort;
        }

        private static IReadOnlyDictionary<string, string> ParseParameters(string argument)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(argument) || argument.IndexOf('=') < 0) return dict;
            foreach (var pair in argument.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string k = pair.Substring(0, eq).Trim();
                string v = pair.Substring(eq + 1).Trim();
                if (k.Length > 0) dict[k] = v;
            }
            return dict;
        }
    }

    /// <summary>Emits a signal on the bus (e.g. to notify game systems the tutorial reached a point).</summary>
    [Serializable]
    [TutorialNode("Interaction/Emit Signal", "Emit a signal on the bus.", Color = "#5D4037")]
    public sealed class EmitSignalNode : TutorialNode
    {
        public string SignalId = "signal.id";

        public override string GetSummary(TutorialGraph graph) => SignalId;

        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            ctx.Signals.Emit(SignalId);
            return UniTask.FromResult(OutPort);
        }
    }
}
