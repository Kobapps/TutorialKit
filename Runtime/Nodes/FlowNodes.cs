using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>Entry point marker. Passes straight through; conventional start of a graph.</summary>
    [Serializable]
    [TutorialNode("Flow/Start", "Entry point of the tutorial.", Color = "#2E7D32")]
    public sealed class StartNode : TutorialNode
    {
        public override bool HasInput => false;
        public override string DisplayName => "Start";
        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
            => UniTask.FromResult(OutPort);
    }

    /// <summary>Ends the tutorial (marks it complete).</summary>
    [Serializable]
    [TutorialNode("Flow/End", "Ends the tutorial successfully.", Color = "#455A64")]
    public sealed class EndNode : TutorialNode
    {
        private static readonly string[] NoPorts = Array.Empty<string>();
        public override IReadOnlyList<string> OutputPorts => NoPorts;
        public override string DisplayName => "End";
        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
            => UniTask.FromResult<string>(null);
    }

    /// <summary>Waits for a fixed amount of unscaled time.</summary>
    [Serializable]
    [TutorialNode("Wait/Wait Time", "Pause for a number of seconds.", Color = "#00838F")]
    public sealed class WaitTimeNode : TutorialNode
    {
        [Tooltip("How long to pause, in seconds.")]
        [Min(0f)] public float Seconds = 1f;
        [Tooltip("Ignore Time.timeScale so it still runs while the game is paused.")]
        public bool RealTime = true;

        public override string GetSummary(TutorialGraph graph) => $"{Seconds:0.##}s";

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, Seconds)), RealTime, cancellationToken: ct);
            return OutPort;
        }
    }

    /// <summary>Waits until a game action/signal is emitted on the signal bus.</summary>
    [Serializable]
    [TutorialNode("Wait/Wait For Signal", "Block until game code emits a named signal.", Color = "#00838F")]
    public sealed class WaitSignalNode : TutorialNode
    {
        [Tooltip("Signal id emitted via ITutorialSignalBus.Emit / TutorialSignalEmitter.")]
        public string SignalId = "signal.id";

        public override string GetSummary(TutorialGraph graph) => SignalId;

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await ctx.Signals.WaitAsync(SignalId, ct);
            return OutPort;
        }
    }

    /// <summary>Waits for player input: any input, a pointer press, or a tap on a target.</summary>
    [Serializable]
    [TutorialNode("Wait/Wait For Input", "Block until the player provides input.", Color = "#00838F")]
    public sealed class WaitInputNode : TutorialNode
    {
        public WaitInputKind Kind = WaitInputKind.AnyInput;
        [Tooltip("Target the player must tap when Kind = TapOnTarget.")]
        public TutorialTargetRef Target;

        public override string GetSummary(TutorialGraph graph) =>
            Kind == WaitInputKind.TapOnTarget ? $"tap {Target}" : Kind.ToString();

        private static readonly TutorialDataPort[] TargetInputPort = { new TutorialDataPort("Target", TutorialPortTypes.Target) };
        public override IReadOnlyList<TutorialDataPort> InputDataPorts => TargetInputPort;

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            switch (Kind)
            {
                case WaitInputKind.AnyInput:
                    await UniTask.WaitUntil(() => ctx.Input.AnyInputDownThisFrame, cancellationToken: ct);
                    break;
                case WaitInputKind.PointerDown:
                    await UniTask.WaitUntil(() => ctx.Input.TryGetPointerDown(out _), cancellationToken: ct);
                    break;
                case WaitInputKind.TapOnTarget:
                    await UniTask.WaitUntil(() =>
                    {
                        if (!ctx.Input.TryGetPointerDown(out var pos)) return false;
                        var t = ctx.ResolveTargetInput(this, "Target", Target);
                        return t != null && t.TryGetScreenRect(out var r) && r.Contains(pos);
                    }, cancellationToken: ct);
                    break;
            }
            return OutPort;
        }
    }

    /// <summary>Branches based on a simple condition. Follows the "True" or "False" port.</summary>
    [Serializable]
    [TutorialNode("Flow/Condition", "Branch on a persistence/blackboard condition.", Color = "#6A1B9A")]
    public sealed class ConditionNode : TutorialNode
    {
        public enum ConditionKind { BlackboardFlag, TutorialCompleted, CheckpointReached }

        public ConditionKind Kind = ConditionKind.BlackboardFlag;
        [Tooltip("Blackboard key / other-tutorial id / checkpoint id depending on Kind.")]
        public string Key = "flag";
        [Tooltip("Second id used by CheckpointReached (tutorial id). Empty = current tutorial.")]
        public string TutorialId = "";

        private static readonly string[] Ports = { "True", "False" };
        public override IReadOnlyList<string> OutputPorts => Ports;

        public override string GetSummary(TutorialGraph graph) => $"{Kind}: {Key}";

        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            bool result;
            switch (Kind)
            {
                case ConditionKind.BlackboardFlag:
                    result = ctx.Blackboard.TryGetValue(Key, out var v) && v is bool b && b;
                    break;
                case ConditionKind.TutorialCompleted:
                    result = ctx.Persistence.IsTutorialCompleted(Key);
                    break;
                case ConditionKind.CheckpointReached:
                    string tid = string.IsNullOrEmpty(TutorialId) ? ctx.Graph.TutorialId : TutorialId;
                    result = ctx.Persistence.IsCheckpointReached(tid, Key);
                    break;
                default:
                    result = false;
                    break;
            }
            return UniTask.FromResult(result ? "True" : "False");
        }
    }

    /// <summary>Records a checkpoint in persistence so the tutorial can resume/skip past it.</summary>
    [Serializable]
    [TutorialNode("Flow/Mark Checkpoint", "Persist a checkpoint id.", Color = "#6A1B9A")]
    public sealed class MarkCheckpointNode : TutorialNode
    {
        public string CheckpointId = "checkpoint";

        public override string GetSummary(TutorialGraph graph) => CheckpointId;

        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            ctx.Persistence.SetCheckpoint(ctx.Graph.TutorialId, CheckpointId);
            return UniTask.FromResult(OutPort);
        }
    }

    /// <summary>Sets a boolean flag on the run blackboard (for use with Condition nodes).</summary>
    [Serializable]
    [TutorialNode("Flow/Set Flag", "Set a blackboard boolean.", Color = "#6A1B9A")]
    public sealed class SetFlagNode : TutorialNode
    {
        public string Key = "flag";
        public bool Value = true;

        public override string GetSummary(TutorialGraph graph) => $"{Key} = {Value}";

        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            ctx.Blackboard[Key] = Value;
            return UniTask.FromResult(OutPort);
        }
    }
}
