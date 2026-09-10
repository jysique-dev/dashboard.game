using System;
using System.Collections.Generic;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// Makes the inventory react to what the machine does: a pulse on the materials a craft
    /// just consumed, and on the items it just delivered.
    /// </summary>
    /// <remarks>
    /// This is the one place the two blocks meet, and it is a one-way street. The inventory
    /// knows nothing about crafting and the machine UI knows nothing about grids; this class
    /// listens to one and calls the other. Remove it and both halves keep working.
    /// <para>
    /// It listens to the adapter's raw event rather than its coalesced flags, because a
    /// pulse is a one-shot reaction to a specific moment. Coalescing exists to avoid
    /// rebuilding layout forty times; flashing a cell is exactly the thing that should
    /// happen forty times.
    /// </para>
    /// </remarks>
    public sealed class CraftingHighlightBridge : IDisposable
    {
        private readonly MachineUIAdapter _machines;
        private readonly CraftingUITheme _theme;
        private readonly List<InventoryGridView> _grids = new List<InventoryGridView>(3);

        private bool _disposed;

        public CraftingHighlightBridge(MachineUIAdapter machines, CraftingUITheme theme)
        {
            _machines = machines ?? throw new ArgumentNullException(nameof(machines));
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));

            _machines.Raised += OnCraftingEvent;
        }

        /// <summary>Pulse drawn on materials leaving a container.</summary>
        public UnityEngine.Color ConsumedColor { get; set; }

        /// <summary>Pulse drawn on items arriving in a container.</summary>
        public UnityEngine.Color ProducedColor { get; set; }

        /// <summary>Flash length in milliseconds.</summary>
        public int DurationMs { get; set; } = 320;

        /// <summary>False to stop reacting without unsubscribing.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Adds a grid to pulse. Any grid: player inventory, machine input, output.</summary>
        public void AddGrid(InventoryGridView grid)
        {
            if (grid != null && !_grids.Contains(grid))
                _grids.Add(grid);
        }

        public bool RemoveGrid(InventoryGridView grid) => _grids.Remove(grid);

        private void OnCraftingEvent(in CraftingEventArgs args)
        {
            if (!Enabled || args.Recipe == null || _grids.Count == 0)
                return;

            switch (args.Type)
            {
                case CraftingEventType.JobStarted:
                    // Inputs are paid for when the job starts, not when it finishes.
                    FlashAll(args.Recipe.Inputs, Consumed());
                    break;

                case CraftingEventType.RepetitionCompleted:
                    // One repetition landed: outputs were delivered, and the next repetition
                    // has already been paid for if there is one.
                    FlashAll(args.Recipe.Outputs, Produced());
                    if (args.Machine != null && args.Machine.GetSlot(args.SlotIndex).Remaining > 0)
                        FlashAll(args.Recipe.Inputs, Consumed());
                    break;

                case CraftingEventType.JobCancelled:
                    // Cancelling refunds the in-flight repetition, so materials come back.
                    FlashAll(args.Recipe.Inputs, Produced());
                    break;
            }
        }

        private void FlashAll(ItemStack[] stacks, UnityEngine.Color color)
        {
            if (stacks == null)
                return;

            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i].IsEmpty)
                    continue;

                CraftingId item = stacks[i].Item;
                for (int g = 0; g < _grids.Count; g++)
                    _grids[g].Flash(in item, color, DurationMs);
            }
        }

        private UnityEngine.Color Consumed()
            => ConsumedColor == default ? _theme.Blocked : ConsumedColor;

        private UnityEngine.Color Produced()
            => ProducedColor == default ? _theme.Running : ProducedColor;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _machines.Raised -= OnCraftingEvent;
            _grids.Clear();
        }
    }
}