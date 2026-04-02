using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#nullable enable
namespace CityBuilder.Tools
{
    public class BulldozerTool : MonoBehaviour
    {
        public event Action<bool>? OnActiveChanged;

        public bool IsActive { get; private set; }

        private readonly List<IDemolishHandler> _handlers = new();
        private Camera? _camera;

        public void RegisterHandler(IDemolishHandler handler)
        {
            _handlers.Add(handler);
            Debug.Log($"[BulldozerTool] Handler registered: {handler.GetType().Name} (total: {_handlers.Count})");
        }

        public void UnregisterHandler(IDemolishHandler handler) => _handlers.Remove(handler);

        private void Start() => _camera = Camera.main;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse ms = Mouse.current;
            if (kb == null || ms == null) return;

            if (kb.bKey.wasPressedThisFrame)
            {
                SetActive(!IsActive);
                Debug.Log($"[BulldozerTool] IsActive = {IsActive}, handlers = {_handlers.Count}");
            }

            if (!IsActive || !ms.leftButton.wasPressedThisFrame) return;

            if (!_camera)
            {
                Debug.LogError("[BulldozerTool] _camera is null!");
                return;
            }

            Ray ray = _camera.ScreenPointToRay(ms.position.value);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log("[BulldozerTool] Raycast missed – nothing hit.");
                return;
            }

            Debug.Log($"[BulldozerTool] Raycast hit: '{hit.collider.gameObject.name}' " +
                      $"layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} " +
                      $"handlers={_handlers.Count}");

            bool claimed = false;
            foreach (IDemolishHandler handler in _handlers)
            {
                bool result = handler.TryDemolish(hit, Time.time);
                Debug.Log($"[BulldozerTool] {handler.GetType().Name}.TryDemolish = {result}");
                if (result) { claimed = true; break; }
            }

            if (!claimed)
                Debug.Log("[BulldozerTool] No handler claimed the hit.");
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            OnActiveChanged?.Invoke(IsActive);
        }
    }
}
