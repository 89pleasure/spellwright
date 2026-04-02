using UnityEngine;
using UnityEngine.InputSystem;

namespace CityBuilder.Core
{
    /// <summary>
    /// Top-down city-builder camera using the Unity Input System.
    /// WASD / arrow keys: pan  |  Scroll wheel: zoom  |  Middle mouse drag: pan
    /// Pan speed scales with camera height so movement feels consistent at all zoom levels.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] private float panSpeed = 0.3f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 1.5f;
        [SerializeField] private float minHeight = 10f;
        [SerializeField] private float maxHeight = 600f;

        // Mouse scroll in the new Input System is in pixels (~120 per notch), not –1…1
        private const float ScrollNormalize = 0.1f;

        private void Update()
        {
            HandleKeyPan();
            HandleMiddleMousePan();
            HandleZoom();
        }

        private void HandleKeyPan()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            float h = 0f;
            float v = 0f;

            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
            {
                h -= 1f;
            }

            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
            {
                h += 1f;
            }

            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
            {
                v -= 1f;
            }

            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
            {
                v += 1f;
            }

            if (h == 0f && v == 0f)
            {
                return;
            }

            float speed = panSpeed * transform.position.y * Time.deltaTime;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            transform.position += (right * h + forward * v) * speed;
        }

        private void HandleMiddleMousePan()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.middleButton.isPressed)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();
            if (delta == Vector2.zero)
            {
                return;
            }

            float speed = panSpeed * transform.position.y * Time.deltaTime;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            transform.position += (-right * delta.x + -forward * delta.y) * (speed * 0.1f);
        }

        private void HandleZoom()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y * ScrollNormalize;
            if (scroll == 0f)
            {
                return;
            }

            Vector3 pos = transform.position + transform.forward * (scroll * zoomSpeed * transform.position.y);
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
            transform.position = pos;
        }
    }
}
