# TutorialKit

A AAA-quality, adapter-based framework for authoring and playing tutorial sequences in Unity 6.
Author branching, multi-step flows in a visual **graph editor**; play them at runtime to highlight
UI, point with animated hands, show styled text, gate input, run custom game commands, and persist
progress — all decoupled from your game through small **adapters**.

- **Async** via [UniTask]; **tweens** via [DOTween].
- **Standalone** package — drop it in, wire ~4 adapters, ship.
- **Remote-updatable** — load/patch tutorials from JSON without a client rebuild.

---

## Installation

**Requires** Unity 6 (6000.0+), plus **UniTask** and **DOTween** (see below).

Install via the Package Manager — **Window ▸ Package Manager ▸ + ▸ Add package from git URL…**:

```
https://github.com/Kobapps/TutorialKit.git
```

or add it to `Packages/manifest.json`:

```json
"com.tutorialkit": "https://github.com/Kobapps/TutorialKit.git"
```

Pin a version by appending a tag, e.g. `…/TutorialKit.git#v0.8.3`.

### Dependencies

- **UniTask** — add `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` as a git-URL package.
- **DOTween** — import from the [Asset Store][DOTween] and run its setup panel once.
- `com.unity.ugui` (TextMeshPro) and `com.unity.inputsystem` come in automatically as declared dependencies.

### Samples

Import **Basic Tutorials** from the package's **Samples** tab in the Package Manager — a demo scene and
example graphs (highlight, pointer, text, waits, commands, dynamic targets).

---

## Quick start

1. Add a **Tutorial Graph**: `Assets ▸ Create ▸ TutorialKit ▸ Tutorial Graph`.
2. Open it: double-click the asset, or `Window ▸ TutorialKit ▸ Tutorial Graph Editor`.
   Right-click the canvas ▸ **Add Node** to build a flow. Right-click a node ▸ **Set as Start**.
3. Mark game elements: add a **Tutorial Target** component to any UI element or world object and
   give it an id (e.g. `play_button`). Reference that id from `Show Vignette` / `Show Pointer` nodes.
4. Start it from a **Tutorial Trigger** component (On Start / On Signal / Manual), or from code:

```csharp
using TutorialKit;

var handle = TutorialDirector.EnsureExists().Play(myGraph);
await handle.Completion;          // UniTask
```

5. Press **▶ Test in Play** in the graph editor to run it and watch the live node.

## Core concepts

| Piece | Role |
|------|------|
| `TutorialGraph` | ScriptableObject holding nodes + connections (the authored sequence). |
| `TutorialNode` | One step. Returns the output port to follow next. |
| `TutorialDirector` | Runtime entry point / composition root. `Play(graph)` → `TutorialHandle`. |
| `TutorialHandle` | Observe/await/skip/abort a running tutorial. |
| `TutorialTarget` | Marks a scene element addressable by id. |
| `TutorialTargets` | Register **dynamic** targets from code — `RegisterDynamic(id, () => FindElement())` for runtime-found elements, or `RegisterRect(id, () => rect)` for an explicit position/size. |
| `TutorialTrigger` | Starts a graph from a trigger, gated by conditions. |

## Adapters (the game boundary)

The runtime references **no game types**. Provide your own implementations before the first
`Play` (defaults are auto-installed otherwise):

```csharp
var dir = TutorialDirector.EnsureExists();
dir.SetPersistence(new MySaveSystemAdapter());     // IPersistenceService
dir.SetInputLock(new MyInputLockAdapter());        // IInputLockService

// Register game commands nodes can call & await:
dir.Commands.Register("openShop", async (ctx, ct) => { await Shop.OpenAsync(); });

// Let a Wait-For-Signal node react to gameplay:
dir.Signals.Emit("firstMatchMade");
```

| Adapter | Default | Purpose |
|---------|---------|---------|
| `IPersistenceService` | PlayerPrefs | tutorial completion + checkpoints |
| `IInputLockService` | event-based | lock/unlock interaction groups |
| `IGameCommandRegistry` | in-memory | invoke named game commands |
| `ITutorialSignalBus` | in-memory | game → tutorial signals |
| `ITutorialTargetRegistry` | in-memory | resolve target ids to scene elements |

## Node library

- **Flow**: Start, End, Condition, Mark Checkpoint, Set Flag
- **Wait**: Wait Time, Wait For Input (any / pointer / tap-on-target), Wait For Signal
- **Highlight**: Show/Hide Vignette (circle or rounded-rect hole, softness, padding; add
  `Additional Targets` to cut **multiple holes** at once; taps pass through the holes unless you turn
  off `Allow Clicks Through Holes` to lock them too)
- **Pointer**: Show/Hide Pointer (point, tap, swipe, drag, merge) — ships with bundled hand & arrow
  art (CC0, Kenney); the drag gesture even closes the hand into a fist. Assign your own
  `handSprite`/`arrowSprite` on the pointer view to override.
- **Text**: Show/Hide Text Box (default styled box or custom prefab, typewriter, wait-for-continue)
- **Interaction**: Set Input Lock, Game Command, Emit Signal
- **Target providers**: Target By Id, Target By Screen Position (subclass `TargetNodeBase` for custom
  logic) — connect their **Target output** into the optional **Target input** on a vignette / pointer /
  text box to inject a target dynamically instead of hard-coding an id.

## Data ports

Alongside flow connections, nodes can expose typed **data ports** (teal). A *target provider* node
outputs a `Target`; wire it into the optional `Target` input of `Show Vignette` / `Show Pointer` /
`Show Text Box` / `Wait For Input` to control what they point at from a node (which may run game
logic). The **vignette's `Target` input is multi-capacity** — connect several providers to cut several
holes at once. The port framework is generic (`TutorialDataPort` + `EvaluatePort`) so new port types
can be added later.

## Remote loading

```csharp
var graph = await RemoteTutorialLoader.LoadFromUrlAsync("https://cdn.example.com/ftue.json", ct);
TutorialDirector.EnsureExists().Play(graph);
```

Export any graph to JSON from its inspector (**Export JSON…**) and edit/host it externally.

## Graph editor

**Window ▸ TutorialKit ▸ Tutorial Graph Editor** — a draggable/collapsible **minimap**, a
**blackboard** panel for typed variables, node **groups**, copy/paste & duplicate, full **undo/redo**,
one-click **Auto Layout**, an **inspector** for the selected node (with field tooltips), an animated
pulse on the live node during **▶ Test in Play**, icon toolbar, and right-click **Add Node** search.

Toggle **Vertical** in the toolbar to flow the graph **top → bottom** (VFX-graph style: flow ports move
to the top/bottom of each node, data ports stay on the sides); Auto Layout follows the chosen direction.

Connections: an output port can **fan out to several nodes** — those branches run concurrently and
join. Many nodes may also converge into one input. Use `Condition` (True/False) for either/or branches.

### Blackboard
Declare `Bool/Int/Float/String` variables in the blackboard panel — each default uses a
type-appropriate editor (bool → checkbox, etc.). Defaults seed `ctx.Blackboard` when the tutorial
runs. `Set Flag` and `Condition` nodes read/write them by key, and any flag they reference is added
to the panel automatically. Live values are shown next to each variable during Play.

### Live debugger
In Play mode the editor **auto-attaches to the running tutorial** (toggle with **Attach**) — loading
its graph (even code-built ones), pulsing the active node, marking the visited-node trail and the
traversed edges, showing live blackboard values (`→ value`), and reporting `▶ Running` / `✓ Completed`.

## Settings & AI authoring skill

**Window ▸ TutorialKit ▸ Settings** installs an **AI authoring skill** — an instruction file that
teaches an AI assistant (Claude Code, etc.) how to author, configure, test, and debug tutorials in
your project. Its node reference is generated live from your project, so **custom nodes are documented
automatically**. Re-run *Update* after adding new node types.

The same window has a **Default Pointer Art** section: create a `TutorialKitSettings` asset (pre-filled
with the bundled CC0 art) and swap the hand/arrow sprites to re-skin every pointer project-wide. The
asset lives in a `Resources` folder so runtime and builds pick it up; an empty field falls back to the
bundled default. Per-tutorial art can still be assigned on the pointer view.

## Custom nodes

See [Documentation~/custom-nodes.md](Documentation~/custom-nodes.md). In short: subclass
`TutorialNode`, add `[TutorialNode("Menu/Path")]`, implement `ExecuteAsync`. It appears in the graph
editor, the JSON format, and the AI skill automatically.

## Requirements

Unity 6000.0+, UniTask, DOTween, com.unity.ugui (TextMeshPro), Input System.

[UniTask]: https://github.com/Cysharp/UniTask
[DOTween]: http://dotween.demigiant.com/
