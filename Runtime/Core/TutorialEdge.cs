using System;

namespace TutorialKit
{
    /// <summary>
    /// A directed connection from a node's output port to another node's input.
    /// The player follows the edge whose <see cref="FromPort"/> matches the port name
    /// returned by the source node's <c>ExecuteAsync</c>.
    /// </summary>
    [Serializable]
    public struct TutorialEdge : IEquatable<TutorialEdge>
    {
        public string FromNodeId;
        public string FromPort;
        public string ToNodeId;

        public TutorialEdge(string fromNodeId, string fromPort, string toNodeId)
        {
            FromNodeId = fromNodeId;
            FromPort = fromPort;
            ToNodeId = toNodeId;
        }

        public bool Equals(TutorialEdge other) =>
            FromNodeId == other.FromNodeId && FromPort == other.FromPort && ToNodeId == other.ToNodeId;

        public override bool Equals(object obj) => obj is TutorialEdge e && Equals(e);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = FromNodeId?.GetHashCode() ?? 0;
                h = (h * 397) ^ (FromPort?.GetHashCode() ?? 0);
                h = (h * 397) ^ (ToNodeId?.GetHashCode() ?? 0);
                return h;
            }
        }
    }
}
