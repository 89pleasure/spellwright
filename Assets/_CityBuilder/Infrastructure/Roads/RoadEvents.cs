using CityBuilder.Core.EventBus;

namespace CityBuilder.Infrastructure.Roads
{
    /// <summary>
    /// Fired when a new road segment is successfully placed in the world.
    /// Listeners: Parcel generation, power/water routing, traffic simulation.
    /// </summary>
    public readonly struct RoadBuiltEvent : ISimulationEvent
    {
        public readonly int SegmentId;
        public readonly int NodeAId;
        public readonly int NodeBId;
        public float GameTime { get; }
        public int CascadeDepth { get; }

        public RoadBuiltEvent(int segmentId, int nodeAId, int nodeBId, float gameTime, int cascadeDepth = 0)
        {
            SegmentId = segmentId;
            NodeAId = nodeAId;
            NodeBId = nodeBId;
            GameTime = gameTime;
            CascadeDepth = cascadeDepth;
        }
    }

    /// <summary>
    /// Fired when a road segment is removed.
    /// Listeners: Parcel invalidation, power/water re-routing, citizen dirty-flag propagation.
    /// </summary>
    public readonly struct RoadDemolishedEvent : ISimulationEvent
    {
        public readonly int SegmentId;
        public readonly int NodeAId;
        public readonly int NodeBId;
        public float GameTime { get; }
        public int CascadeDepth { get; }

        public RoadDemolishedEvent(int segmentId, int nodeAId, int nodeBId, float gameTime, int cascadeDepth = 0)
        {
            SegmentId = segmentId;
            NodeAId = nodeAId;
            NodeBId = nodeBId;
            GameTime = gameTime;
            CascadeDepth = cascadeDepth;
        }
    }
}
