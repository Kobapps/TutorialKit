# Basic Tutorials sample

This sample shows TutorialKit end-to-end:

- **`TutorialKitDemoController`** builds an example graph in code and plays it on Start:
  text box → vignette highlight on the *Play* button → animated hand pointer → wait for the player
  to tap it → custom `Demo/Confetti` node → closing text box.
- **`DemoConfettiNode`** is an example *custom node* defined in game code (not the package). It shows
  up in the graph editor and the JSON format automatically.
- **`basics.json`** is the same tutorial exported to the remote JSON format. Load it with:

  ```csharp
  var graph = await RemoteTutorialLoader.LoadFromUrlAsync(url, ct);
  TutorialDirector.EnsureExists().Play(graph);
  ```

## Setup

1. Create a UI Canvas with a button. Add a **Tutorial Target** component to the button and set its
   id to `play_button`.
2. Add the **TutorialKitDemoController** component to any GameObject.
3. Press Play.

Open the graph editor via **Window ▸ TutorialKit ▸ Tutorial Graph Editor** and press **▶ Test in Play**
to run authored `.asset` graphs and watch the live node highlight during playback.
