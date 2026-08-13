# Changelog

All notable changes to TutorialKit are documented here.

## [1.2.0] — 2026-08-13

### Changed
- **The shipped nodes are no longer `sealed`.** Every built-in node (`StartNode`, `EndNode`,
  `WaitTimeNode`, `WaitSignalNode`, `WaitInputNode`, `ConditionNode`, `MarkCheckpointNode`,
  `SetFlagNode`, `ShowVignetteNode`, `HideVignetteNode`, `ShowPointerNode`, `HidePointerNode`,
  `ShowTextBoxNode`, `HideTextBoxNode`, `SetInputLockNode`, `GameCommandNode`, `EmitSignalNode`,
  `TargetByIdNode`, `TargetByPositionNode`) can now be subclassed by a game, so a project can keep a
  node's authoring fields, ports and editor look and change only the part it cares about.

### Added
- **`protected virtual` extension points on the built-in nodes**, so subclasses rarely have to
  reimplement `ExecuteAsync`: `BuildRequest(ctx)` on Show Vignette / Show Pointer / Show Text Box
  (plus `CollectTargets(ctx)` on the vignette), `IsInputSatisfied(ctx)` and `IsTapOnTarget(ctx, pos)`
  on Wait For Input, `Evaluate(ctx)` on Condition, `GetSeconds(ctx)` on Wait Time, `GetSignalId(ctx)`
  on Wait For Signal, `BuildCommandContext(ctx)` and `ParseParameters(arg)` on Game Command, and
  `GetGroup(ctx)` on Set Input Lock. The requests are structs, so the usual shape is
  `var req = base.BuildRequest(ctx); req.Field = …; return req;`.
- **`ConditionNode.TruePort` / `ConditionNode.FalsePort`** constants for the branch port names.
- The built-in port arrays are `protected static` (`TargetInputPort`, `PointerInputs`, `Ports`,
  `NoPorts`, `TargetNodeBase.OutPorts` / `NoFlowPorts`) so a subclass keeping the same port shape can
  reuse them instead of redeclaring.
- **Docs:** a "Extending a built-in node" section in `Documentation~/custom-nodes.md` with the hook
  table and the two Unity gotchas — `[Serializable]` and `[TutorialNode(...)]` are not inherited, so a
  subclass must re-apply both (without the latter it still works, but lands under `Custom/<ClassName>`
  in the Add Node menu with its class name as its JSON `TypeId`).

## [1.1.0] — 2026-08-04

### Added
- **Global pointer Art Scale.** `TutorialKitSettings` gained a `pointerArtScale` multiplier, shown as
  **Art Scale (×)** in *Tools ▸ TutorialKit ▸ Settings ▸ Default Pointer Art* — right beside the sprite
  overrides. It scales the pointer **sprites only** (hand, open/closed hand, arrow) for every pointer in
  the game: Show Pointer nodes and the standalone `TutorialPointers` API. Custom art usually carries
  transparent padding and so reads much smaller in game than the bundled hand at the same size; `1.5`–`3`
  brings it back up. The tap ring and the gesture distances keep following the pointer size, so the
  ripple doesn't inflate with the art. Restoring the bundled art also restores the scale to `1`.
- **Pointer Size mirrored into the Default Pointer Art section.** The base `Size` (px) — which scales the
  sprite *and* the tap ring — now appears next to the art overrides as well as under Pointer Animation,
  so the section a game opens after swapping in its own art has both scale knobs in it, with a live
  "sprites drawn at N px" readout.

### Fixed
- **Non-square pointer art is no longer squashed.** Pointer sprites were forced into a square
  `Size × Size` rect, distorting any art that isn't 1:1. They are now fitted by their longest side with
  the aspect preserved, and the tip hotspot is offset against the fitted rect (so the fingertip/arrow
  point still lands on the target at any scale). The drag gesture re-fits on each open/closed hand swap,
  which matters when custom hand poses have different aspects. Bundled art is square, so its look is
  unchanged.

## [1.0.0] — 2026-07-31

### Changed
- **The three TutorialKit windows moved from the Window menu to Tools.** The Tutorial Browser, the
  Tutorial Graph Editor and Settings are now under **Tools ▸ TutorialKit**, so the kit's commands
  sit in one place beside every other Kobapps tool rather than in Unity's Window menu. The README,
  the sample README and the `TutorialKitSettings` tooltip were updated to match.

  First stable release. Nothing about the authoring model, the graph format or the runtime API
  changed here — the version simply now says what the 0.26 line already was.

## [0.26.1] — 2026-07-23

### Fixed
- **Compile error in `TutorialPointerDefaults.cs`** (`CS8803: Top-level statements must precede
  namespace and type declarations`). Stray trailing text after the namespace's closing brace, introduced
  when the file was written in 0.26.0, broke compilation. Removed it. No API or behaviour changes.

## [0.26.0] — 2026-07-23

### Added
- **Pointer animation settings editor.** `TutorialKitSettings` now carries a `PointerDefaults` block
  that controls the look and per-gesture timing of *every* animated pointer — both Show Pointer nodes
  and the standalone `TutorialPointers` API — from one place. Appearance: size, tint, hand/arrow tip
  hotspot, hide-fade duration. Per-gesture timing groups (`Point`, `Tap`, `DoubleTap`, `Swipe`,
  `Drag`, `Merge`) expose their durations (seconds at speed 1), dip/pulse scales, tap-ring alpha and
  from/to scales, and the idle rest between cycles. All durations are still divided by a pointer's
  per-call `speed`. Edit them in **Window ▸ TutorialKit ▸ Settings ▸ Pointer Animation** (with a
  "Reset to defaults" button); read them from code via `TutorialKitSettings.ResolvePointerDefaults()`.
  Previously these values were hardcoded inside `PointerView`.

### Changed
- **Faster double-tap.** The `DoubleTap` gesture is now noticeably snappier by default — shorter press
  (0.16s → 0.10s), tighter gap between the two taps (0.06s → 0.03s) and a shorter rest before it
  repeats (0.55s → 0.40s) — so the two taps read as one crisp gesture. All still tunable in the new
  Pointer Animation settings.

## [0.25.0] — 2026-07-22

### Added
- **Double-tap pointer gesture.** New `PointerGesture.DoubleTap` performs two quick taps in succession
  then a pause, over a single target (which it follows like Point/Tap). Available on the Show Pointer
  node's Gesture dropdown and via the standalone API: `TutorialPointers.DoubleTap(target)`.
- **Text box text alignment.** Show Text Box nodes gain a `BodyAlignment` field
  (`Default` / `Left` / `Center` / `Right` / `Justified`) controlling the horizontal alignment of the
  body text. `Default` follows the project-wide setting.
- **Global default styles for vignettes and text boxes.** `TutorialKitSettings` now carries a
  `VignetteDefaults` block (shape, overlay colour, softness, padding, corner radius, fade) and a
  `TextBoxDefaults` block (panel/accent/text colour, body alignment, typewriter speed, animation). The
  built-in text box uses the text box defaults; a Show Vignette node with the new **Use Global Style**
  toggle (on by default) draws with the vignette defaults instead of its own fields. A node's `Default`
  alignment and `0` typewriter speed resolve to these. Edit them in **Window ▸ TutorialKit ▸ Settings ▸
  Default Vignette & Text Box Style**.

## [0.24.0] — 2026-07-21

### Added
- **Standalone pointer API (`TutorialPointers`).** Show animated pointers (hand / arrow doing point /
  tap / swipe / drag / merge) anywhere in the game — no tutorial graph or running sequence required.
  Each call returns a `PointerHandle` you keep and later `Hide()`; several pointers can be on screen at
  once. Convenience methods `Point` / `Tap` / `Arrow` / `Swipe` / `Drag` / `Merge` / `ShowAt`, plus a
  general `Show(ITutorialTarget, …)`, with overloads taking a `Transform`, a screen `Vector2`, a
  registered target id, or any `ITutorialTarget`. `HideAll()` clears them.
  ```csharp
  var hint = TutorialPointers.Tap(button.transform); // looping hint that follows the button
  hint.Hide();
  TutorialPointers.Swipe(cardA.transform, cardB.transform);
  ```
  Pointers render on the shared overlay above all game UI and are independent of tutorials (a playing
  tutorial never disturbs them). Backed by a pool of dedicated pointer views separate from the tutorial
  pointer. `TutorialDirector.EnsureOverlay()` is now public so standalone APIs can reach the overlay.

## [0.23.0] — 2026-07-21

### Added
- **Text box show / hide animations.** Each text box now chooses how it animates in and out via an
  `Animation Mode` on its `TutorialTextBox` — **Script** (code/tween-driven; the built-in slide + fade,
  or override `PlayShowScript` / `PlayHideScript` on a custom box), **Legacy** (play named clips on a
  Unity `Animation` component), or **None** (instant). The built-in default box's mode is set on the
  `TextBoxView` component. Legacy gracefully falls back to the script animation when no clip is present.
- **Per-tutorial auto-open control.** A tutorial can override the project-wide "auto-open the graph
  editor on play" setting with its own `Auto Open Editor` mode — `UseDefault`, `Always`, or `Never`
  (in the graph editor's **Tutorial** tab and the graph inspector).
- **Vignette shader shipped in builds.** The overlay's `TutorialKit/UIVignette` shader is now referenced
  by the `TutorialKitSettings` asset (which lives in `Resources`), so it is no longer stripped from a
  player build where it was only reached via `Shader.Find`. The Settings window assigns it by default
  and warns if it is missing.

### Changed
- **Merge pointer** shows only the hand now — the trailing circle/dot indicator was removed.

## [0.22.0] — 2026-07-14

### Added
- **Tutorial settings.** Every `TutorialGraph` now carries a `TutorialSettings` block (`graph.Settings`).
  The graph editor's side panel is now tabbed — **Node** (the selected node, as before) and **Tutorial**
  (these settings) — and the graph asset's inspector shows the same block. `TutorialDirector` enforces
  it for every entry point — trigger, code, or remotely loaded JSON:
  - **Play Mode** — `SingleUse` (plays once, completion saved), `Recurring` (plays every trigger,
    nothing saved), `OncePerSession` (once per app run).
  - **Max Plays** / **Cooldown Seconds** — *Recurring only*; cap the number of plays and enforce a
    minimum real-time gap between them. Both survive a restart.
  - **When Busy** — what happens when a tutorial is triggered while another is running: `Interrupt`
    (previous behaviour), `Ignore`, or `Queue`.
  - **Lock Input While Playing** (+ lock group) and **Pause Game While Playing** (`Time.timeScale = 0`),
    both released when the tutorial ends, however it ends.
  - **Allow Skip** — turn off for a mandatory tutorial; `TutorialHandle.Skip()` then does nothing.
- `TutorialDirector.CanPlay(graph, out var reason)` reports whether a graph's settings currently allow a
  play, plus `GetPlayCount` / `GetTimeSinceLastPlay` / `PlayedThisSession` for the saved history.
- `TutorialHandle.Started` (fires when a queued tutorial actually begins), `IsQueued`, and `CanSkip`.
- The graph inspector shows the saved play history, and **Reset Saved Progress** now clears the
  completion flag, play count, and cooldown together (via `IPersistenceService.ResetTutorial`).

### Changed
- A play is now recorded only when a tutorial **completes or is skipped**; an aborted tutorial no longer
  counts toward Max Plays or the cooldown.
- `TutorialTrigger`'s *only if not completed* gate now defers to the graph's play mode via `CanPlay`.
- `TutorialJson` round-trips the settings block. JSON written before 0.22 loads with defaults.

### Deprecated
- `graph.Repeatable` still works as an alias for `PlayMode != SingleUse`. Existing assets that had the
  flag set migrate to `Recurring` automatically the first time they load. Prefer `graph.Settings.PlayMode`.

## [0.21.0] — 2026-07-10

### Added
- **Repeatable tutorials.** A new **Repeatable** flag on a `TutorialGraph` (`graph.Repeatable`) makes a
  tutorial play *every time* it's triggered instead of once: its completion is never written to
  persistence, and it bypasses the only-once / only-if-not-completed gating in `TutorialDirector.Play`
  and `TutorialTrigger`. Good for recurring hints.

## [0.20.0] — 2026-07-10

### Added
- **Editor authoring API** — `TutorialKit.Editor.TutorialGraphAuthoring` for creating and editing tutorial
  `.asset`s from code (AI tools, generators, tests): `CreateGraph` (with Start/End), `AddNode<T>` /
  `AddNode(id)`, `Connect` / `ConnectFanOut` / `Chain`, `ConnectData`, `GetStart`/`GetEnd`, `Layout`,
  `Save`. Auto-layout was extracted into a view-independent `TutorialGraphLayout` so it runs headless.
- The **AI authoring skill** now has the assistant create/edit tutorial **assets** via that API instead
  of building graphs in throwaway runtime code, so the result stays editable in the graph window.

## [0.19.0] — 2026-07-10

### Changed
- **Animations now go through a swappable backend, and DOTween is optional.** The overlays animate via
  a small `ITutorialTweenRunner` abstraction (`TutorialTween`). Two backends ship: a **built-in,
  dependency-free** runner (the default, UniTask-driven) and a **DOTween adapter**. The package no
  longer hard-depends on DOTween, so **it compiles and runs even if DOTween isn't installed**.
- Pick the backend in **Settings ▸ Animation Backend** (or set `TutorialKitSettings.TweenAdapterId`);
  register your own with `TutorialTween.Register`. Selecting DOTween adds the `TUTORIALKIT_DOTWEEN`
  scripting define that compiles the adapter assembly.

### Fixed
- **DOTween module fragility.** The DOTween adapter uses only DOTween's *core* API (`DOTween.To`), never
  the extension "modules" (`DOFade`/`DOMove`/…), so it no longer requires the DOTween module setup and
  can't break on a partial DOTween install.

## [0.18.0] — 2026-07-10

### Added
- **Export / Import JSON from the graph editor toolbar.** The Manage group gains **Export** (save the
  open tutorial as a `.json`) and **Import** (bring a tutorial `.json` in as a new graph asset and open
  it). Uses the same portable, type-tagged format as remote loading; nodes, edges, data edges and
  positions round-trip.

## [0.17.1] — 2026-07-10

### Fixed
- **Dot-grid background no longer overflows the mesh vertex limit** (it threw
  `ArgumentOutOfRangeException: … exceeds the limit of 65535` and made the editor sluggish). The dots
  are now drawn as a GPU-tiled repeating background image instead of per-dot mesh geometry — no vertex
  cost, still soft and still panning/zooming with the graph.

## [0.17.0] — 2026-07-10

### Changed
- **Softer graph canvas.** Replaced the hard line grid with a subtle **dot grid** that pans and zooms
  with the graph (theme-aware, purely decorative) for a calmer, more modern backdrop.

## [0.16.0] — 2026-07-10

### Changed
- **Multiple End nodes are allowed again.** Branching flows often terminate in more than one place, so
  `End` is back in the Add-Node menu and can be freely added and deleted. A graph still needs at least
  one — the editor adds one only when a graph has none (and no longer merges extras). Start is still the
  single, non-deletable entry.

### Added
- **Branch validation.** Any flow output that leads nowhere (a branch that stops without reaching an
  `End`) marks its node with a ⚠ badge and a tooltip. Re-checked on load and on every edge change.

## [0.15.0] — 2026-07-10

### Changed
- **The End node is now mandatory too** — symmetric with Start. Every graph keeps exactly one End (the
  editor adds one, wiring loose branches to it, and merges any extras); it can't be deleted or
  duplicated and is hidden from the Add-Node menu.
- **Graph toolbar cleanup.** Removed the section captions (Manage/Layout/View/Info) — the toolbar is now
  a single row of grouped buttons with dividers. The layout direction is a **single toggle button** with
  a horizontal/vertical icon that flips the graph and shows the current direction.

## [0.14.0] — 2026-07-10

### Changed
- **Every graph now has a required Start node.** It's the fixed entry point: the editor adds one to any
  graph that lacks it (wired to whatever ran first), and it **can't be deleted or duplicated** and no
  longer appears in the Add-Node menu. The old "Set as Start" right-click action is removed. All example
  graphs (and the code-built samples) now begin with a Start node. Custom nodes can hide from the menu
  too via `[TutorialNode(..., HideInMenu = true)]`.

## [0.13.0] — 2026-07-10

### Added
- **Sectioned graph toolbar.** The editor's top bar is now grouped into labelled sections —
  **Manage** (pick / save / test), **Layout** (auto layout, frame, vertical), **View** (blackboard,
  minimap, attach) and **Info** (status) — with dividers, so controls are easier to find.
- **Edit Script shortcut in the node inspector.** Selecting a custom node shows an **Edit Script**
  button in the inspector header that opens its `.cs` file (like a MonoBehaviour's script field). Nodes
  authored one-per-file (the custom-node convention) resolve automatically.

## [0.12.0] — 2026-07-10

### Added
- **Per-graph layout direction.** Each tutorial remembers its own horizontal/vertical preference (the
  toolbar **Vertical** toggle now saves onto the graph). Graphs left on *Use Default* follow a new
  project-wide **Default graph layout** picker in **Settings ▸ Tools**.

### Changed
- **Pointer no longer covers its target.** The hand/arrow is offset so its finger/arrow *tip* — not the
  sprite centre — sits at the target, leaving a button's label visible; the tap ring still ripples from
  the tip. Tunable via the pointer view's hotspot fields.

### Fixed
- **Tap indicator showed a white square.** The procedural tap ring/dot sprites could be unloaded and
  the `??=` cache handed back the destroyed (Unity-null) sprite, which renders as a white box. Generated
  sprites/textures are now `HideAndDontSave` and the cache rebuilds any that were destroyed.

## [0.11.0] — 2026-07-10

### Added
- **Auto-open the live view.** When a tutorial starts in Play mode, the graph editor now opens (or
  focuses) itself on that tutorial and live-attaches automatically — no need to open the window first.
  Works for asset and code-built graphs (new static `TutorialDirector.AnyStarted` hook). Toggle it in
  **Settings ▸ Tools ▸ "Auto-open the graph editor when a tutorial plays"** (on by default).

## [0.10.0] — 2026-07-10

### Added
- **Settings for default pointer art.** New `TutorialKitSettings` asset (created/edited from
  **Window ▸ TutorialKit ▸ Settings ▸ Default Pointer Art**) exposes the four pointer sprites
  (hand, open, closed, arrow). It's pre-filled with the bundled generic art and lives in a `Resources`
  folder so runtime/builds read it; replace any sprite to re-skin pointers project-wide, or clear one
  to fall back to the bundled default → procedural shape.
- **Vertical graph layout.** A **Vertical** toolbar toggle flows the graph **top → bottom** like the
  VFX Graph editor: flow ports move to the top/bottom of each node (data ports stay on the sides) and
  Auto Layout arranges by the chosen direction (barycenter placement, providers in a left gutter). The
  choice is remembered per-editor.

## [0.9.0] — 2026-07-10

### Added
- **Bundled pointer art.** The hand and arrow pointers now ship real art (CC0, from the Kenney Cursor
  Pack, rasterized to 256px sprites) under `Runtime/Resources/TutorialKit/Pointers` — used by default
  instead of the procedural silhouettes (which remain as a fallback). The **drag** gesture now animates
  an open hand → closed fist → open hand for a clear grab/release read. A game can still override the
  art via the pointer view's `handSprite` / `arrowSprite`. See `Third Party Notices.md` for the license.
- Demo launcher gains a **✋ Pointer gestures** showcase that tours point, tap, swipe, drag, merge and
  the arrow pointer so every pose is visible.

## [0.8.3] — 2026-07-10

### Changed
- **Rewrote Auto Layout** for best-organized graphs. Columns are only widened for a provider lane when
  a column actually has provider nodes feeding it — provider-free graphs (and provider-free columns)
  now use compact, uniform spacing instead of the previous fixed double-width columns that left big
  gaps between plain nodes. Vertical placement uses a barycenter pass (each node centres under its
  flow parents), so linear chains stay aligned, branches spread, and joins re-centre. Provider/target
  nodes are stacked **directly below their consumer** in the reserved lane, ordered to match the
  consumer's input ports — so their data edges always rise up-right to a port and never route back up
  through the flow band or under another node. Verified overlaps=0 with uniform 300px column spacing
  across linear, branching, and multi-target-provider graphs.

## [0.8.2] — 2026-07-10

### Fixed
- **Auto Layout** places provider nodes in a dedicated lane left of their consumer, on free rows, so
  they no longer overlap other nodes (wider flow columns + collision-free row assignment).

## [0.8.1] — 2026-07-10

### Fixed
- **Auto Layout** now places target/value provider nodes (no flow ports) just to the left of the node
  they feed, stacked — instead of dumping them into a trailing column.

## [0.8.0] — 2026-07-10

### Added
- The **vignette's Target input is now multi-capacity** — connect several target provider nodes to cut
  several holes at once (`ResolveTargetInputs` gathers them all). New Grid sample tutorial where a
  custom `GridTileTargetNode` (subclassing `TargetNodeBase`) finds tiles at runtime and three of them
  feed one vignette. Data input ports can declare `Multi`.

## [0.7.1] — 2026-07-10

### Changed
- Redesigned the graph editor's **node inspector**: a colour-coded header banner (the node's accent
  colour) with an auto-contrasting title, a menu-path subtitle, the description, and the fields grouped
  in a card. Node headers use a richer tinted colour.

## [0.7.0] — 2026-07-10

### Added
- **Data ports & target provider nodes**: nodes can now expose typed data ports. `Show Vignette`,
  `Show Pointer`, `Show Text Box`, and `Wait For Input` gained an optional **Target input**; connect a
  target provider node to inject the target. Built-in providers: **Target By Id** and **Target By
  Screen Position**; subclass `TargetNodeBase` to compute a target with game logic. Data edges
  serialize (asset + JSON) and the graph editor renders/validates them (teal ports, type-checked).
  The framework (`TutorialDataPort` + `EvaluatePort`) is generic for future port types.

### Changed
- The overlay requests now carry resolved `ITutorialTarget`s (nodes resolve refs/inputs), decoupling
  the overlay from the target registry.

## [0.6.0] — 2026-07-10

### Added
- **Dynamic targets**: targets are now an `ITutorialTarget` interface. Register providers resolved by
  game logic at runtime — `TutorialTargets.RegisterDynamic(id, () => FindElement())` for elements found
  each frame, or `RegisterRect(id, () => rect)` for an explicit position/size. Highlights and pointers
  follow them. New **Grid** sample: a runtime grid where two tiles are picked by game logic, highlighted
  with a multi-hole vignette, and connected with a merge-pointer hint.

## [0.5.1] — 2026-07-10

### Added
- `Show Vignette` option **Allow Clicks Through Holes** (default on). Turn off to lock the holes too,
  making the highlight purely visual (`HighlightRequest.BlockHoles`).

### Fixed
- Multi-hole vignette: a newly-appearing hole now snaps into place instead of gliding from a stale
  position. The `MultiHighlightTutorial` example is now interactive (instruction box + Next) rather
  than a brief auto-advancing flash.

## [0.5.0] — 2026-07-10

### Added
- **Multi-hole vignette**: one `Show Vignette` node can highlight several targets at once
  (`AdditionalTargets`); the shader cuts up to 8 soft holes. New `MultiHighlightTutorial` example.
- **Blackboard inspector**: flags used by `Set Flag` / `Condition` nodes auto-appear in the blackboard
  panel; each variable's default uses a type-appropriate editor (Bool → checkbox, Int/Float/String →
  their fields); live values still shown in Play.

### Changed
- The pointer is guaranteed to render above the vignette.

## [0.4.1] — 2026-07-10

### Fixed
- **Text-box node field `Id` renamed to `BoxId`** — it hid `TutorialNode.Id`, which corrupted edges
  when building graphs in code (edges from text-box nodes pointed at `"main"`). Example tutorials
  regenerated and now run correctly.
- Demo no longer auto-plays (which fought with the graph editor's Test-in-Play); the demo scene now
  has an on-screen launcher with a button per example.

## [0.4.0] — 2026-07-10

### Added
- **Live debugger**: the graph editor auto-attaches to the running tutorial in Play mode (even
  code-built graphs), pulses the active node, marks the visited node trail and traversed edges,
  shows live blackboard values, and reports status. Toolbar **Attach** toggle.
- Runtime `TutorialDirector.ActiveContext` / `ActiveBlackboard` and a `NodeExited` event.

## [0.3.0] — 2026-07-10

### Added
- **Fan-out ports**: an output port can connect to multiple nodes; those branches run concurrently
  and join (`AddEdge` / recursive fork-join player).
- **Blackboard**: declare typed variables (Bool/Int/Float/String) on a graph in a floating, draggable,
  collapsible panel; defaults seed the run-time blackboard.
- **Node inspector**: right-docked panel to edit the selected node's fields, with tooltips.
- Professional graph styling (stylesheet), colour-chipped slim nodes, icon toolbar buttons,
  draggable/collapsible minimap at the bottom, and Minimap/Blackboard toggles.

### Changed
- Nodes no longer show inline fields (edited in the inspector) for a cleaner canvas.

## [0.2.0] — 2026-07-10

### Added
- **Graph editor**: minimap, node groups, copy/paste & duplicate, full undo/redo, **Auto Layout**,
  animated pulse on the live node during Test-in-Play, and a per-node summary line.
- **Settings window** (Window ▸ TutorialKit ▸ Settings) that installs an **AI authoring skill** whose
  node reference is generated live from the project (custom nodes included).

### Changed
- Vignette now **glides** smoothly between highlight targets instead of snapping.
- Text boxes **slide up** while fading in.

## [0.1.0] — 2026-07-10

Initial release.

### Added
- **Core**: `TutorialGraph` (SerializeReference nodes + edges), `TutorialNode` base, `TutorialPlayer`
  interpreter, `TutorialDirector` composition root, `TutorialHandle` (await / skip / abort), fully
  UniTask + cancellation driven.
- **Adapters**: `IPersistenceService` (PlayerPrefs default), `IInputLockService`,
  `IGameCommandRegistry`, `ITutorialSignalBus`, `ITutorialTargetRegistry`, `IInputProvider` — each
  with a default implementation auto-installed when the game supplies none.
- **Overlay**: soft-edged vignette (SDF circle / rounded-rect hole, click-through hole via
  `ICanvasRaycastFilter`), animated pointer (point / tap / swipe / drag / merge), styled default
  text box with typewriter + wait-for-continue and custom-prefab support. Procedural sprites (no art
  dependency).
- **Nodes**: Start, End, Condition, Mark Checkpoint, Set Flag, Wait Time / Input / Signal,
  Show/Hide Vignette, Show/Hide Pointer, Show/Hide Text Box, Set Input Lock, Game Command, Emit Signal.
- **Components**: `TutorialTrigger` (lifecycle / signal / manual, with conditions), `TutorialTarget`
  (id-based scene resolution for UI & world objects), `TutorialSignalEmitter`.
- **Serialization**: type-tagged JSON (`TutorialJson`) + `RemoteTutorialLoader` (URL / TextAsset /
  string). Custom nodes round-trip automatically.
- **Editor**: GraphView authoring window (browse, edit, save, ▶ test in Play, live node highlight),
  node search create-menu, custom inspectors (graph / trigger / target), Tutorial Browser window.
- **Samples**: Basic Tutorials — demo scene, in-code example graph, example custom node, exported JSON.
