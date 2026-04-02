using CityBuilder.Core;
using CityBuilder.Tools;
using UnityEngine;
using UnityEngine.InputSystem;

#nullable enable
namespace CityBuilder.Rendering.Roads
{
    /// <summary>
    /// Demolishes road segments via direct raycast hit on the road mesh.
    /// Highlights the hovered segment while the bulldozer tool is active.
    /// Attach to any GameObject in the scene.
    /// Assign <see cref="BulldozerTool"/> and <see cref="RoadRenderer"/> in the Inspector.
    /// </summary>
    public class RoadDemolishHandler : MonoBehaviour, IDemolishHandler
    {
        [SerializeField] private BulldozerTool? bulldozerTool;
        [SerializeField] private RoadRenderer? roadRenderer;

        private Camera? _camera;

        private void Start()
        {
            if (bulldozerTool == null)
            {
                return;
            }

            _camera = Camera.main;
            bulldozerTool.RegisterHandler(this);
        }

        private void Update()
        {
            if (!bulldozerTool || roadRenderer?.Registry == null)
            {
                return;
            }

            if (!bulldozerTool.IsActive)
            {
                roadRenderer.Registry.ClearHighlight();
                return;
            }

            Mouse ms = Mouse.current;
            if (ms == null || !_camera)
            {
                roadRenderer.Registry.ClearHighlight();
                return;
            }

            Ray ray = _camera.ScreenPointToRay(ms.position.value);
            if (Physics.Raycast(ray, out RaycastHit hit) &&
                roadRenderer.Registry.TryGetId(hit.collider.gameObject, out int segId))
            {
                roadRenderer.Registry.SetHighlight(segId);
            }
            else
            {
                roadRenderer.Registry.ClearHighlight();
            }
        }

        private void OnDestroy()
        {
            if (bulldozerTool != null)
            {
                bulldozerTool.UnregisterHandler(this);
            }

            roadRenderer?.Registry?.ClearHighlight();
        }

        public bool TryDemolish(RaycastHit hit, float gameTime)
        {
            if (roadRenderer?.Registry == null || GameServices.Instance == null)
            {
                return false;
            }

            if (!roadRenderer.Registry.TryGetId(hit.collider.gameObject, out int segmentId))
            {
                return false;
            }

            return GameServices.Instance.Roads.DemolishRoad(segmentId, gameTime);
        }
    }
}
