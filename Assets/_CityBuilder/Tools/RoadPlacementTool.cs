using System;
using CityBuilder.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityBuilder.Tools
{
    /// <summary>
    /// Handles player input for placing road segments.
    /// R            – toggle tool on/off
    /// Left click   – set start point, then end point (chains: end becomes new start)
    /// Right click  – cancel current placement
    /// </summary>
    public class RoadPlacementTool : MonoBehaviour
    {
        [SerializeField] private float roadWidth = 7f;
        [SerializeField] private float roadElevation = 0.05f;
        [SerializeField] private Color previewColor = new(1f, 0.8f, 0.2f, 1f);

        public event Action<bool> OnActiveChanged;

        public bool IsActive => _isActive;

        private bool _isActive;
        private bool _hasStart;
        private Vector3 _startPoint;
        private GameObject _previewGo;
        private Material _previewMaterial;
        private Camera _camera;

        private void Start()
        {
            _camera = Camera.main;

            Shader flatShader = Shader.Find("CityBuilder/FlatShading");
            _previewMaterial = new Material(flatShader != null
                ? flatShader
                : Shader.Find("Hidden/InternalErrorShader"));
            _previewMaterial.SetColor("_BaseColor", previewColor);

            _previewGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _previewGo.name = "RoadPreview";
            _previewGo.GetComponent<MeshRenderer>().material = _previewMaterial;
            Destroy(_previewGo.GetComponent<BoxCollider>());
            _previewGo.SetActive(false);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse ms = Mouse.current;
            if (kb == null || ms == null)
            {
                return;
            }

            if (kb.rKey.wasPressedThisFrame)
            {
                SetActive(!_isActive);
            }

            if (!_isActive)
            {
                return;
            }

            Vector3? worldPos = RaycastTerrain(ms.position.ReadValue());

            UpdatePreview(worldPos);

            if (ms.leftButton.wasPressedThisFrame && worldPos.HasValue)
            {
                HandleClick(worldPos.Value);
            }

            if (ms.rightButton.wasPressedThisFrame)
            {
                CancelPlacement();
            }
        }

        private void HandleClick(Vector3 worldPos)
        {
            if (!_hasStart)
            {
                _startPoint = worldPos;
                _hasStart = true;
                return;
            }

            GameServices.Instance!.Roads.BuildRoad(
                new float3(_startPoint.x, _startPoint.y, _startPoint.z),
                new float3(worldPos.x, worldPos.y, worldPos.z),
                Time.time);

            _startPoint = worldPos;
        }

        private void UpdatePreview(Vector3? worldPos)
        {
            if (!_hasStart || !worldPos.HasValue)
            {
                _previewGo.SetActive(false);
                return;
            }

            Vector3 from = _startPoint + Vector3.up * roadElevation;
            Vector3 to = worldPos.Value + Vector3.up * roadElevation;
            Vector3 dir = to - from;
            float len = dir.magnitude;

            if (len < 0.1f)
            {
                _previewGo.SetActive(false);
                return;
            }

            _previewGo.SetActive(true);
            _previewGo.transform.position = (from + to) * 0.5f;
            _previewGo.transform.rotation = Quaternion.LookRotation(dir.normalized);
            _previewGo.transform.localScale = new Vector3(roadWidth, 0.1f, len);
        }

        private void CancelPlacement()
        {
            _hasStart = false;
            _previewGo.SetActive(false);
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            if (!_isActive)
            {
                CancelPlacement();
            }

            OnActiveChanged?.Invoke(_isActive);
        }

        private Vector3? RaycastTerrain(Vector2 screenPos)
        {
            if (!_camera)
            {
                return null;
            }

            // Exclude the Road layer so clicks land on terrain, not on existing road meshes
            int roadLayer = LayerMask.NameToLayer("Road");
            int layerMask = roadLayer != -1 ? ~(1 << roadLayer) : Physics.DefaultRaycastLayers;

            Ray ray = _camera.ScreenPointToRay(screenPos);
            return Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask) ? hit.point : null;
        }

        private void OnDestroy()
        {
            if (_previewGo != null)
            {
                Destroy(_previewGo);
            }
        }
    }
}
