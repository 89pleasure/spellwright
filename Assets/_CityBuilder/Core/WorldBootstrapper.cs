using CityBuilder.Core;
using CityBuilder.Infrastructure.Roads;
using Unity.Mathematics;
using UnityEngine;

namespace CityBuilder
{
    /// <summary>
    /// Entry point for the simulation. Attach to a single GameObject in the main scene.
    /// Initializes all services and spawns the procedural starter world.
    /// </summary>
    public class WorldBootstrapper : MonoBehaviour
    {
        [SerializeField] private int terrainSize = 1000;
        [SerializeField] private Color terrainColor = new Color(0.35f, 0.6f, 0.25f);

        private void Awake()
        {
            GameServices.Initialize();
            CreateFlatTerrain();
            SpawnStarterWorld();
        }

        private void OnDestroy() => GameServices.Shutdown();

        private void CreateFlatTerrain()
        {
            TerrainData data = new ()
            {
                heightmapResolution = 257, // 2^8 + 1
                size = new Vector3(terrainSize, 600, terrainSize)
            };
            // Heights default to 0 → perfectly flat

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Terrain";
            terrainGo.transform.position = new Vector3(-terrainSize / 2f, 0, -terrainSize / 2f);

            Shader flatShader = Shader.Find("CityBuilder/FlatShading");
            if (flatShader != null)
            {
                Material mat = new (flatShader);
                mat.SetColor("_BaseColor", terrainColor);
                terrainGo.GetComponent<Terrain>().materialTemplate = mat;
            }
            else
            {
                Debug.LogWarning("[WorldBootstrapper] CityBuilder/FlatShading shader not found.");
            }
        }

        private static void SpawnStarterWorld()
        {
            RoadGraphService roads = GameServices.Instance!.Roads;
            const float t = 0f; // game time at world start

            // + intersection at origin.
            // Each road is split into two segments so the shared center node
            // gets created first and all four arms snap onto it.
            roads.BuildRoad(new float3(0, 0, 0), new float3(-200, 0, 0), t); // west
            roads.BuildRoad(new float3(0, 0, 0), new float3(200, 0, 0), t); // east
            roads.BuildRoad(new float3(0, 0, 0), new float3(0, 0, -200), t); // south
            roads.BuildRoad(new float3(0, 0, 0), new float3(0, 0, 200), t); // north

            // A short side street branching north-east from the intersection
            roads.BuildRoad(new float3(0, 0, 0), new float3(120, 0, 120), t);

            Debug.Log($"[WorldBootstrapper] Starter world ready – " +
                      $"{roads.Graph.Nodes.Count} nodes, " +
                      $"{roads.Graph.Segments.Count} segments.");
        }
    }
}
