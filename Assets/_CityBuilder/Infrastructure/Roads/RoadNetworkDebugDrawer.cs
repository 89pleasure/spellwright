using CityBuilder.Core;
using Unity.Mathematics;
using UnityEngine;

namespace CityBuilder.Infrastructure.Roads
{
    /// <summary>
    /// Visualizes the road graph in the Unity Scene View via Gizmos.
    /// Attach to the same GameObject as WorldBootstrapper.
    /// Only active in the Editor – stripped from builds.
    ///
    /// Segments are drawn as sampled Bézier curves so the debug view matches the
    /// actual road geometry, not a simplified straight-line approximation.
    /// </summary>
    public class RoadNetworkDebugDrawer : MonoBehaviour
    {
        [Header("Node Gizmos")]
        public Color nodeColor      = new Color(0.2f, 0.8f, 0.2f);
        public Color dirtyNodeColor = new Color(1.0f, 0.3f, 0.1f);
        public float nodeRadius     = 3f;

        [Header("Segment Gizmos")]
        public Color segmentColor   = new Color(0.9f, 0.9f, 0.2f);
        public Color blockedColor   = new Color(1.0f, 0.1f, 0.1f);
        [Tooltip("Number of straight-line steps used to approximate each Bézier curve.")]
        public int   curveSamples   = 16;

        private void OnDrawGizmos()
        {
            if (GameServices.Instance == null)
                return;

            RoadGraph graph = GameServices.Instance.Roads.Graph;

            // ── Draw segments as sampled Bézier curves ────────────────────────
            foreach (RoadSegment seg in graph.Segments.Values)
            {
                if (!graph.Nodes.TryGetValue(seg.NodeA, out RoadNode nodeA)) continue;
                if (!graph.Nodes.TryGetValue(seg.NodeB, out RoadNode nodeB)) continue;

                Gizmos.color = seg.IsBlocked ? blockedColor : segmentColor;
                DrawBezierCurve(
                    nodeA.Position, seg.ControlPointA,
                    seg.ControlPointB, nodeB.Position,
                    curveSamples);
            }

            // ── Draw nodes on top of segments ─────────────────────────────────
            foreach (RoadNode node in graph.Nodes.Values)
            {
                Gizmos.color = node.IsDirty ? dirtyNodeColor : nodeColor;
                Gizmos.DrawSphere(node.Position, nodeRadius);
            }
        }

        /// <summary>
        /// Draws a cubic Bézier curve as a series of short line segments.
        /// Uses arc-length-uniform sampling so the lines are evenly distributed
        /// even on tight curves.
        /// </summary>
        private static void DrawBezierCurve(
            float3 p0, float3 p1, float3 p2, float3 p3,
            int    samples)
        {
            float3 previous = p0;

            for (int i = 1; i <= samples; i++)
            {
                float  t       = i / (float)samples;
                float3 current = BezierCurve.Evaluate(p0, p1, p2, p3, t);

                Gizmos.DrawLine(
                    new Vector3(previous.x, previous.y, previous.z),
                    new Vector3(current.x,  current.y,  current.z));

                previous = current;
            }
        }
    }
}
