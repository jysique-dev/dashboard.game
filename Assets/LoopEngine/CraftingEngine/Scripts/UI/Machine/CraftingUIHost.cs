using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// The scene-side entry point: holds the <see cref="UIDocument"/>, builds the context and
    /// the panel, and drives them once per frame.
    /// </summary>
    /// <remarks>
    /// It does not create a <see cref="CraftingSystem"/> and does not look for one. Game code
    /// owns the system and calls <see cref="Bind"/>, because the UI has no business deciding
    /// how the crafting system is constructed or how long it lives.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("LoopEngine/Crafting/Crafting UI Host")]
    public sealed class CraftingUIHost : MonoBehaviour
    {
        [SerializeField, Tooltip("Document the panel is attached to. Taken from this object when empty.")]
        private UIDocument _document;

        [SerializeField, Tooltip("Crafting database to read the item list from. Takes precedence over the manual list below.")]
        private CraftingDatabase _database;

        [SerializeField, Tooltip("Manual item list for the default display provider. Used only when no database is assigned, and ignored entirely when you supply your own provider.")]
        private List<ItemDefinition> _itemCatalog = new List<ItemDefinition>();

        [SerializeField, Tooltip("Hide the whole panel when no machine is selected.")]
        private bool _hideWhenClosed = true;

        [SerializeField, Min(0f), Tooltip("Seconds between progress samples. 0.033 is about 30 a second.")]
        private float _progressInterval = 1f / 30f;

        [SerializeField, Min(0f), Tooltip("Seconds between container samples: item counts and what is affordable.")]
        private float _containerInterval = 0.25f;

        private CraftingUIContext _context;
        private MachinePanel _panel;
        private VisualElement _root;

        /// <summary>Null until <see cref="Bind"/> is called.</summary>
        public CraftingUIContext Context => _context;

        /// <summary>The panel, or null before <see cref="Bind"/>. Use it to reach the sections.</summary>
        public MachinePanel Panel => _panel;

        /// <summary>True while a machine is being shown.</summary>
        public bool IsOpen => _context != null && _context.Machines.HasMachine;

        /// <summary>
        /// The item list backing the default display provider: the database when one is
        /// assigned, the manual list otherwise.
        /// </summary>
        public IReadOnlyList<ItemDefinition> ResolveItems()
            => _database != null ? _database.Items : (IReadOnlyList<ItemDefinition>)_itemCatalog;

        private void Reset() => _document = GetComponent<UIDocument>();

        private void Awake()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
        }

        /// <summary>
        /// Attaches the UI to a crafting system. Call once, from whatever owns the system.
        /// Pass a provider to control names and icons; omit it to use asset names.
        /// </summary>
        public CraftingUIContext Bind(CraftingSystem system, ICraftingDisplayProvider display = null, CraftingUITheme theme = null)
        {
            if (system == null)
            {
                Debug.LogError("CraftingUIHost.Bind was given no crafting system.", this);
                return null;
            }

            Unbind();

            _context = new CraftingUIContext(
                system,
                display ?? new DefaultCraftingDisplayProvider(ResolveItems()),
                theme);

            _context.Progress.Interval = _progressInterval;
            _context.Containers.Interval = _containerInterval;

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogError("CraftingUIHost has no UIDocument root. Assign a Panel Settings asset on the UIDocument.", this);
                return _context;
            }

            _panel = new MachinePanel(_context);
            _panel.style.position = Position.Absolute;
            _panel.style.right = 24f;
            _panel.style.top = 24f;
            _panel.style.bottom = 24f;
            _panel.style.width = 380f;
            _root.Add(_panel);

            ApplyVisibility();
            return _context;
        }

        /// <summary>Points the panel at a machine and shows it.</summary>
        public void Show(MachineInstance machine)
        {
            if (_context == null)
            {
                Debug.LogError("CraftingUIHost.Show called before Bind.", this);
                return;
            }

            _context.Machines.Select(machine);
            _context.SetSamplingPaused(false);
            ApplyVisibility();
        }

        /// <summary>Clears the selection and stops sampling.</summary>
        public void Hide()
        {
            if (_context == null)
                return;

            _context.Machines.Select(null);
            _context.SetSamplingPaused(true);
            ApplyVisibility();
        }

        /// <summary>Adds an item to the built-in display provider's catalogue at runtime.</summary>
        public void RegisterItem(ItemDefinition item)
        {
            if (item == null)
                return;

            if (_context != null && _context.Display is DefaultCraftingDisplayProvider provider)
                provider.Register(item);
            else if (!_itemCatalog.Contains(item))
                _itemCatalog.Add(item);
        }

        private void ApplyVisibility()
        {
            if (_panel == null)
                return;

            bool visible = !_hideWhenClosed || IsOpen;
            _panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Update()
        {
            // Unscaled, so the panel keeps animating while the game is paused behind it.
            _context?.Tick(Time.unscaledDeltaTime);
        }

        private void OnDisable() => _context?.SetSamplingPaused(true);

        private void OnEnable() => _context?.SetSamplingPaused(!IsOpen);

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (_panel != null)
            {
                _panel.Dispose();
                _panel.RemoveFromHierarchy();
                _panel = null;
            }

            _context?.Dispose();
            _context = null;
        }

        private void OnValidate()
        {
            if (_context == null)
                return;

            _context.Progress.Interval = _progressInterval;
            _context.Containers.Interval = _containerInterval;
        }
    }
}