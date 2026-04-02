using UnityEngine;

namespace CityBuilder.Rendering.Roads
{
    /// <summary>
    /// Plain vertex data for one road segment's mesh.
    /// Produced by RoadMeshBuilder; consumed by RoadRenderer to populate a Unity Mesh.
    ///
    /// Kept as separate arrays to match Unity's Mesh.SetVertices / SetTriangles / SetUVs API
    /// without any additional conversion step in the renderer.
    ///
    /// Triangles is a jagged array – one int[] per material submesh, indexed by
    /// the MaterialIndex values in the RoadProfile. The renderer sets mesh.subMeshCount
    /// to Triangles.Length and calls SetTriangles(Triangles[i], i) for each slot.
    /// </summary>
    public sealed class RoadMeshData
    {
        public readonly Vector3[] Vertices;
        public readonly Vector3[] Normals;
        public readonly Vector2[] UVs;
        public readonly int[][]   Triangles;   // [submeshIndex][triangleIndex]

        public RoadMeshData(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[][] triangles)
        {
            Vertices  = vertices;
            Normals   = normals;
            UVs       = uvs;
            Triangles = triangles;
        }
    }
}
