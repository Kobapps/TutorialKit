using System;
using TutorialKit;
using UnityEngine;

namespace TutorialKitDemo
{
    /// <summary>
    /// Example CUSTOM target provider node. Subclasses <see cref="TargetNodeBase"/> and uses game logic
    /// (<see cref="TutorialGridDemo.FindTileByIndex"/>) to output a grid tile that doesn't exist at author
    /// time. Wire several of these into a Show Vignette node's (multi) Target input to highlight several
    /// runtime-found tiles at once.
    /// </summary>
    [Serializable]
    [TutorialNode("Grid Demo/Tile Target", "Outputs a grid tile found by index at runtime (game logic).", Color = "#00897B")]
    public sealed class GridTileTargetNode : TargetNodeBase
    {
        [Tooltip("Which grid tile to target (creation-order index).")]
        public int TileIndex = 0;

        public override string DisplayName => "Tile Target";
        public override string GetSummary(TutorialGraph graph) => "tile #" + TileIndex;

        protected override ITutorialTarget ResolveTarget(TutorialRunContext ctx)
        {
            int idx = TileIndex;
            // Dynamic: re-find the tile each frame so the highlight follows the moving grid.
            return new DynamicTutorialTarget(() => TutorialGridDemo.FindTileByIndex(idx));
        }
    }
}
