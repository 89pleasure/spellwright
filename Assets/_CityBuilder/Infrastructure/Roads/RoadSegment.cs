using Unity.Mathematics;

#nullable enable
namespace CityBuilder.Infrastructure.Roads
{
    /// <summary>
    /// One road section between two nodes in the city road network.
    ///
    /// The physical path of the road follows a cubic Bézier curve:
    ///   P0 (NodeA position) → P1 (ControlPointA) → P2 (ControlPointB) → P3 (NodeB position)
    ///
    /// For straight roads P1 and P2 sit at ⅓ and ⅔ along the straight line,
    /// so the Bézier degenerates to a straight line without any special-casing.
    ///
    /// Mesh clipping (TrimmedStartT / TrimmedEndT):
    ///   The road mesh is only extruded between these two parameters.
    ///   The gap at each clipped end is filled by the intersection mesh that belongs
    ///   to the adjacent node – keeping road and intersection meshes fully independent.
    /// </summary>
    public class RoadSegment
    {
        public readonly int   Id;
        public readonly int   NodeA;
        public          int   NodeB;       // Non-readonly: changes when this segment is split at a new T-junction
        public readonly int   Lanes;
        public readonly float SpeedLimit;  // km/h
        public          bool  IsBlocked;

        // ── Bézier curve shape ────────────────────────────────────────────────
        // P0 = NodeA.Position and P3 = NodeB.Position live in the graph;
        // only the two inner handles are stored here.
        public float3 ControlPointA;   // P1 – first inner handle
        public float3 ControlPointB;   // P2 – second inner handle

        // ── Arc-length LUT ────────────────────────────────────────────────────
        // Precomputed once at construction and rebuilt after any split.
        // Translates a real-world distance along the road (metres) into the curve
        // parameter t, so mesh cross-sections and parcel intervals are evenly spaced
        // even on tight curves.
        public float[] ArcLengthLUT   { get; private set; }
        public float   TotalArcLength { get; private set; }

        // ── Mesh clipping ─────────────────────────────────────────────────────
        // The road mesh is generated only between these curve parameters.
        // Set by the intersection builder after it determines how far each road
        // arm must be pulled back to leave room for the crossing polygon.
        public float TrimmedStartT = 0f;
        public float TrimmedEndT   = 1f;

        // ── Split tracking ────────────────────────────────────────────────────
        // -1  → original segment, not the product of a split.
        // ≥ 0 → this segment was created when segment ParentSegmentId was divided
        //        at a new T-junction. Useful for undo and debugging.
        public int ParentSegmentId = -1;

        public RoadSegment(
            int    id,
            int    nodeA,
            int    nodeB,
            float3 nodeAPos,
            float3 nodeBPos,
            float3 controlPointA,
            float3 controlPointB,
            int    lanes,
            float  speedLimit)
        {
            Id            = id;
            NodeA         = nodeA;
            NodeB         = nodeB;
            Lanes         = lanes;
            SpeedLimit    = speedLimit;
            ControlPointA = controlPointA;
            ControlPointB = controlPointB;
            ArcLengthLUT  = System.Array.Empty<float>();
            RebuildArcLengthLUT(nodeAPos, nodeBPos);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Recalculates the arc-length LUT after the curve shape changes.
        /// Must be called by RoadGraph whenever control points or node positions change –
        /// for example after this segment is shortened by a split operation.
        /// </summary>
        public void RebuildArcLengthLUT(float3 nodeAPos, float3 nodeBPos)
        {
            ArcLengthLUT   = BezierCurve.BuildArcLengthLUT(nodeAPos, ControlPointA, ControlPointB, nodeBPos);
            TotalArcLength = ArcLengthLUT[ArcLengthLUT.Length - 1];
        }

        /// <summary>Returns the ID of the node at the other end of this segment.</summary>
        public int OtherNode(int nodeId) => nodeId == NodeA ? NodeB : NodeA;
    }
}
