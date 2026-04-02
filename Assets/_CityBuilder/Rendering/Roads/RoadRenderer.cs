using CityBuilder.Core;
using CityBuilder.Core.EventBus;
using CityBuilder.Infrastructure.Roads;
using Unity.Mathematics;
using UnityEngine;

#nullable enable
namespace CityBuilder.Rendering.Roads
{
    /// <summary>
    /// Listens to road events and maintains a matching set of GameObjects with
    /// procedurally generated road meshes.
    ///
    /// On RoadBuiltEvent:     build mesh via RoadMeshBuilder → spawn GameObject
    /// On RoadDemolishedEvent: destroy the matching GameObject
    ///
    /// The MeshRegistry handles the segment-ID ↔ GameObject mapping and provides
    /// highlight support for the demolish tool.
    ///
    /// Assign the RoadProfile asset and at least one Material in the Inspector.
    /// The materials array must have one entry per MaterialIndex used in the profile.
    /// </summary>
    public class RoadRenderer : MonoBehaviour,
        IEventHandler<RoadBuiltEvent>,
        IEventHandler<RoadDemolishedEvent>
    {
        [Header("Road Profile")]
        [Tooltip("Defines the cross-section: carriageway, pavement, cycle lanes, etc.")]
        [SerializeField] private RoadProfile? roadProfile;

        [Header("Materials")]
        [Tooltip("One material per MaterialIndex defined in the profile. Index 0 = carriageway by convention.")]
        [SerializeField] private Material[] roadMaterials = System.Array.Empty<Material>();

        [Header("Highlight")]
        [SerializeField] private Color highlightColor = new(1f, 0.35f, 0.1f, 1f);

        [Header("Elevation")]
        [Tooltip("Lifts road meshes slightly above the terrain to prevent z-fighting.")]
        [SerializeField] private float roadElevation = 0.05f;

        public MeshRegistry? Registry { get; private set; }

        private void Start()
        {
            if (roadProfile == null)
            {
                Debug.LogWarning("RoadRenderer: no RoadProfile assigned – roads will not be visible.", this);
                return;
            }

            if (roadMaterials.Length == 0)
            {
                Debug.LogWarning("RoadRenderer: no materials assigned – roads will appear pink.", this);
            }

            // Use the first material as the shared base for highlight tracking
            Material baseMaterial = roadMaterials.Length > 0
                ? roadMaterials[0]
                : new Material(Shader.Find("Hidden/InternalErrorShader")!);

            Registry = new MeshRegistry(baseMaterial, highlightColor);

            EventBus bus = GameServices.Instance!.Bus;
            bus.Subscribe<RoadBuiltEvent>(this);
            bus.Subscribe<RoadDemolishedEvent>(this);

            // Visualize any segments that were built before this component started
            foreach (RoadSegment seg in GameServices.Instance.Roads.Graph.Segments.Values)
                SpawnSegmentMesh(seg);
        }

        public void Handle(RoadBuiltEvent evt)
        {
            if (!GameServices.Instance!.Roads.Graph.Segments.TryGetValue(evt.SegmentId, out RoadSegment seg))
                return;
            SpawnSegmentMesh(seg);
        }

        public void Handle(RoadDemolishedEvent evt) => Registry?.Unregister(evt.SegmentId);

        /// <summary>
        /// Destroys and recreates the mesh for one segment.
        /// Called by IntersectionRenderer after it updates TrimmedStartT/TrimmedEndT
        /// so the new mesh reflects the correct clipped length.
        /// </summary>
        public void RebuildSegment(int segmentId)
        {
            Registry?.Unregister(segmentId);

            if (GameServices.Instance!.Roads.Graph.Segments.TryGetValue(segmentId, out RoadSegment seg))
                SpawnSegmentMesh(seg);
        }

        // ─────────────────────────────────────────────────────────
        //  Mesh spawning
        // ─────────────────────────────────────────────────────────

        private void SpawnSegmentMesh(RoadSegment seg)
        {
            if (roadProfile == null || Registry == null)
                return;

            RoadGraph graph = GameServices.Instance!.Roads.Graph;

            if (!graph.Nodes.TryGetValue(seg.NodeA, out RoadNode nodeA) ||
                !graph.Nodes.TryGetValue(seg.NodeB, out RoadNode nodeB))
                return;

            RoadMeshData? data = RoadMeshBuilder.Build(seg, roadProfile, nodeA.Position, nodeB.Position);
            if (data == null)
                return;

            GameObject go = BuildGameObject(seg, data);
            Registry.Register(seg.Id, go);
        }

        /// <summary>
        /// Creates a GameObject with MeshFilter, MeshRenderer, and MeshCollider
        /// from the given mesh data. The collider lets the demolish tool raycast
        /// against the actual road surface rather than an approximation.
        /// </summary>
        private GameObject BuildGameObject(RoadSegment seg, RoadMeshData data)
        {
            GameObject go = new ($"Road_{seg.Id}");
            go.transform.position = new Vector3(0f, roadElevation, 0f);

            int roadLayer = LayerMask.NameToLayer("Road");
            if (roadLayer != -1)
                go.layer = roadLayer;

            Mesh mesh = BuildMesh(data);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = BuildMaterialArray(data.Triangles.Length);

            mesh.UploadMeshData(markNoLongerReadable: true);

            return go;
        }

        /// <summary>
        /// Populates a Unity Mesh from the plain vertex arrays produced by RoadMeshBuilder.
        /// </summary>
        private static Mesh BuildMesh(RoadMeshData data)
        {
            Mesh mesh         = new ();
            mesh.name         = "RoadMesh";
            mesh.indexFormat  = UnityEngine.Rendering.IndexFormat.UInt32;  // Supports > 65k vertices
            mesh.SetVertices(data.Vertices);
            mesh.SetNormals(data.Normals);
            mesh.SetUVs(0, data.UVs);

            mesh.subMeshCount = data.Triangles.Length;
            for (int i = 0; i < data.Triangles.Length; i++)
                mesh.SetTriangles(data.Triangles[i], i);

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Returns a material array matching the number of submeshes.
        /// Slots beyond roadMaterials.Length fall back to the last assigned material.
        /// </summary>
        private Material[] BuildMaterialArray(int submeshCount)
        {
            Material[] mats = new Material[submeshCount];
            for (int i = 0; i < submeshCount; i++)
            {
                mats[i] = i < roadMaterials.Length
                    ? roadMaterials[i]
                    : roadMaterials[roadMaterials.Length - 1];
            }
            return mats;
        }

        private void OnDestroy()
        {
            if (GameServices.Instance == null)
                return;

            EventBus bus = GameServices.Instance.Bus;
            bus.Unsubscribe<RoadBuiltEvent>(this);
            bus.Unsubscribe<RoadDemolishedEvent>(this);
            Registry?.Clear();
        }
    }
}
