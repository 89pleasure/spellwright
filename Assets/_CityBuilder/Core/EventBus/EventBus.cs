using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityBuilder.Core.EventBus
{
    public class EventBus
    {
        private const int MaxCascadeDepth = 10;

        private readonly Dictionary<Type, List<object>> _handlers = new();
        private readonly HashSet<(object entity, Type eventType, float gameTime)> _processedThisFrame = new();

        public void Subscribe<T>(IEventHandler<T> handler) where T : ISimulationEvent
        {
            Type type = typeof(T);
            if (!_handlers.TryGetValue(type, out List<object> list))
            {
                list = new List<object>();
                _handlers[type] = list;
            }
            list.Add(handler);
        }

        public void Unsubscribe<T>(IEventHandler<T> handler) where T : ISimulationEvent
        {
            if (_handlers.TryGetValue(typeof(T), out List<object> list))
            {
                list.Remove(handler);
            }
        }

        public void Publish<T>(T evt) where T : ISimulationEvent
        {
            if (evt.CascadeDepth > MaxCascadeDepth)
            {
                Debug.LogWarning($"[EventBus] Cascade depth exceeded for {typeof(T).Name}");
                return;
            }

            (object, Type, float GameTime) key = (evt as object, typeof(T), evt.GameTime);
            if (!_processedThisFrame.Add(key))
            {
                return;
            }

            if (!_handlers.TryGetValue(typeof(T), out List<object> list))
            {
                return;
            }

            foreach (object handler in list)
            {
                ((IEventHandler<T>)handler).Handle(evt);
            }
        }

        public void ClearFrameCache() => _processedThisFrame.Clear();
    }
}
