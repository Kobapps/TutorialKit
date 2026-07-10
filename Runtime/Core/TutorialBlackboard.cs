using System;

namespace TutorialKit
{
    public enum BlackboardVarType
    {
        Bool = 0,
        Int = 1,
        Float = 2,
        String = 3,
    }

    /// <summary>
    /// A design-time blackboard variable declared on a <see cref="TutorialGraph"/>. Its default value
    /// seeds the run-time blackboard (<see cref="TutorialRunContext.Blackboard"/>) when the tutorial
    /// starts; nodes read/write it by <see cref="Key"/> (e.g. Set Flag / Condition).
    /// </summary>
    [Serializable]
    public sealed class TutorialBlackboardVar
    {
        public string Key = "var";
        public BlackboardVarType Type = BlackboardVarType.Bool;
        public string DefaultValue = "";

        public object ToValue()
        {
            switch (Type)
            {
                case BlackboardVarType.Bool:
                    return DefaultValue == "1" || string.Equals(DefaultValue, "true", StringComparison.OrdinalIgnoreCase);
                case BlackboardVarType.Int:
                    int.TryParse(DefaultValue, out var i);
                    return i;
                case BlackboardVarType.Float:
                    float.TryParse(DefaultValue, out var f);
                    return f;
                default:
                    return DefaultValue ?? "";
            }
        }
    }
}
