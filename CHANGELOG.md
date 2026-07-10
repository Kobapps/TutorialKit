# Changelog

All notable changes to TutorialKit are documented here.

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
