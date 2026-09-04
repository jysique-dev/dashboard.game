using System.Collections.Generic;

namespace LoopEngine.GridMovement.Internal
{
    /// <summary>
    /// A binary min-heap: the cheapest item comes out first.
    ///
    /// Written by hand on purpose. System.Collections.Generic.PriorityQueue only exists
    /// from .NET 6 onwards and is not available to Unity scripting, which targets
    /// .NET Standard 2.1, so relying on it would not compile.
    /// (Reference: Microsoft docs list PriorityQueue as .NET 6+; Unity documents the
    /// .NET Standard 2.1 API profile for player scripting.)
    ///
    /// Duplicate entries are allowed rather than doing a decrease-key. That is the usual
    /// trade in game pathfinding: pushing a second, cheaper entry costs one heap slot,
    /// while finding and sifting an existing one costs an index that has to be kept in
    /// sync. The search discards the stale entry when it pops it, so the result is the
    /// same either way.
    /// </summary>
    internal sealed class MinHeap<T>
    {
        private readonly List<T> items;
        private readonly List<float> priorities;

        public MinHeap(int capacity = 16)
        {
            items = new List<T>(capacity);
            priorities = new List<float>(capacity);
        }

        public int Count => items.Count;

        public void Clear()
        {
            items.Clear();
            priorities.Clear();
        }

        public void Push(T item, float priority)
        {
            items.Add(item);
            priorities.Add(priority);
            SiftUp(items.Count - 1);
        }

        /// <summary>Removes and returns the cheapest item. Undefined when the heap is empty; check Count.</summary>
        public T Pop()
        {
            T result = items[0];
            int last = items.Count - 1;

            items[0] = items[last];
            priorities[0] = priorities[last];
            items.RemoveAt(last);
            priorities.RemoveAt(last);

            if (items.Count > 0) SiftDown(0);
            return result;
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (priorities[index] >= priorities[parent]) break;
                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            int count = items.Count;

            while (true)
            {
                int left = index * 2 + 1;
                if (left >= count) break;

                int smallest = left;
                int right = left + 1;
                if (right < count && priorities[right] < priorities[left]) smallest = right;

                if (priorities[index] <= priorities[smallest]) break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            (items[a], items[b]) = (items[b], items[a]);
            (priorities[a], priorities[b]) = (priorities[b], priorities[a]);
        }
    }
}