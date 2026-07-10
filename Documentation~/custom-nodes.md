# Writing custom nodes

TutorialKit is extended by adding your own node types. A custom node appears in the graph editor's
**Add Node** menu and in the JSON format **automatically** — no registration boilerplate.

## The minimal recipe

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TutorialKit;
using UnityEngine;

[Serializable]                                   // so it serializes on the graph
[TutorialNode("Game/Spawn Reward",               // menu path in the editor
              "Spawns a reward and waits for it to be collected.")]
public sealed class SpawnRewardNode : TutorialNode
{
    // Public fields become inspector fields on the node, edited in the graph.
    public string rewardId = "coin";
    public int amount = 10;

    // Optional: a one-line summary shown under the node title.
    public override string GetSummary(TutorialGraph graph) => $"{amount}x {rewardId}";

    // Do the work. Return the port to follow next, or null to end the tutorial.
    public override async UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
    {
        // Use adapters through ctx — never reference the runtime's internals directly.
        await ctx.Commands.InvokeAsync("spawnReward",
            new TutorialCommandContext("spawnReward", $"{rewardId}={amount}", null, ctx.Blackboard), ct);

        await ctx.Signals.WaitAsync("rewardCollected", ct);
        return OutPort;                          // the default single "Out" port
    }
}
```

That's the whole thing. Recompile and the node is in the menu.

## Multiple output ports (branching)

Override `OutputPorts` and return the chosen port name:

```csharp
[Serializable]
[TutorialNode("Game/Coin Flip")]
public sealed class CoinFlipNode : TutorialNode
{
    static readonly string[] Ports = { "Heads", "Tails" };
    public override System.Collections.Generic.IReadOnlyList<string> OutputPorts => Ports;

    public override UniTask<string> ExecuteAsync(TutorialRunContext ctx, CancellationToken ct)
        => UniTask.FromResult(ctx.Blackboard.Count % 2 == 0 ? "Heads" : "Tails");
}
```

## What you get from `ctx` (`TutorialRunContext`)

| Member | Use |
|--------|-----|
| `ctx.Vignette` | show/hide the highlight vignette |
| `ctx.Pointer` | show/hide the animated pointer |
| `ctx.TextBox` | show/hide text boxes |
| `ctx.InputLock` | lock/unlock interaction |
| `ctx.Persistence` | read/write completion & checkpoints |
| `ctx.Commands` | invoke registered game commands |
| `ctx.Signals` | emit / await game signals |
| `ctx.Targets` / `ctx.Resolve(ref)` | resolve target ids to scene elements |
| `ctx.Input` | poll input in a wait loop |
| `ctx.Blackboard` | share values between nodes this run |
| `ctx.Graph` | the running graph (id, etc.) |

## Rules

- **Honour cancellation.** Always thread `ct` into awaits (`UniTask.Delay(..., cancellationToken: ct)`,
  `WaitAsync(id, ct)`). When a tutorial is skipped/aborted the token cancels and your node should
  unwind. Undo any transient visual you created if you created it directly.
- **Stay on the adapter side.** Reach the game only through `ctx` services (commands/signals/targets),
  never through hard references — that keeps the graph reusable across projects.
- **Keep `TypeId` stable.** For content you publish as JSON, set a fixed id so renames don't break it:
  `[TutorialNode("Game/Spawn Reward") { TypeId = "spawn_reward" }]` — or just don't rename the class.

## Custom target provider nodes

To feed a highlight/pointer/text box a target computed by game logic, subclass `TargetNodeBase` — it
has a single `Target` data output and is evaluated on demand (not part of the flow). Connect its
output into the optional `Target` input on a `Show Vignette` / `Show Pointer` / `Show Text Box` /
`Wait For Input` node.

```csharp
[Serializable]
[TutorialNode("Game/Target: Nearest Enemy", "Highlights the closest enemy.")]
public sealed class NearestEnemyTargetNode : TargetNodeBase
{
    public float maxRange = 20f;

    protected override ITutorialTarget ResolveTarget(TutorialRunContext ctx)
    {
        // Return a dynamic target that re-finds the element each frame (so the highlight follows it).
        return new DynamicTutorialTarget(() =>
        {
            var e = EnemyManager.FindNearest(maxRange);
            return e != null ? e.transform : null;
        });
    }
}
```

You can also return a `RectTutorialTarget(() => someScreenRect)` to point at an explicit position/size,
or `ctx.Resolve(someRef)` to resolve a registered id.

## For the "just call my command" case

You usually don't need a custom node at all: use the built-in **Game Command** node and register a
handler:

```csharp
TutorialDirector.EnsureExists().Commands.Register("openShop", async (c, ct) =>
{
    await Shop.OpenAsync();
});
```
