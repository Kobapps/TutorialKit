using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>Discovery info for a concrete <see cref="TutorialNode"/> type.</summary>
    public sealed class NodeTypeInfo
    {
        public Type Type;
        public string TypeId;      // stable id for JSON
        public string MenuPath;    // for the editor create menu
        public string Description;
        public Color Color;
        public bool HasColor;

        public string DisplayName
        {
            get
            {
                int slash = MenuPath != null ? MenuPath.LastIndexOf('/') : -1;
                return slash >= 0 ? MenuPath.Substring(slash + 1) : (MenuPath ?? Type.Name);
            }
        }

        public TutorialNode CreateInstance()
        {
            var node = (TutorialNode)Activator.CreateInstance(Type);
            node.EnsureId();
            return node;
        }
    }

    /// <summary>
    /// Reflection-based registry of all node types, keyed by a stable <c>TypeId</c>. Powers both the
    /// remote JSON (de)serializer and the editor's node create menu. Custom game nodes appear here
    /// automatically once they carry a <see cref="TutorialNodeAttribute"/>.
    /// </summary>
    public static class NodeTypeRegistry
    {
        private static Dictionary<string, NodeTypeInfo> _byId;
        private static Dictionary<Type, NodeTypeInfo> _byType;
        private static List<NodeTypeInfo> _all;

        public static IReadOnlyList<NodeTypeInfo> All { get { EnsureBuilt(); return _all; } }

        public static NodeTypeInfo Get(string typeId)
        {
            EnsureBuilt();
            return typeId != null && _byId.TryGetValue(typeId, out var info) ? info : null;
        }

        public static NodeTypeInfo Get(Type type)
        {
            EnsureBuilt();
            return type != null && _byType.TryGetValue(type, out var info) ? info : null;
        }

        public static string GetTypeId(TutorialNode node)
        {
            if (node == null) return null;
            var info = Get(node.GetType());
            return info != null ? info.TypeId : node.GetType().Name;
        }

        public static TutorialNode Create(string typeId)
        {
            var info = Get(typeId);
            return info?.CreateInstance();
        }

        public static void Rebuild() { _byId = null; EnsureBuilt(); }

        private static void EnsureBuilt()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, NodeTypeInfo>(StringComparer.Ordinal);
            _byType = new Dictionary<Type, NodeTypeInfo>();
            _all = new List<NodeTypeInfo>();

            var nodeBase = typeof(TutorialNode);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !nodeBase.IsAssignableFrom(t)) continue;
                    if (t.GetConstructor(Type.EmptyTypes) == null) continue;

                    var attr = t.GetCustomAttribute<TutorialNodeAttribute>();
                    var info = new NodeTypeInfo
                    {
                        Type = t,
                        TypeId = attr != null && !string.IsNullOrEmpty(attr.TypeId) ? attr.TypeId : t.Name,
                        MenuPath = attr != null ? attr.MenuPath : "Custom/" + t.Name,
                        Description = attr?.Description,
                    };
                    if (attr != null && !string.IsNullOrEmpty(attr.Color) &&
                        ColorUtility.TryParseHtmlString(attr.Color, out var c))
                    {
                        info.Color = c;
                        info.HasColor = true;
                    }

                    if (_byId.ContainsKey(info.TypeId))
                    {
                        Debug.LogWarning($"[TutorialKit] Duplicate node TypeId '{info.TypeId}' ({t.FullName}); skipping.");
                        continue;
                    }
                    _byId[info.TypeId] = info;
                    _byType[t] = info;
                    _all.Add(info);
                }
            }
            _all.Sort((a, b) => string.CompareOrdinal(a.MenuPath, b.MenuPath));
        }
    }
}
