using System.Collections.Generic;
using Unity.Mathematics;

namespace CityBuilder.Infrastructure.Roads
{
    /// <summary>
    /// A point in the road network where one or more road segments meet.
    ///
    /// Two segments: simple waypoint or road endpoint.
    /// Three or more segments: road intersection requiring its own crossing mesh.
    ///
    /// OrderedSegmentIds is maintained by RoadGraph and is the key to two systems:
    ///
    ///   Intersection mesh builder
    ///     Needs segments in clockwise angular order to stitch the crossing polygon
    ///     correctly – otherwise triangles wind the wrong way and face downward.
    ///
    ///   Parcel generator
    ///     Traverses the road network face by face by always taking the next segment
    ///     clockwise at each node. Each closed traversal outlines one city block.
    /// </summary>
    public class RoadNode
    {
        public readonly int Id;
        public float3 Position;

        /// <summary>
        /// All segment IDs that touch this node.
        /// Unordered – used for pathfinding where angular order is irrelevant.
        /// </summary>
        public readonly List<int> SegmentIds = new();

        /// <summary>
        /// Same segment IDs as SegmentIds, sorted clockwise from north in the XZ plane.
        /// Rebuilt automatically by RoadGraph after every add or remove at this node.
        /// </summary>
        public readonly List<int> OrderedSegmentIds = new();

        /// <summary>
        /// Set by the pathfinding system when a road change makes cached citizen
        /// routes through this node potentially invalid.
        /// </summary>
        public bool IsDirty;

        public RoadNode(int id, float3 position)
        {
            Id       = id;
            Position = position;
        }
    }
}
