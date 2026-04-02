using System.Collections.Generic;
using CityBuilder.Infrastructure.Roads;
using Unity.Mathematics;
using UnityEngine;

#nullable enable
namespace CityBuilder.Rendering.Roads
{
    /// <summary>
    /// Builds the mesh that fills the gap at a road intersection.
    ///
    /// Each road arm is extruded only up to a clip point short of the node centre.
    /// This builder covers the remaining polygon between all arm openings so
    /// the intersection surface is seamless.
    ///
    /// The approach:
    ///   1. Decide whether an intersection mesh is actually needed (see below).
    ///   2. Compute a clip t for every segment at this node based on road half-width.
    ///   3. Evaluate the two outer edge vertices of each arm at its clip t.
    ///   4. Sort all edge vertices clockwise around the node centre.
    ///   5. Fan-triangulate the resulting convex polygon from its centroid.
    ///
    /// When is an intersection mesh needed?
    ///   • Three or more arms always need one.
    ///   • Two arms need one only when they meet at a noticeable angle (> ~12°),
    ///     i.e. they are NOT a straight pass-through.  A straight continuation
    ///     can simply abut the two segment meshes without a gap.
    ///
    /// SegmentClip records are always emitted so the caller can update
    /// TrimmedStartT / TrimmedEndT on every affected segment and trigger
    /// segment mesh rebuilds – regardless of whether a mesh was produced.
    /// </summary>
    public static class IntersectionMeshBuilder
    {
        /// <summary>
        /// Updated clipping parameters for one segment at this node.
        /// Only the end facing this node is touched; the far end is left unchanged.
        /// </summary>
        public readonly struct SegmentClip
        {
            public readonly int   SegmentId;
            public readonly float TrimmedStartT;
            public readonly float TrimmedEndT;

            public SegmentClip(int id, float start, float end)
            {
                SegmentId     = id;
                TrimmedStartT = start;
                TrimmedEndT   = end;
            }
        }

        // Clip at most this fraction of a segment's total length to protect
        // very short segments from being clipped all the way through.
        private const float MaxClipFraction = 0.4f;

        // Two segments whose outward tangents are more anti-parallel than this
        // threshold form a straight road – no intersection mesh is produced.
        private const float StraightDotThreshold = -0.978f;  // ≈ 168°

        // ─────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the intersection mesh for a node and outputs per-segment clip values.
        ///
        /// Returns null when no visual gap needs filling (endpoint or straight continuation).
        /// clipUpdates is populated in every case so the caller can reset clip t values.
        /// </summary>
        public static RoadMeshData? Build(
            RoadNode                              node,
            IReadOnlyDictionary<int, RoadSegment> segments,
            IReadOnlyDictionary<int, RoadNode>    nodes,
            RoadProfile                           profile,
            out List<SegmentClip>                 clipUpdates)
        {
            clipUpdates = new List<SegmentClip>(node.OrderedSegmentIds.Count);

            int armCount = node.OrderedSegmentIds.Count;

            if (armCount < 2 || armCount == 2 && IsStraightPassThrough(node, segments, nodes))
            {
                EmitResetClips(node, segments, clipUpdates);
                return null;
            }

            float halfWidth = profile.TotalWidth * 0.5f;

            // ── Compute clip t and edge vertices for each arm ─────────────────
            List<float3> allEdgePoints = new List<float3>(armCount * 2);

            foreach (int segId in node.OrderedSegmentIds)
            {
                if (!segments.TryGetValue(segId,    out RoadSegment seg))    continue;
                if (!nodes.TryGetValue(seg.NodeA,   out RoadNode    nodeA))  continue;
                if (!nodes.TryGetValue(seg.NodeB,   out RoadNode    nodeB))  continue;

                float clipRadius = math.min(halfWidth, seg.TotalArcLength * MaxClipFraction);
                bool  isNodeA    = seg.NodeA == node.Id;

                float clipT;
                float newStart = seg.TrimmedStartT;
                float newEnd   = seg.TrimmedEndT;

                if (isNodeA)
                {
                    clipT    = BezierCurve.ArcLengthToT(seg.ArcLengthLUT, clipRadius);
                    newStart = clipT;
                }
                else
                {
                    clipT  = BezierCurve.ArcLengthToT(seg.ArcLengthLUT, seg.TotalArcLength - clipRadius);
                    newEnd = clipT;
                }

                clipUpdates.Add(new SegmentClip(segId, newStart, newEnd));

                // Edge vertices at the clip point (perpendicular to outward tangent)
                float3 clipPos        = BezierCurve.Evaluate(nodeA.Position, seg.ControlPointA, seg.ControlPointB, nodeB.Position, clipT);
                float3 tangent        = BezierCurve.EvaluateTangent(nodeA.Position, seg.ControlPointA, seg.ControlPointB, nodeB.Position, clipT);
                float3 outwardTangent = isNodeA ? tangent : -tangent;
                float3 right          = math.normalizesafe(math.cross(outwardTangent, new float3(0f, 1f, 0f)), new float3(1f, 0f, 0f));

                // Two outer edge points of this arm opening at the clip line
                allEdgePoints.Add(clipPos - right * halfWidth);
                allEdgePoints.Add(clipPos + right * halfWidth);
            }

            if (allEdgePoints.Count < 3)
                return null;

            // ── Sort edge points clockwise around the node centre ─────────────
            float3 nodeCenter = node.Position;
            allEdgePoints.Sort((float3 a, float3 b) =>
                ClockwiseAngleFromNorth(a, nodeCenter).CompareTo(ClockwiseAngleFromNorth(b, nodeCenter)));

            // ── Compute centroid ──────────────────────────────────────────────
            float3 centroid = float3.zero;
            foreach (float3 p in allEdgePoints)
                centroid += p;
            centroid /= allEdgePoints.Count;

            // ── Fan-triangulate from centroid ─────────────────────────────────
            // Vertices in CW order from above → triangle (centroid, Vi, Vi+1) gives
            // a normal pointing upward (matches road surface orientation).
            return BuildFanMeshLocal(allEdgePoints, centroid, nodeCenter);
        }

        // ─────────────────────────────────────────────────────────
        //  Mesh construction
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Produces a flat polygon mesh by connecting each consecutive pair of
        /// boundary vertices to the centroid.
        /// </summary>
        private static RoadMeshData BuildFanMeshLocal(
            List<float3> boundaryWorld,
            float3 centroidWorld,
            float3 originWorld)
        {
            int n = boundaryWorld.Count;

            Vector3[] vertices = new Vector3[n + 1];
            Vector3[] normals  = new Vector3[n + 1];
            Vector2[] uvs      = new Vector2[n + 1];
            int[] tris         = new int[n * 3];

            float3 centroidLocal = centroidWorld - originWorld;
            vertices[n] = new Vector3(centroidLocal.x, centroidLocal.y, centroidLocal.z);
            normals[n]  = Vector3.up;
            uvs[n]      = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < n; i++)
            {
                float3 pWorld = boundaryWorld[i];
                float3 pLocal = pWorld - originWorld;

                vertices[i] = new Vector3(pLocal.x, pLocal.y, pLocal.z);
                normals[i]  = Vector3.up;

                float angle = ClockwiseAngleFromNorth(pWorld, centroidWorld);
                uvs[i] = new Vector2(angle / (math.PI * 2f), 0f);

                int triBase       = i * 3;
                tris[triBase]     = n;
                tris[triBase + 1] = i;
                tris[triBase + 2] = (i + 1) % n;
            }

            return new RoadMeshData(vertices, normals, uvs, new [] { tris });
        }

        // ─────────────────────────────────────────────────────────
        //  Geometry helpers
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Angle of point relative to centre, measured clockwise from +Z (North),
        /// normalised to [0, 2π]. Used to sort intersection boundary vertices CW.
        /// </summary>
        private static float ClockwiseAngleFromNorth(float3 point, float3 centre)
        {
            float dx    = point.x - centre.x;
            float dz    = point.z - centre.z;
            float angle = math.atan2(dx, dz);   // CW from +Z
            return angle < 0f ? angle + math.PI * 2f : angle;
        }

        /// <summary>
        /// Returns true when exactly two arms meet at a node and their outward
        /// tangents are nearly anti-parallel – meaning the road continues straight
        /// through the node without a visible bend or kink.
        /// </summary>
        private static bool IsStraightPassThrough(
            RoadNode                              node,
            IReadOnlyDictionary<int, RoadSegment> segments,
            IReadOnlyDictionary<int, RoadNode>    nodes)
        {
            int seg0Id = node.OrderedSegmentIds[0];
            int seg1Id = node.OrderedSegmentIds[1];

            if (!segments.TryGetValue(seg0Id, out RoadSegment seg0)) return false;
            if (!segments.TryGetValue(seg1Id, out RoadSegment seg1)) return false;
            if (!nodes.TryGetValue(seg0.NodeA, out RoadNode s0A))    return false;
            if (!nodes.TryGetValue(seg0.NodeB, out RoadNode s0B))    return false;
            if (!nodes.TryGetValue(seg1.NodeA, out RoadNode s1A))    return false;
            if (!nodes.TryGetValue(seg1.NodeB, out RoadNode s1B))    return false;

            // Outward tangent at the end that touches this node
            float3 tan0 = seg0.NodeA == node.Id
                ?  BezierCurve.EvaluateTangent(s0A.Position, seg0.ControlPointA, seg0.ControlPointB, s0B.Position, 0f)
                : -BezierCurve.EvaluateTangent(s0A.Position, seg0.ControlPointA, seg0.ControlPointB, s0B.Position, 1f);

            float3 tan1 = seg1.NodeA == node.Id
                ?  BezierCurve.EvaluateTangent(s1A.Position, seg1.ControlPointA, seg1.ControlPointB, s1B.Position, 0f)
                : -BezierCurve.EvaluateTangent(s1A.Position, seg1.ControlPointA, seg1.ControlPointB, s1B.Position, 1f);

            return math.dot(tan0, tan1) < StraightDotThreshold;
        }

        /// <summary>
        /// Emits clip values that reset both trimmed ends at this node to [0, 1]
        /// so no gap is cut in the road mesh at a straight pass-through or endpoint.
        /// Only the end facing this node is reset; the far end is preserved.
        /// </summary>
        private static void EmitResetClips(
            RoadNode                              node,
            IReadOnlyDictionary<int, RoadSegment> segments,
            List<SegmentClip>                     clipUpdates)
        {
            foreach (int segId in node.SegmentIds)
            {
                if (!segments.TryGetValue(segId, out RoadSegment seg))
                    continue;

                float newStart = seg.NodeA == node.Id ? 0f : seg.TrimmedStartT;
                float newEnd   = seg.NodeB == node.Id ? 1f : seg.TrimmedEndT;
                clipUpdates.Add(new SegmentClip(segId, newStart, newEnd));
            }
        }
    }
}
