using System;
using UnityEngine;

namespace LoopEngine.CraftingEngine.Samples
{
    /// <summary>
    /// Smallest possible check that the system is wired up: build a container, put items in
    /// it, print what is inside. No crafting yet.
    /// </summary>
    /// <remarks>
    /// Sample code. Delete it once your real inventory implements <see cref="IItemContainer"/>.
    /// </remarks>
    [AddComponentMenu("LoopEngine/Crafting/Samples/Inventory Test Bed")]
    public sealed class InventoryTestBed : MonoBehaviour
    {
        [System.Serializable]
        public struct StartingStack
        {
            [Tooltip("Item id exactly as authored on the ItemDefinition asset.")]
            public string itemId;

            [Min(1)] public int amount;
        }

        [SerializeField, Tooltip("Database with your ItemDefinition assets. Required.")]
        private CraftingDatabase _database;

        [SerializeField, Tooltip("Maximum units of any single item. 0 means unlimited.")]
        private int _capacityPerItem;

        [SerializeField, Tooltip("What to put in the inventory on start.")]
        private StartingStack[] _startingItems = new StartingStack[0];

        private CraftingContext _context;
        private SimpleItemContainer _inventory;

        /// <summary>The inventory built by this component. Null before Start.</summary>
        public SimpleItemContainer Inventory => _inventory;


        private void Start()
        {
            if (_database == null)
            {
                Debug.LogError("[TestBed] No database assigned.", this);
                return;
            }

            // 1. Build the registries. Items must be registered before anything can be added,
            //    because an unregistered id is not a real item as far as the system is concerned.
            var report = new CraftingBuildReport();
            _context = _database.BuildContext(new UnityCraftingClock(), report);

            Debug.Log($"[TestBed] Registered {_context.Items.Count} item(s). " +
                      $"Rejected: {report.RejectedItems.Count}.", this);

            // 2. Create the inventory. This is just an IItemContainer; nothing about it is
            //    special to the crafting system.
            _inventory = new SimpleItemContainer(_capacityPerItem, _context.Items.Count);


            // 3. Fill it.
            AddStartingItems();

            // 4. Show what landed.
            Dump();
        }

        private void AddStartingItems()
        {
            if (_startingItems == null)
                return;

            for (int i = 0; i < _startingItems.Length; i++)
            {
                StartingStack entry = _startingItems[i];
                if (string.IsNullOrWhiteSpace(entry.itemId))
                    continue;

                if (!TryAdd(entry.itemId, entry.amount, out int inserted))
                    continue;

                if (inserted < entry.amount)
                {
                    Debug.LogWarning(
                        $"[TestBed] '{entry.itemId}': only {inserted} of {entry.amount} fit. " +
                        "Per-item capacity reached.", this);
                }
            }
        }

        /// <summary>
        /// Adds items by id, refusing anything the registry does not know.
        /// This check is the point of the sample: silently accepting an unknown id would
        /// leave you with an inventory full of items no recipe can ever consume.
        /// </summary>
        public bool TryAdd(string rawItemId, int amount, out int inserted)
        {
            inserted = 0;

            if (_inventory == null || amount < 1)
                return false;

            var id = new CraftingId(rawItemId);

            if (!_context.Items.TryGet(in id, out ItemDefinition item))
            {
                Debug.LogError($"[TestBed] Unknown item id '{rawItemId}'. " +
                               "Check the Id field on the ItemDefinition asset.", this);
                return false;
            }

            var stack = new ItemStack(item.Id, amount);
            inserted = _inventory.Insert(in stack);
            return inserted > 0;
        }

        /// <summary>Removes items by id. Returns how many were actually taken.</summary>
        public int Remove(string rawItemId, int amount)
        {
            if (_inventory == null || amount < 1)
                return 0;

            var id = new CraftingId(rawItemId);
            var stack = new ItemStack(id, amount);
            return _inventory.Remove(in stack);
        }

        /// <summary>Logs the whole inventory.</summary>
        [ContextMenu("Dump Inventory")]
        public void Dump()
        {
            if (_inventory == null)
            {
                Debug.Log("[TestBed] Inventory not built yet.", this);
                return;
            }

            // Passing the registry resolves display names; without it you get raw ids.
            Debug.Log("[TestBed] " + _inventory.ToString(_context.Items), this);
        }
    }
}