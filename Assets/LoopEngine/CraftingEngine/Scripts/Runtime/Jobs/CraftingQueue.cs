namespace LoopEngine.CraftingEngine
{
    /// <summary>What the queue does when the entry at the front cannot start yet.</summary>
    public enum QueueStallPolicy
    {
        /// <summary>
        /// Hold the line. The machine idles until the entry becomes startable.
        /// Order is guaranteed; a missing ingredient stops everything behind it.
        /// </summary>
        Wait = 0,

        /// <summary>
        /// Look further down the queue for something that can start now.
        /// Keeps the machine busy; order is no longer guaranteed.
        /// </summary>
        SkipToNext = 1,

        /// <summary>
        /// Discard entries that cannot start and move on.
        /// For queues that must never stall, at the cost of silently losing orders.
        /// </summary>
        Drop = 2
    }

    /// <summary>An order waiting for a free slot.</summary>
    public readonly struct QueuedCraft
    {
        public static readonly QueuedCraft Empty = default;

        public readonly RecipeDefinition Recipe;

        /// <summary>Repetitions requested.</summary>
        public readonly int Count;

        /// <summary>Identifier handed to the caller so it can cancel this specific entry.</summary>
        public readonly int Ticket;

        public QueuedCraft(RecipeDefinition recipe, int count, int ticket)
        {
            Recipe = recipe;
            Count = count < 1 ? 1 : count;
            Ticket = ticket;
        }

        public bool IsValid => Recipe != null && Ticket != 0;
    }

    /// <summary>
    /// Fixed capacity FIFO of pending orders, backed by a ring buffer allocated once.
    /// Enqueue and dequeue are O(1); removing from the middle is O(n) in the queue length,
    /// which is bounded by the machine's declared capacity.
    /// </summary>
    public sealed class CraftingQueue
    {
        private readonly QueuedCraft[] _items;
        private int _head;
        private int _count;

        public CraftingQueue(int capacity)
        {
            if (capacity < 0)
                capacity = 0;

            _items = new QueuedCraft[capacity];
        }

        public int Capacity => _items.Length;

        public int Count => _count;

        public bool IsEmpty => _count == 0;

        public bool IsFull => _count >= _items.Length;

        /// <summary>Entry at a logical position, 0 being the front. Empty when out of range.</summary>
        public QueuedCraft GetAt(int index)
        {
            if (index < 0 || index >= _count)
                return QueuedCraft.Empty;

            return _items[Wrap(_head + index)];
        }

        public bool TryEnqueue(in QueuedCraft item)
        {
            if (IsFull || !item.IsValid)
                return false;

            _items[Wrap(_head + _count)] = item;
            _count++;
            return true;
        }

        public bool TryPeek(out QueuedCraft item)
        {
            if (_count == 0)
            {
                item = QueuedCraft.Empty;
                return false;
            }

            item = _items[_head];
            return true;
        }

        public bool TryDequeue(out QueuedCraft item)
        {
            if (_count == 0)
            {
                item = QueuedCraft.Empty;
                return false;
            }

            item = _items[_head];
            _items[_head] = QueuedCraft.Empty;
            _head = Wrap(_head + 1);
            _count--;
            return true;
        }

        /// <summary>
        /// Removes the entry at a logical position, preserving the order of the rest.
        /// Shifts from whichever end is closer, so removing the front stays O(1).
        /// </summary>
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _count)
                return false;

            if (index == 0)
                return TryDequeue(out _);

            if (index == _count - 1)
            {
                _items[Wrap(_head + index)] = QueuedCraft.Empty;
                _count--;
                return true;
            }

            for (int i = index; i < _count - 1; i++)
                _items[Wrap(_head + i)] = _items[Wrap(_head + i + 1)];

            _items[Wrap(_head + _count - 1)] = QueuedCraft.Empty;
            _count--;
            return true;
        }

        /// <summary>Finds a logical position by ticket, or -1.</summary>
        public int IndexOfTicket(int ticket)
        {
            if (ticket == 0)
                return -1;

            for (int i = 0; i < _count; i++)
            {
                if (_items[Wrap(_head + i)].Ticket == ticket)
                    return i;
            }

            return -1;
        }

        public bool RemoveByTicket(int ticket)
        {
            int index = IndexOfTicket(ticket);
            return index >= 0 && RemoveAt(index);
        }

        /// <summary>
        /// Moves an entry to another logical position, shifting the entries in between.
        /// For queue reordering in the UI.
        /// </summary>
        public bool TryMove(int from, int to)
        {
            if (from < 0 || from >= _count || to < 0 || to >= _count || from == to)
                return false;

            QueuedCraft moving = _items[Wrap(_head + from)];

            if (to > from)
            {
                for (int i = from; i < to; i++)
                    _items[Wrap(_head + i)] = _items[Wrap(_head + i + 1)];
            }
            else
            {
                for (int i = from; i > to; i--)
                    _items[Wrap(_head + i)] = _items[Wrap(_head + i - 1)];
            }

            _items[Wrap(_head + to)] = moving;
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < _count; i++)
                _items[Wrap(_head + i)] = QueuedCraft.Empty;

            _head = 0;
            _count = 0;
        }

        private int Wrap(int index)
        {
            int length = _items.Length;
            if (length == 0)
                return 0;

            // Index never goes negative here, so a modulo is enough.
            return index >= length ? index % length : index;
        }
    }
}