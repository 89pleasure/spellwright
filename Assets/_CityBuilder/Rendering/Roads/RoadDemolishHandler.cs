using CityBuilder.Core;
using CityBuilder.Tools;
using UnityEngine;
using UnityEngine.InputSystem;

#nullable enable
namespace CityBuilder.Rendering.Roads
{
    public class RoadDemolishHandler : MonoBehaviour, IDemolishHandler
    {
        [SerializeField] private BulldozerTool? bulldozerTool;
        [SerializeField] private RoadRenderer? roadRenderer;

        private Camera? _camera;

        private void Start()
        {
            if (bulldozerTool == null)
                bulldozerTool = FindAnyObjectByType<BulldozerTool>();

            if (bulldozerTool == null)
            {
                Debug.LogError("[RoadDemolishHandler] BulldozerTool not found!", this);
                return;
            }

            if (roadRenderer == null)
                roadRenderer = FindAnyObjectByType<RoadRenderer>();

            if (roadRenderer == null)
            {
                Debug.LogError("[RoadDemolishHandler] RoadRenderer not found!", this);
                return;
            }

            _camera = Camera.main;
            bulldozerTool.RegisterHandler(this);
            Debug.Log("[RoadDemolishHandler] Registered successfully.", this);
        }

        private void Update()
        {
            if (!bulldozerTool || roadRenderer?.Registry == null) { return; }
            if (!bulldozerTool.IsActive) { roadRenderer.Registry.ClearHighlight(); return; }

            Mouse ms = Mouse.current;
            if (ms == null || !_camera) { roadRenderer.Registry.ClearHighlight(); return; }

            Ray ray = _camera.ScreenPointToRay(ms.position.value);
            int roadMask = LayerMask.GetMask("Road");

            if (Physics.Raycast(ray, out RaycastHit roadHit, 1000f, roadMask))
            {
                Debug.Log($"[RoadOnly] Hit {roadHit.collider.gameObject.name}");
            }
            else
            {
                Debug.Log("[RoadOnly] Miss");
            }

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (roadRenderer.Registry.TryGetId(hit.collider.gameObject, out int segId))
                {
                    roadRenderer.Registry.SetHighlight(segId);
                }
                else
                {
                    // Hit something, but it's not a road – log once per new object
                    Debug.Log($"[RoadDemolishHandler] Hover hit '{hit.collider.gameObject.name}' – not in Registry.");
                    roadRenderer.Registry.ClearHighlight();
                }
            }
            else
            {
                roadRenderer.Registry.ClearHighlight();
            }
        }

        private void OnDestroy()
        {
            bulldozerTool?.UnregisterHandler(this);
            roadRenderer?.Registry?.ClearHighlight();
        }

        public bool TryDemolish(RaycastHit hit, float gameTime)
        {
            if (roadRenderer?.Registry == null)
            {
                Debug.LogError("[RoadDemolishHandler] TryDemolish: Registry is null!");
                return false;
            }

            if (GameServices.Instance == null)
            {
                Debug.LogError("[RoadDemolishHandler] TryDemolish: GameServices.Instance is null!");
                return false;
            }

            if (!roadRenderer.Registry.TryGetId(hit.collider.gameObject, out int segmentId))
            {
                Debug.Log($"[RoadDemolishHandler] TryDemolish: '{hit.collider.gameObject.name}' not in Registry.");
                return false;
            }

            Debug.Log($"[RoadDemolishHandler] Demolishing segment {segmentId}.");
            return GameServices.Instance.Roads.DemolishRoad(segmentId, gameTime);
        }
    }
}
