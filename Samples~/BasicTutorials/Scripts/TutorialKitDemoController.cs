using Cysharp.Threading.Tasks;
using TutorialKit;
using UnityEngine;

namespace TutorialKitDemo
{
    /// <summary>
    /// Builds an example tutorial graph in code and plays it on start. Demonstrates the full loop:
    /// text box → vignette highlight → animated pointer → wait-for-tap → custom command → done.
    /// Targets are resolved by id from <see cref="TutorialTarget"/> components in the scene.
    /// </summary>
    public sealed class TutorialKitDemoController : MonoBehaviour
    {
        [SerializeField] private bool playOnStart = true;

        private void Start()
        {
            var dir = TutorialDirector.EnsureExists();

            // A game command the tutorial can invoke and await.
            dir.Commands.Register("demo.confetti", ctx =>
                Debug.Log($"[TutorialKitDemo] 🎉 Confetti burst x{ctx.Argument}!"));

            if (playOnStart)
                dir.Play(BuildDemoGraph(), force: true);
        }

        public static TutorialGraph BuildDemoGraph()
        {
            var g = ScriptableObject.CreateInstance<TutorialGraph>();
            g.name = "DemoBasics";

            var welcome = new ShowTextBoxNode
            {
                Title = "Welcome to TutorialKit",
                Body = "This whole flow was authored as a graph. Tap Next to begin.",
                Placement = TextBoxPlacement.Top,
                WaitForDismiss = true,
                ShowContinueButton = true,
                ContinueLabel = "Next",
            };
            var vignette = new ShowVignetteNode
            {
                Target = new TutorialTargetRef("play_button"),
                Shape = HighlightShape.Circle,
                Softness = 0.25f,
                Padding = 20f,
            };
            var pointer = new ShowPointerNode
            {
                Kind = PointerKind.Hand,
                Gesture = PointerGesture.Tap,
                Target = new TutorialTargetRef("play_button"),
            };
            var instruct = new ShowTextBoxNode
            {
                Body = "Now tap the highlighted PLAY button.",
                Placement = TextBoxPlacement.Bottom,
                WaitForDismiss = false,
                ShowContinueButton = false,
            };
            var waitTap = new WaitInputNode
            {
                Kind = WaitInputKind.TapOnTarget,
                Target = new TutorialTargetRef("play_button"),
            };
            var hideVig = new HideVignetteNode();
            var hidePtr = new HidePointerNode();
            var hideTxt = new HideTextBoxNode { Id = "main" };
            var confetti = new DemoConfettiNode { burst = 40 };
            var done = new ShowTextBoxNode
            {
                Title = "Nice work!",
                Body = "You finished the sample tutorial. 🎉",
                Placement = TextBoxPlacement.Center,
                WaitForDismiss = true,
            };
            var end = new EndNode();

            Chain(g, welcome, vignette, pointer, instruct, waitTap, hideVig, hidePtr, hideTxt, confetti, done, end);
            g.EntryNodeId = welcome.Id;
            return g;
        }

        private static void Chain(TutorialGraph g, params TutorialNode[] seq)
        {
            foreach (var n in seq) { n.EnsureId(); g.AddNode(n); }
            for (int i = 0; i < seq.Length - 1; i++)
                g.SetEdge(seq[i].Id, TutorialNode.OutPort, seq[i + 1].Id);
        }
    }
}
