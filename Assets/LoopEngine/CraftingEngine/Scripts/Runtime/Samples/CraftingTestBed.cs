using LoopEngine.CraftingEngine.UI;
using LoopEngine.TagInput;
using System;
using UnityEngine;

namespace LoopEngine.CraftingEngine.Samples
{
    /// <summary>
    /// End to end check of the crafting system: build a database, fill an inventory, create a
    /// machine, press a key to start a job and watch the whole process in the console.
    /// </summary>
    /// <remarks>
    /// Sample code, not production code. It uses the legacy <c>Input</c> class, which requires
    /// Active Input Handling set to "Input Manager (Old)" or "Both" in Player Settings.
    /// Delete this file once your own inventory implements <see cref="IItemContainer"/>.
    /// </remarks>
    [AddComponentMenu("LoopEngine/Crafting/Samples/Crafting Test Bed")]
    public sealed class CraftingTestBed : MonoBehaviour
    {
        [System.Serializable]
        public struct StartingStack
        {
            [Tooltip("Item id exactly as authored on the ItemDefinition asset.")]
            public string itemId;

            [Min(1)] public int amount;
        }

        [Header("Content")]
        [SerializeField, Tooltip("Database with your definitions. Required.")]
        private CraftingDatabase _database;

        [Header("Inventory")]
        [SerializeField, Tooltip("Maximum units of any single item. 0 means unlimited.")]
        private int _capacityPerItem;

        [SerializeField, Tooltip("What to put in the inventory on start.")]
        private StartingStack[] _startingItems = new StartingStack[0];

        [Header("Machine")]
        [SerializeField, Tooltip("Machine id to instantiate. Leave empty to test the inventory only.")]
        private string _machineId = string.Empty;

        [Header("Job")]
        [SerializeField, Tooltip("Recipe started by the key below.")]
        private string _recipeId = string.Empty;

        [SerializeField, Min(1), Tooltip("Repetitions per key press.")]
        private int _count = 3;



        [Header("Logging")]
        [SerializeField, Min(0f), Tooltip("Seconds between progress logs while a job runs. 0 disables them.")]
        private float _progressLogInterval = 0.5f;

        private CraftingContext _context;
        private CraftingSystem _system;
        private SimpleItemContainer _inventory;
        private MachineInstance _machine;
        private float _nextProgressLog;

        /// <summary>The inventory built by this component. Null before Start.</summary>
        public SimpleItemContainer Inventory => _inventory;

        /// <summary>The machine built by this component. Null before Start, or if no id was set.</summary>
        public MachineInstance Machine => _machine;

        public CraftingSystem System => _system;

        public Action<CraftingSystem> OnOpenCrafting;


        public Action<IItemContainer> OnOpenInventory;
        private void Start()
        {
            if (_database == null)
            {
                Debug.LogError("[TestBed] No database assigned.", this);
                return;
            }

            // 1. Registries. An id that is not registered is not a real definition.
            var report = new CraftingBuildReport();
            _context = _database.BuildContext(new UnityCraftingClock(), report);

            

            Debug.Log($"[TestBed] Built database: {_context.Items.Count} item(s), " +
                      $"{_context.Recipes.Count} recipe(s), {_context.Machines.Count} machine(s). " +
                      $"Problems: {report.ProblemCount}.", this);

            // 2. The facade. Nothing ticks until Update calls into it.
            _system = new CraftingSystem(_context);
            _system.Events += OnCraftingEvent;

            OnOpenCrafting?.Invoke(_system);

            // 3. The inventory. Just an IItemContainer; the system knows nothing else about it.
            _inventory = new SimpleItemContainer(_capacityPerItem, _context.Items.Count);
            AddStartingItems();
            OnOpenInventory.Invoke(_inventory);

            Dump();

            // 4. The machine. Input and output share one container here for simplicity;
            //    in a real game they would usually be separate buffers.
            CreateMachine();
        }

        private void Update()
        {
            if (_system == null)
                return;

            _system.Tick();

            if (KeyInputManager.Down("start_mac"))
                StartJob();

            if (KeyInputManager.Down("pause_mac"))
                CancelJobs();

            LogProgress();
        }

        private void OnDestroy()
        {
            if (_system != null)
                _system.Events -= OnCraftingEvent;
        }

        // --- Setup ------------------------------------------------------------------

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

        private void CreateMachine()
        {
            if (string.IsNullOrWhiteSpace(_machineId))
            {
                Debug.Log("[TestBed] No machine id set; inventory only.", this);
                return;
            }

            var machineId = new CraftingId(_machineId);
            CraftingStatus status = _system.CreateMachine(in machineId, _inventory, _inventory, out _machine);

            if (status != CraftingStatus.Success)
            {
                Debug.LogError($"[TestBed] Could not create machine '{_machineId}': {status}.", this);
                return;
            }

            Debug.Log($"[TestBed] Machine '{_machine.Definition.DisplayName}' ready. " +
                      $"Slots: {_machine.SlotCount}, queue: {_machine.QueueCapacity}, " +
                      $"speed x{_machine.Stats.Speed:0.##}. Press start to craft.", this);
        }

        // --- Jobs -------------------------------------------------------------------

        /// <summary>Starts the configured recipe, or queues it when every slot is busy.</summary>
        public void StartJob()
        {
            if (_machine == null)
            {
                Debug.LogWarning("[TestBed] No machine to craft with.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(_recipeId))
            {
                Debug.LogWarning("[TestBed] No recipe id set.", this);
                return;
            }

            var recipeId = new CraftingId(_recipeId);
            CraftingStatus status = _machine.TryStart(in recipeId, _count, out CraftingJobHandle handle);

            if (status == CraftingStatus.Success)
            {
                // The JobStarted event already logged the details; this line only reports
                // which handle the caller got back.
                Debug.Log($"[TestBed] Started {handle}.", this);
                return;
            }

            // Every slot busy is not an error: that is what the queue is for.
            if (status == CraftingStatus.MachineBusy)
            {
                CraftingStatus queued = _machine.Enqueue(in recipeId, _count, out int ticket);
                Debug.Log(queued == CraftingStatus.Success
                    ? $"[TestBed] All slots busy, queued as ticket {ticket}."
                    : $"[TestBed] All slots busy and could not queue: {queued}.", this);
                return;
            }

            Debug.LogWarning($"[TestBed] Could not start '{_recipeId}': {status}. {Explain(status)}", this);
        }

        /// <summary>Cancels everything running on the machine and refunds the in-flight repetition.</summary>
        public void CancelJobs()
        {
            if (_machine == null)
                return;

            int cancelled = _machine.CancelAll();
            _machine.ClearQueue();
            Debug.Log($"[TestBed] Cancelled {cancelled} job(s) and cleared the queue.", this);
        }

        private void LogProgress()
        {
            if (_progressLogInterval <= 0f || _machine == null || _machine.ActiveJobs == 0)
                return;

            if (Time.time < _nextProgressLog)
                return;

            _nextProgressLog = Time.time + _progressLogInterval;

            for (int i = 0; i < _machine.SlotCount; i++)
            {
                CraftingJobView slot = _machine.GetSlot(i);
                if (!slot.IsActive)
                    continue;

                Debug.Log($"[TestBed] Slot {i}: {slot.Recipe.DisplayName} " +
                          $"{slot.Progress * 100f:0}% | done {slot.Completed}, left {slot.Remaining} " +
                          $"| {slot.State}", this);
            }
        }

        // --- Events -----------------------------------------------------------------

        private void OnCraftingEvent(in CraftingEventArgs e)
        {
            string recipe = e.Recipe != null ? e.Recipe.DisplayName : "-";

            switch (e.Type)
            {
                case CraftingEventType.JobStarted:
                    Debug.Log($"[Crafting] START {recipe} in slot {e.SlotIndex}.", this);
                    break;

                case CraftingEventType.RepetitionCompleted:
                    Debug.Log($"[Crafting] MADE {recipe}. {_inventory.ToString(_context.Items)}", this);
                    break;

                case CraftingEventType.JobFinished:
                    Debug.Log(e.Status == CraftingStatus.Success
                        ? $"[Crafting] DONE {recipe}."
                        : $"[Crafting] DONE {recipe}, stopped early: {e.Status}.", this);
                    break;

                case CraftingEventType.JobBlocked:
                    Debug.LogWarning($"[Crafting] BLOCKED {recipe}: output full. Retrying every tick.", this);
                    break;

                case CraftingEventType.JobResumed:
                    Debug.Log($"[Crafting] RESUMED {recipe}.", this);
                    break;

                case CraftingEventType.JobCancelled:
                    Debug.Log($"[Crafting] CANCELLED {recipe}.", this);
                    break;

                case CraftingEventType.Enqueued:
                    Debug.Log($"[Crafting] QUEUED {recipe} (ticket {e.Ticket}).", this);
                    break;

                case CraftingEventType.Dequeued:
                    Debug.Log($"[Crafting] DEQUEUED {recipe} (ticket {e.Ticket}): {e.Status}.", this);
                    break;

                case CraftingEventType.ModifiersChanged:
                    Debug.Log($"[Crafting] STATS speed x{e.Machine.Stats.Speed:0.##}, " +
                              $"yield x{e.Machine.Stats.Yield:0.##}.", this);
                    break;
            }
        }

        /// <summary>Turns the less obvious failures into something actionable in the console.</summary>
        private static string Explain(CraftingStatus status)
        {
            switch (status)
            {
                case CraftingStatus.UnknownRecipe:
                    return "Check the recipe Id and that it is listed in the database.";
                case CraftingStatus.IncompatibleMachine:
                    return "The machine does not provide the category this recipe requires.";
                case CraftingStatus.MissingInputs:
                    return "Not enough ingredients in the inventory.";
                case CraftingStatus.OutputBlocked:
                    return "The output container cannot fit the result.";
                default:
                    return string.Empty;
            }
        }

        // --- Inventory helpers ------------------------------------------------------

        /// <summary>
        /// Adds items by id, refusing anything the registry does not know.
        /// Silently accepting an unknown id would leave you with an inventory full of items
        /// no recipe can ever consume.
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

            Debug.Log("[TestBed] " + _inventory.ToString(_context.Items), this);
        }
    }
}