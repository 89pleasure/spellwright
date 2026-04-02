using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#nullable enable
namespace CityBuilder.Rendering
{
    /// <summary>
    /// Generic per-ID mesh lifecycle manager.
    /// Owns a set of GameObjects keyed by integer ID and handles
    /// highlight/restore without knowing anything about domain objects.
    /// </summary>
    public class MeshRegistry
    {
        private readonly Dictionary<int, GameObject> _objects = new();

        // Reverse lookup: Unity entity ID → entity ID, for direct raycast hit identification
        private readonly Dictionary<EntityId, int> _instanceToId = new();

        private readonly Material _sharedMaterial;
        private readonly Color _highlightColor;
        private int _highlightedId = -1;

        public MeshRegistry(Material sharedMaterial, Color highlightColor)
        {
            _sharedMaterial = sharedMaterial;
            _highlightColor = highlightColor;
        }

        public void Register(int id, GameObject go)
        {
            _objects[id] = go;
            _instanceToId[go.GetEntityId()] = id;
        }

        public void Unregister(int id)
        {
            if (_highlightedId == id)
            {
                _highlightedId = -1;
            }

            if (!_objects.TryGetValue(id, out GameObject go))
            {
                return;
            }

            _instanceToId.Remove(go.GetEntityId());
            Object.Destroy(go);
            _objects.Remove(id);
        }

        /// <summary>
        /// Resolves a GameObject (e.g. from a RaycastHit) back to its entity ID.
        /// Returns false if the object is not managed by this registry.
        /// </summary>
        public bool TryGetId(GameObject go, out int entityId) =>
            _instanceToId.TryGetValue(go.GetEntityId(), out entityId);

        /// <summary>Highlights the given ID; clears any previously highlighted entry.</summary>
        public void SetHighlight(int id)
        {
            if (_highlightedId == id)
            {
                return;
            }

            ClearHighlight();

            if (!_objects.TryGetValue(id, out GameObject highlightGo))
            {
                return;
            }

            // .material creates a per-instance copy, leaving all others unaffected
            highlightGo.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", _highlightColor);
            _highlightedId = id;
        }

        /// <summary>Restores the highlighted entry to the shared material.</summary>
        public void ClearHighlight()
        {
            if (_highlightedId == -1)
            {
                return;
            }

            if (_objects.TryGetValue(_highlightedId, out GameObject prevGo))
            {
                prevGo.GetComponent<MeshRenderer>().sharedMaterial = _sharedMaterial;
            }

            _highlightedId = -1;
        }

        /// <summary>Destroys all registered objects and clears the registry.</summary>
        public void Clear()
        {
            foreach (GameObject go in _objects.Values.OfType<GameObject>())
            {
                Object.Destroy(go);
            }

            _objects.Clear();
            _instanceToId.Clear();
            _highlightedId = -1;
        }
    }
}
