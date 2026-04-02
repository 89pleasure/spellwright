using System.Collections.Generic;
using CityBuilder.Core.EventBus;

namespace CityBuilder.Core.DES
{
    // Priority queue (min-heap) sorted by GameTime
    public class SimulationEventQueue<T> where T : ISimulationEvent
    {
        private readonly List<T> _heap = new();

        public int Count => _heap.Count;

        public void Enqueue(T evt)
        {
            _heap.Add(evt);
            BubbleUp(_heap.Count - 1);
        }

        public T Peek() => _heap[0];

        public T Dequeue()
        {
            T top = _heap[0];
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            if (_heap.Count > 0)
            {
                SiftDown(0);
            }

            return top;
        }

        private void BubbleUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_heap[parent].GameTime <= _heap[i].GameTime)
                {
                    break;
                }

                (_heap[i], _heap[parent]) = (_heap[parent], _heap[i]);
                i = parent;
            }
        }

        private void SiftDown(int i)
        {
            int n = _heap.Count;
            while (true)
            {
                int left = 2 * i + 1, right = 2 * i + 2, smallest = i;
                if (left < n && _heap[left].GameTime < _heap[smallest].GameTime)
                {
                    smallest = left;
                }

                if (right < n && _heap[right].GameTime < _heap[smallest].GameTime)
                {
                    smallest = right;
                }

                if (smallest == i)
                {
                    break;
                }

                (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
                i = smallest;
            }
        }
    }
}
