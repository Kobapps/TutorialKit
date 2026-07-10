using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Convenience component for emitting tutorial signals from game code, UnityEvents, or UI
    /// Button.onClick. Wait-For-Signal nodes and OnSignal triggers react to these.
    /// </summary>
    [AddComponentMenu("TutorialKit/Tutorial Signal Emitter")]
    public class TutorialSignalEmitter : MonoBehaviour
    {
        [Tooltip("Default signal id emitted by the parameterless Emit().")]
        [SerializeField] private string signalId;

        /// <summary>Emits the configured <see cref="signalId"/>. Hook this to a Button/UnityEvent.</summary>
        public void Emit()
        {
            if (!string.IsNullOrEmpty(signalId))
                TutorialDirector.EnsureExists().Signals.Emit(signalId);
        }

        /// <summary>Emits an explicit signal id.</summary>
        public void Emit(string id)
        {
            if (!string.IsNullOrEmpty(id))
                TutorialDirector.EnsureExists().Signals.Emit(id);
        }
    }
}
