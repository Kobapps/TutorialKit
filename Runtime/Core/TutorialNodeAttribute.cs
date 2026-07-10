using System;

namespace TutorialKit
{
    /// <summary>
    /// Marks a <see cref="TutorialNode"/> subclass so it is discovered by the graph editor's
    /// create menu and by the JSON serializer's type registry.
    /// Custom game nodes only need to add this attribute to appear everywhere.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class TutorialNodeAttribute : Attribute
    {
        /// <summary>Slash-separated path shown in the node create search window, e.g. "Flow/Wait".</summary>
        public string MenuPath { get; }

        /// <summary>Short human description shown as a tooltip / help text.</summary>
        public string Description { get; }

        /// <summary>
        /// Stable identifier used in the remote JSON format. Defaults to the class name.
        /// Keep it stable across renames to avoid breaking published content.
        /// </summary>
        public string TypeId { get; set; }

        /// <summary>Accent colour (hex, e.g. "#3A7BD5") for the node header in the editor.</summary>
        public string Color { get; set; }

        /// <summary>Hide this type from the node-create menu (still registered for serialization). Used
        /// by the built-in Start node, which the editor manages automatically — one per graph.</summary>
        public bool HideInMenu { get; set; }

        public TutorialNodeAttribute(string menuPath, string description = null)
        {
            MenuPath = menuPath;
            Description = description;
        }
    }
}
