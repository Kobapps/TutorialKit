using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// A gating condition for a <see cref="TutorialTrigger"/>. Implement your own by subclassing
    /// this ScriptableObject. Built-ins cover the common persistence-based cases.
    /// </summary>
    public abstract class TutorialCondition : ScriptableObject
    {
        public abstract bool IsSatisfied(TutorialDirector director);
    }

    [CreateAssetMenu(menuName = "TutorialKit/Conditions/Tutorial Not Completed")]
    public sealed class TutorialNotCompletedCondition : TutorialCondition
    {
        [Tooltip("Tutorial id to check. Empty = the trigger's own graph (handled by the trigger).")]
        public string tutorialId;

        public override bool IsSatisfied(TutorialDirector director)
        {
            if (string.IsNullOrEmpty(tutorialId)) return true;
            return !director.Persistence.IsTutorialCompleted(tutorialId);
        }
    }

    [CreateAssetMenu(menuName = "TutorialKit/Conditions/Tutorial Completed")]
    public sealed class TutorialCompletedCondition : TutorialCondition
    {
        public string tutorialId;
        public override bool IsSatisfied(TutorialDirector director) =>
            !string.IsNullOrEmpty(tutorialId) && director.Persistence.IsTutorialCompleted(tutorialId);
    }

    [CreateAssetMenu(menuName = "TutorialKit/Conditions/Persistence Flag")]
    public sealed class PersistenceFlagCondition : TutorialCondition
    {
        public string key;
        public string expectedValue = "1";
        public override bool IsSatisfied(TutorialDirector director) =>
            director.Persistence.GetValue(key, null) == expectedValue;
    }
}
