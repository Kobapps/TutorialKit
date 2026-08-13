using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Mandatory entry point. Every graph has exactly one (the editor adds/keeps it and it can't be
    /// deleted or duplicated). Passes straight through to the first connected node.
    /// </summary>
    [Serializable]
    [TutorialNode("Flow/Start", "Entry point of the tutorial (one per graph, managed automatically).",
        Color = "#2E7D32", HideInMenu = true)]
    public class StartNode : TutorialNode
    {
        public override bool HasInput => false;
        public override string DisplayName => "Start";
        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
            => UniTask.FromResult(OutPort);
    }

    /// <summary>
    /// Terminal node — marks the tutorial (or a branch of it) complete. A graph needs at least one; the
    /// editor adds one if a graph has none. Branching flows can have several — one per terminating branch.
    /// </summary>
    [Serializable]
    [TutorialNode("Flow/End", "Ends the tutorial (or a branch). A graph needs at least one.",
        Color = "#455A64")]
    public class EndNode : TutorialNode
    {
        /// <summary>Shared empty port list — a subclass keeping the "terminal" shape can reuse it.</summary>
        protected static readonly string[] NoPorts = Array.Empty<string>();
        public override IReadOnlyList<string> OutputPorts => NoPorts;
        public override string DisplayName => "End";
        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
            => UniTask.FromResult<string>(null);
    }

    /// <summary>Waits for a fixed amount of unscaled time.</summary>
    [Serializable]
    [TutorialNode("Wait/Wait Time", "Pause for a number of seconds.", Color = "#00838F")]
    public class WaitTimeNode : TutorialNode
    {
        [Tooltip("How long to pause, in seconds.")]
        [Min(0f)] public float Seconds = 1f;
        [Tooltip("Ignore Time.timeScale so it still runs while the game is paused.")]
        public bool RealTime = true;

        public override string GetSummary(TutorialGraph graph) => $"{Seconds:0.##}s";

        /// <summary>How long to actually wait. Override to scale the delay (difficulty, replay speed…).</summary>
        protected virtual float GetSeconds(TutorialRunContext ctx) => Mathf.Max(0f, Seconds);

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(GetSeconds(ctx)), RealTime, cancellationToken: ct);
            return OutPort;
        }
    }

    /// <summary>Waits until a game action/signal is emitted on the signal bus.</summary>
    [Serializable]
    [TutorialNode("Wait/Wait For Signal", "Block until game code emits a named signal.", Color = "#00838F")]
    public class WaitSignalNode : TutorialNode
    {
        [Tooltip("Signal id emitted via ITutorialSignalBus.Emit / TutorialSignalEmitter.")]
        public string SignalId = "signal.id";

        public override string GetSummary(TutorialGraph graph) => SignalId;

        /// <summary>The signal to wait for. Override to compose it from run state (e.g. per-level ids).</summary>
        protected virtual string GetSignalId(TutorialRunContext ctx) => SignalId;

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await ctx.Signals.WaitAsync(GetSignalId(ctx), ct);
            return OutPort;
        }
    }

    /// <summary>Waits for player input: any input, a pointer press, or a tap on a target.</summary>
    [Serializable]
    [TutorialNode("Wait/Wait For Input", "Block until the player provides input.", Color = "#00838F")]
    public class WaitInputNode : TutorialNode
    {
        public WaitInputKind Kind = WaitInputKind.AnyInput;
        [Tooltip("Target the player must tap when Kind = TapOnTarget.")]
        public TutorialTargetRef Target;

        public override string GetSummary(TutorialGraph graph) =>
            Kind == WaitInputKind.TapOnTarget ? $"tap {Target}" : Kind.ToString();

        /// <summary>The single "Target" data input — reuse it when a subclass keeps the same port shape.</summary>
        protected static readonly TutorialDataPort[] TargetInputPort = { new TutorialDataPort("Target", TutorialPortTypes.Target) };
        public override IReadOnlyList<TutorialDataPort> InputDataPorts => TargetInputPort;

        public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        {
            await UniTask.WaitUntil(() => IsInputSatisfied(ctx), cancellationToken: ct);
            return OutPort;
        }

        /// <summary>
        /// Polled every frame until it returns true. Override to add input kinds of your own
        /// (call <c>base.IsInputSatisfied</c> for the built-in ones).
        /// </summary>
        protected virtual bool IsInputSatisfied(TutorialRunContext ctx)
        {
            switch (Kind)
            {
                case WaitInputKind.AnyInput:
                    return ctx.Input.AnyInputDownThisFrame;
                case WaitInputKind.PointerDown:
                    return ctx.Input.TryGetPointerDown(out _);
                case WaitInputKind.TapOnTarget:
                    return ctx.Input.TryGetPointerDown(out var pos) && IsTapOnTarget(ctx, pos);
                default:
                    return true;
            }
        }

        /// <summary>Hit test for TapOnTarget. Override to widen the hit area or accept several targets.</summary>
        protected virtual bool IsTapOnTarget(TutorialRunContext ctx, Vector2 screenPos)
        {
            var t = ctx.ResolveTargetInput(this, "Target", Target);
            return t != null && t.TryGetScreenRect(out var r) && r.Contains(screenPos);
        }
    }

    /// <summary>Branches based on a simple condition. Follows the "True" or "False" port.</summary>
    [Serializable]
    [TutorialNode("Flow/Condition", "Branch on a persistence/blackboard condition.", Color = "#6A1B9A")]
    public class ConditionNode : TutorialNode
    {
        public enum ConditionKind { BlackboardFlag, TutorialCompleted, CheckpointReached }

        /// <summary>Port followed when <see cref="Evaluate"/> returns true.</summary>
        public const string TruePort = "True";
        /// <summary>Port followed when <see cref="Evaluate"/> returns false.</summary>
        public const string FalsePort = "False";

        public ConditionKind Kind = ConditionKind.BlackboardFlag;
        [Tooltip("Blackboard key / other-tutorial id / checkpoint id depending on Kind.")]
        public string Key = "flag";
        [Tooltip("Second id used by CheckpointReached (tutorial id). Empty = current tutorial.")]
        public string TutorialId = "";

        /// <summary>The True/False ports — reuse when a subclass keeps the same branching shape.</summary>
        protected static readonly string[] Ports = { TruePort, FalsePort };
        public override IReadOnlyList<string> OutputPorts => Ports;

        public override string GetSummary(TutorialGraph graph) => $"{Kind}: {Key}";

        public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
            => UniTask.FromResult(Evaluate(ctx) ? TruePort : FalsePort);

        /// <summary>
        /// Decides which branch to follow. Override to add condition kinds of your own
        /// (call <c>base.Evaluate</c> for the built-in ones).
        /// </summary>
        protected virtual bool Evaluate(TutorialRunContext ctx)
        {
            switch (Kind)
            {
                case ConditionKind.BlackboardFlag:
                    return ctx.Blackboard.TryGetValue(Key, out var v) && v is bool b && b;
                case ConditionKind.TutorialCompleted:
                    return ctx.Persistence.IsTutorialCompleted(Key);
                case ConditionKind.CheckpointReached:
                    string tid = string.IsNullOrEmpty(TutorialId) ? ctx.Graph.TutorialId : TutorialId;
                    return ctx.Persistence.IsCheckpointReached(tid, Key);
                default:
                    return false;
            }
        }
    }

    /// <summary>Records a checkpoint in persistence so the tutorial can resume/skip past it.</summary>
    [Serializable]
    [TutorialNode("Flow/Mark Checkpoint", "Persist a checkpoint id.", Color = "#6A1B9A")]
    public class MarkCheckpointNode : TutorialNode
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
    public class SetFlagNode : TutorialNode
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
