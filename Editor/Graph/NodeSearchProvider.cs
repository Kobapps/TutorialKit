using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace TutorialKit.Editor
{
    /// <summary>Populates the node-creation search window from <see cref="NodeTypeRegistry"/>.</summary>
    public sealed class NodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private TutorialGraphView _graphView;
        private EditorWindowHost _host;

        public interface EditorWindowHost
        {
            Vector2 ScreenToGraphPosition(Vector2 screenPosition);
        }

        public void Configure(TutorialGraphView graphView, EditorWindowHost host)
        {
            _graphView = graphView;
            _host = host;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Add Node"), 0),
            };

            var groups = new HashSet<string>();
            foreach (var info in NodeTypeRegistry.All)
            {
                if (info.HideInMenu) continue; // e.g. the Start node — one per graph, managed automatically
                string path = info.MenuPath ?? info.Type.Name;
                var segments = path.Split('/');

                // Emit group headers as needed.
                string accum = "";
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    accum += (i > 0 ? "/" : "") + segments[i];
                    if (groups.Add(accum))
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(segments[i]), i + 1));
                }

                var leaf = new SearchTreeEntry(new GUIContent(segments[segments.Length - 1]))
                {
                    level = segments.Length,
                    userData = info,
                };
                tree.Add(leaf);
            }
            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is NodeTypeInfo info && _graphView != null)
            {
                var pos = _host != null ? _host.ScreenToGraphPosition(context.screenMousePosition) : Vector2.zero;
                _graphView.CreateNode(info, pos);
                return true;
            }
            return false;
        }
    }
}
