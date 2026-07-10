using System;
using System.Collections.Generic;

namespace TutorialKit
{
    /// <summary>Editor-only grouping of nodes on the graph canvas (title + member node ids).</summary>
    [Serializable]
    public sealed class TutorialGroupData
    {
        public string Title = "Group";
        public List<string> NodeIds = new List<string>();
    }
}
