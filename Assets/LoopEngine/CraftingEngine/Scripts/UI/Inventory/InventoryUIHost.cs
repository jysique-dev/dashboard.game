using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// Scene entry point for the inventory UI: builds the context, draws one grid, and drives
    /// the samplers.
    /// </summary>
    /// <remarks>
    /// Deliberately independent from <see cref="CraftingUIHost"/>. A game can show an
    /// inventory with no crafting window anywhere, and the two only meet when a machine is
    /// opened. Pass an existing context to <see cref="Bind"/> to share one catalogue and one
    /// display provider between both.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("LoopEngine/Crafting/Inventory UI Host")]
    public sealed class InventoryUIHost : MonoBehaviour
    {
        [SerializeField, Tooltip("Document the grid is attached to. Taken from this object when empty.")]
        private UIDocument _document;

        [SerializeField, Tooltip("Crafting database to read the item list from. Takes precedence over the manual list below.")]
        private CraftingDatabase _database;

        [SerializeField, Tooltip("Manual item list, used only when no database is assigned. Contents outside it are invisible to the UI.")]
        private List<ItemDefinition> _itemCatalogue = new List<ItemDefinition>();

        [SerializeField, Tooltip("Heading above the grid.")]
        private string _title = "Inventory";

        [SerializeField, Min(24f), Tooltip("Cell size in pixels.")]
        private float _cellSize = 56f;

        [SerializeField, Min(0), Tooltip("Cells drawn even when the container holds less, so the layout stays still.")]
        private int _minCells = 12;

        [SerializeField, Min(0f), Tooltip("Seconds between container samples.")]
        private float _sampleInterval = 0.25f;

        [SerializeField, Min(0), Tooltip("Items examined per sample. 0 scans the whole catalogue every time.")]
        private int _maxScansPerSample;

        private InventoryUIContext _context;
        private InventoryGridView _grid;
        private ItemContainerAdapter _adapter;

        /// <summary>Null until <see cref="Bind"/> is called.</summary>
        public InventoryUIContext Context => _context;

        /// <summary>The grid, or null before <see cref="Bind"/>.</summary>
        public InventoryGridView Grid => _grid;

        /// <summary>The container being watched, or null.</summary>
        public ItemContainerAdapter Adapter => _adapter;

        /// <summary>
        /// The item list this host will use: the database when one is assigned, the manual
        /// list otherwise. The database is where the assets already live, so keeping a second
        /// list by hand only creates a way for the two to disagree.
        /// </summary>
        public IReadOnlyList<ItemDefinition> ResolveItems()
            => _database != null ? _database.Items : (IReadOnlyList<ItemDefinition>)_itemCatalogue;

        private void Reset() => _document = GetComponent<UIDocument>();

        private void Awake()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
        }

        /// <summary>
        /// Builds the UI around a container. Pass a context to share the catalogue and the
        /// display provider with an already running crafting UI.
        /// </summary>
        public InventoryUIContext Bind(IItemContainer container, InventoryUIContext context = null)
        {
            if (container == null)
            {
                Debug.LogError("InventoryUIHost.Bind was given no container.", this);
                return null;
            }

            Unbind();

            _context = context ?? InventoryUIContext.FromItems(ResolveItems());

            _adapter = _context.Track(container, _sampleInterval);
            _adapter.MaxScansPerSample = _maxScansPerSample;

            VisualElement root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogError("InventoryUIHost has no UIDocument root. Assign a Panel Settings asset on the UIDocument.", this);
                return _context;
            }

            _grid = new InventoryGridView(_context, _title, _cellSize) { MinCells = _minCells };
            _grid.style.position = Position.Absolute;
            _grid.style.left = 24f;
            _grid.style.bottom = 24f;
            _grid.style.width = _cellSize * 6f + 48f;
            _grid.style.backgroundColor = _context.Theme.Background;
            _grid.style.paddingLeft = _context.Theme.SpacingLoose;
            _grid.style.paddingRight = _context.Theme.SpacingLoose;
            _grid.style.paddingTop = _context.Theme.SpacingLoose;
            _grid.style.paddingBottom = _context.Theme.Spacing;
            CraftingUIStyle.SetRadius(_grid, _context.Theme.CornerRadius);
            CraftingUIStyle.SetBorder(_grid, _context.Theme.Border, _context.Theme.BorderWidth);

            _grid.Bind(_adapter);
            root.Add(_grid);

            return _context;
        }

        /// <summary>Shows or hides the grid without dropping the sampling.</summary>
        public void SetVisible(bool visible)
        {
            if (_grid != null)
                _grid.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            _context?.SetSamplingPaused(!visible);
        }

        private void Update()
        {
            _context?.Tick(Time.unscaledDeltaTime);
        }

        private void OnDisable() => _context?.SetSamplingPaused(true);

        private void OnEnable() => _context?.SetSamplingPaused(false);

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (_grid != null)
            {
                _grid.Dispose();
                _grid.RemoveFromHierarchy();
                _grid = null;
            }

            if (_context != null && _adapter != null)
                _context.Untrack(_adapter);

            _adapter = null;

            // The context may be shared with a crafting UI, so it is not disposed here.
            _context = null;
        }
    }
}