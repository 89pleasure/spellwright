using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#nullable enable
namespace CityBuilder.Tools
{
    /// <summary>
    /// Generic demolition tool.
    /// B            – toggle tool on/off
    /// Left click   – raycast scene; first handler that recognises the hit object demolishes it
    ///
    /// Domain-specific demolition logic lives in <see cref="IDemolishHandler"/> implementations
    /// that register themselves via <see cref="RegisterHandler"/>.
    /// </summary>
    public class BulldozerTool : MonoBehaviour
    {
        public event Action<bool>? OnActiveChanged;

        public bool IsActive { get; private set; }

        private readonly List<IDemolishHandler> _handlers = new();
        private Camera? _camera;

        public void RegisterHandler(IDemolishHandler handler) => _handlers.Add(handler);

        public void UnregisterHandler(IDemolishHandler handler) => _handlers.Remove(handler);

        private void Start() => _camera = Camera.main;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse ms = Mouse.current;
            if (kb == null || ms == null)
            {
                return;
            }

            if (kb.bKey.wasPressedThisFrame)
            {
                SetActive(!IsActive);
            }

            if (!IsActive || !ms.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Ray ray = _camera!.ScreenPointToRay(ms.position.value);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                return;
            }

            foreach (IDemolishHandler handler in _handlers)
            {
                if (handler.TryDemolish(hit, Time.time))
                {
                    return;
                }
            }
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            OnActiveChanged?.Invoke(IsActive);
        }
    }
}
