using System;

namespace TutorialKit
{
    /// <summary>
    /// A typed data port on a node (distinct from flow ports). Producer nodes expose data OUTPUT
    /// ports; consumer nodes expose data INPUT ports. A data edge carries a value (pulled on demand)
    /// from a producer's output to a consumer's input. <see cref="TypeId"/> is an open string so new
    /// port kinds can be added in the future (currently <see cref="TutorialPortTypes.Target"/>).
    /// </summary>
    [Serializable]
    public struct TutorialDataPort
    {
        public string Name;
        public string TypeId;
        /// <summary>If true (input ports only), the port accepts several connections at once.</summary>
        public bool Multi;

        public TutorialDataPort(string name, string typeId, bool multi = false)
        {
            Name = name;
            TypeId = typeId;
            Multi = multi;
        }
    }

    /// <summary>Known data-port type ids and their editor port-type mapping.</summary>
    public static class TutorialPortTypes
    {
        /// <summary>A target provider (resolves to an <see cref="ITutorialTarget"/>).</summary>
        public const string Target = "target";

        /// <summary>Maps a port TypeId to a System.Type used by the GraphView for connection compatibility.</summary>
        public static Type ToPortType(string typeId)
        {
            switch (typeId)
            {
                case Target: return typeof(ITutorialTarget);
                default: return typeof(object);
            }
        }
    }

    /// <summary>A directed connection between a producer's data output port and a consumer's data input port.</summary>
    [Serializable]
    public struct TutorialDataEdge : IEquatable<TutorialDataEdge>
    {
        public string FromNodeId;
        public string FromPort;
        public string ToNodeId;
        public string ToPort;

        public TutorialDataEdge(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            FromNodeId = fromNodeId;
            FromPort = fromPort;
            ToNodeId = toNodeId;
            ToPort = toPort;
        }

        public bool Equals(TutorialDataEdge o) =>
            FromNodeId == o.FromNodeId && FromPort == o.FromPort && ToNodeId == o.ToNodeId && ToPort == o.ToPort;

        public override bool Equals(object obj) => obj is TutorialDataEdge e && Equals(e);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = FromNodeId?.GetHashCode() ?? 0;
                h = (h * 397) ^ (FromPort?.GetHashCode() ?? 0);
                h = (h * 397) ^ (ToNodeId?.GetHashCode() ?? 0);
                h = (h * 397) ^ (ToPort?.GetHashCode() ?? 0);
                return h;
            }
        }
    }
}
