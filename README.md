# TutorialKit

A AAA-quality, adapter-based framework for authoring and playing tutorial sequences in Unity 6.
Author branching, multi-step flows in a visual **graph editor**; play them at runtime to highlight
UI, point with animated hands, show styled text, gate input, run custom game commands, and persist
progress — all decoupled from your game through small **adapters**.

- **Async** via [UniTask]; **animations** through a swappable backend (a dependency-free built-in one,
  or DOTween, or your own — pick it in Settings).
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
"com.kobapps.tutorialkit": "https://github.com/Kobapps/TutorialKit.git"
```

Pin a version by appending a tag, e.g. `…/TutorialKit.git#v0.8.3`.

### Dependencies

- **UniTask** — add `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` as a git-URL package.
- **DOTween** — *optional*. Animations use a built-in, dependency-free backend by default. To use DOTween
  instead, install it and pick it in **Settings ▸ Animation Backend** (that adds a `TUTORIALKIT_DOTWEEN`
  define; the adapter uses only DOTween's core API, so it doesn't need the DOTween module setup).
- `com.unity.ugui` (TextMeshPro) and `com.unity.inputsystem` come in automatically as declared dependencies.

### Samples

Import **Basic Tutorials** from the package's **Samples** tab in the Package Manager — a demo scene and
example graphs (highlight, pointer, text, waits, commands, dynamic targets).

---

## Quick start

1. Add a **Tutorial Graph**: `Assets ▸ Create ▸ TutorialKit ▸ Tutorial Graph`.
2. Open it: double-click the asset, or `Tools ▸ TutorialKit ▸ Tutorial Graph Editor`.
   Every graph has a built-in **Start** node (the fixed entry — it can't be deleted) and at least one
   **End**; branching flows can have several Ends (one per terminating branch). Right-click the canvas ▸
   **Add Node** to build the flow. Any branch that doesn't reach an End gets a ⚠ badge.
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

## Tutorial settings

Every `TutorialGraph` carries a **Tutorial Settings** block. Edit it in the graph editor's side panel
under the **Tutorial** tab (next to the **Node** tab, which shows the selected node), in the graph
asset's own inspector, or from code via `graph.Settings`. The director enforces it for **every** entry
point (trigger, code, remote), so games never re-implement it:

| Setting | What it does |
|------|------|
| **Play Mode** | **Single Use** (default) plays once ever and saves completion. **Recurring** plays every time it's triggered and saves nothing — hints and reminders. **Once Per Session** plays once per app run, again after a restart. |
| **Max Plays** | *Recurring only.* Stop after this many completed plays. 0 = unlimited. |
| **Cooldown Seconds** | *Recurring only.* Minimum real time between plays. Saved, so it survives a restart. |
| **When Busy** | Triggered while another tutorial is running: **Interrupt** (abort the other one), **Ignore** (drop it), or **Queue** (start when the other finishes). |
| **Lock Input While Playing** | Holds an input lock (optionally a named group) for the whole tutorial. |
| **Pause Game While Playing** | Zeroes `Time.timeScale` for the duration. Overlay animations are unscaled, so they keep running. |
| **Allow Skip** | Whether `handle.Skip()` is honoured. Turn off for a mandatory tutorial. |

```csharp
graph.Settings.PlayMode = TutorialPlayMode.Recurring;
graph.Settings.MaxPlays = 3;                          // nag at most 3 times…
graph.Settings.CooldownSeconds = 120f;                // …and at most once every 2 minutes
graph.Settings.WhenBusy = TutorialBusyPolicy.Queue;   // never cut off a running tutorial
```

`director.CanPlay(graph, out var reason)` reports whether the settings currently allow a play (and why
not), and `Play(graph, force: true)` bypasses the gating. A play is only counted when a tutorial
reaches its end or is skipped — aborting never burns one. The graph inspector shows the saved history
and its **Reset Saved Progress** button clears completion, play count, and cooldown together.

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
- **Pointer**: Show/Hide Pointer (point, tap, double-tap, swipe, drag, merge) — ships with bundled hand
  & arrow art (CC0, Kenney); the drag gesture even closes the hand into a fist. Assign your own
  `handSprite`/`arrowSprite` on the pointer view to override.
- **Text**: Show/Hide Text Box (default styled box or custom prefab, typewriter, body-text alignment,
  wait-for-continue)
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

Export or import a graph as JSON from the **graph editor toolbar** (Manage ▸ **Export** / **Import**) or
the graph's inspector — Import brings a JSON in as a new tutorial asset. Edit/host the JSON externally.

## Graph editor

**Tools ▸ TutorialKit ▸ Tutorial Graph Editor** — a draggable/collapsible **minimap**, a
**blackboard** panel for typed variables, node **groups**, copy/paste & duplicate, full **undo/redo**,
one-click **Auto Layout**, an **inspector** for the selected node (with field tooltips), an animated
pulse on the live node during **▶ Test in Play**, a **grouped toolbar** (manage / layout / view /
info), a soft **dot-grid** canvas, and right-click **Add Node** search.

The toolbar's **layout** button flips the graph between left → right and **top → bottom** (VFX-graph
style: flow ports move to the top/bottom of each node, data ports stay on the sides); Auto Layout
follows the chosen direction.
The choice is **saved per graph**, and graphs left on *Use Default* follow the project-wide default you
pick in **Settings ▸ Tools ▸ Default graph layout**.

Connections: an output port can **fan out to several nodes** — those branches run concurrently and
join. Many nodes may also converge into one input. Use `Condition` (True/False) for either/or branches.

### Blackboard
Declare `Bool/Int/Float/String` variables in the blackboard panel — each default uses a
type-appropriate editor (bool → checkbox, etc.). Defaults seed `ctx.Blackboard` when the tutorial
runs. `Set Flag` and `Condition` nodes read/write them by key, and any flag they reference is added
to the panel automatically. Live values are shown next to each variable during Play.

### Live debugger
Start a tutorial in Play mode and the graph editor **opens automatically on it and live-attaches** — no
need to open the window first (turn this off in **Settings ▸ Tools**). It loads the running graph (even
code-built ones), pulses the active node, marks the visited-node trail and the traversed edges, shows
live blackboard values (`→ value`), and reports `▶ Running` / `✓ Completed`. If the window is already
open it just follows along; the **Attach** toolbar toggle controls whether it tracks the running run.

## Settings & AI authoring skill

**Tools ▸ TutorialKit ▸ Settings** installs an **AI authoring skill** — an instruction file that
teaches an AI assistant (Claude Code, etc.) how to author, configure, test, and debug tutorials in
your project. It has the assistant **create and edit tutorial `.asset`s** through the editor API
(`TutorialKit.Editor.TutorialGraphAuthoring` — `CreateGraph` / `AddNode<T>` / `Connect` / `Chain` /
`ConnectData` / `Layout` / `Save`) rather than building graphs in throwaway code, so the result stays
editable in the graph window. Its node reference is generated live from your project, so **custom nodes
are documented automatically**. Re-run *Update* after adding new node types.

The same window has a **Default Pointer Art** section: create a `TutorialKitSettings` asset (pre-filled
with the bundled CC0 art) and swap the hand/arrow sprites to re-skin every pointer project-wide. The
asset lives in a `Resources` folder so runtime and builds pick it up; an empty field falls back to the
bundled default. Per-tutorial art can still be assigned on the pointer view.

Two scale knobs sit in that same section, because custom art almost never matches the bundled hand's
footprint (padding around the sprite makes it read small in game):

- **Pointer Size (px)** — the base size of every pointer; scales the sprite *and* the tap ring.
- **Art Scale (×)** — multiplies the **sprites only**, on top of the size. Padded art usually needs
  `1.5`–`3`. The tap ring and gesture distances keep following the size, so raising this enlarges the
  hand/arrow without inflating the ripple.

Both apply to every pointer in the game — Show Pointer nodes and the standalone `TutorialPointers` API.
Sprites are fitted by their longest side with the aspect preserved, so non-square art is no longer
squashed into a square box.

It also has a **Default Vignette & Text Box Style** section — project-wide defaults so a whole game
reads consistently. Set the vignette look (shape, overlay colour, softness, padding, corner radius,
fade) and the text box look (panel/accent/text colour, body alignment, typewriter speed, animation)
once. A Show Vignette node with **Use Global Style** on draws with the vignette defaults; the built-in
text box uses the text box defaults, and a node's `Default` alignment / `0` typewriter speed resolve to
them (set explicit values on a node to override).

Finally, a **Pointer Animation** section controls the look and per-gesture timing of *every* animated
pointer (Show Pointer nodes and the standalone `TutorialPointers` API) from one place: size (the same
field mirrored in Default Pointer Art), tint,
hand/arrow tip hotspot and hide-fade, plus timing groups for Point, Tap, Double Tap, Swipe, Drag and
Merge (durations, dip/pulse scales, tap-ring alpha and scales, idle rest). Durations are seconds at
speed 1 and are still divided by a pointer's per-call `speed`. Enter Play mode and show a pointer to
preview changes live; a **Reset to defaults** button restores the built-in feel.

## Custom nodes

See [Documentation~/custom-nodes.md](Documentation~/custom-nodes.md). In short: subclass
`TutorialNode`, add `[TutorialNode("Menu/Path")]`, implement `ExecuteAsync`. It appears in the graph
editor, the JSON format, and the AI skill automatically. Select the node and click **Edit Script** in
the inspector header to jump straight to its `.cs` file.

## Requirements

Unity 6000.0+, UniTask, DOTween, com.unity.ugui (TextMeshPro), Input System.

[UniTask]: https://github.com/Cysharp/UniTask
[DOTween]: http://dotween.demigiant.com/
