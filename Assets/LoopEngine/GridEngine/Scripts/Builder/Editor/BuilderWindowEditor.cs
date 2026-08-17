using LoopEngine.AssetTool;
using LoopEngine.GridEngine.Placement;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.GridEngine.Builder.EditorTools
{
    public class BuilderWindowEditor : AssetTableWindow<PlaceableDefinition>
    {
        [MenuItem(LoopRoutes.BuilderToolRoute)]
        public static BuilderWindowEditor Open()
        {
            var w = GetWindow<BuilderWindowEditor>("Builder Tool");
            w.Focus();
            return w;
        }

        [SerializeField] private List<PlaceableDefinition> _items = new();
        [SerializeField] private CityLayoutAuthoring _authoring;

        protected override List<PlaceableDefinition> Items => _items;
        protected override string DropHint => "Arrastra aquí PlaceableDefinition del Project";

        private static readonly AssetColumn<PlaceableDefinition>[] _columns =
            {
            new() { Key = "id",        Title = "Id",         Width = 160, Text = p => p.id },
            new() { Key = "width",     Title = "Width",      Width = 60,  Text = p => p.width.ToString("0") },
            new() { Key = "height",    Title = "Height",     Width = 60,  Text = p => p.height.ToString("0") },
            new() { Key = "blocksMov", Title = "Blocks Mov", Width = 60,  Text = p => p.blocksMovement ? "Yes" : "No" },
        };

        protected override IReadOnlyList<AssetColumn<PlaceableDefinition>> Columns => _columns;

        public void SetTarget(CityLayoutAuthoring authoring) => _authoring = authoring;

        protected override void OnItemSelected(PlaceableDefinition item)
        {
            if (!_authoring) _authoring = FindFirstObjectByType<CityLayoutAuthoring>();
            if (!_authoring) return;

            Undo.RecordObject(_authoring, "Set Active Placeable");
            _authoring.activePlaceable = item;
            PrefabUtility.RecordPrefabInstancePropertyModifications(_authoring);
        }
    }
}

/*
[MenuItem(LoopRoutes.BuilderToolRoute)]
        private static void Open() => GetWindow<BuilderWindowEditor>("Builder Tool");

        
        public static BuilderWindowEditor Open2()
        {
            var w = GetWindow<BuilderWindowEditor>("Builder Tool");
            w.Focus();
            return w;
        }


        private CityLayoutAuthoring authoring;

        private CityLayoutAuthoring Authoring
        {
            get
            {
                if(authoring == null)
                {
                    authoring = FindFirstObjectByType<CityLayoutAuthoring>();
                    return authoring;
                }
                return authoring;
            }
        }


        private readonly List<PlaceableDefinition> _items = new();
        private MultiColumnListView _table;

        private readonly string _title_drop = "Arrastra aquí PlacementDefinition del Project";

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 6; root.style.paddingRight = 6; root.style.paddingTop = 6;

            // Zona de destino para soltar assets del Project
            var drop = new VisualElement
            {
                style = { height = 44, marginBottom = 6,
                          alignItems = Align.Center, justifyContent = Justify.Center,
                          backgroundColor = new StyleColor(new Color(0, 0, 0, 0.12f)) }
            };
            drop.Add(new Label(_title_drop));
            RegisterDrop(drop);
            root.Add(drop);

            // Tabla multi-columna, reordenable
            _table = new MultiColumnListView { reorderable = true, style = { flexGrow = 1 } };
            _table.columns.Add(new Column { name = "id", title = "Id", width = 160 });
            _table.columns.Add(new Column { name = "width", title = "Width", width = 60 });
            _table.columns.Add(new Column { name = "height", title = "Height", width = 60 });
            _table.columns.Add(new Column { name = "blocksMov", title = "Blocks Mov", width = 60 });

            _table.columns["id"].makeCell = () => new Label();
            _table.columns["id"].bindCell = (el, i) => ((Label)el).text = _items[i]? _items[i].id: "missing";
            _table.columns["width"].makeCell = () => new Label();
            _table.columns["width"].bindCell = (el, i) => ((Label)el).text = _items[i] ? _items[i].width.ToString("0"): "missing";
            _table.columns["height"].makeCell = () => new Label();
            _table.columns["height"].bindCell = (el, i) => ((Label)el).text = _items[i] ? _items[i].height.ToString("0") : "missing";

            _table.columns["blocksMov"].makeCell = () => new Label();
            _table.columns["blocksMov"].bindCell = (el, i) => ((Label)el).text = _items[i] ? _items[i].blocksMovement? "Yes":"No": "mission";

            //_table.columns["height"].bindCell = (el, i) => ((Label)el).text = (_items[i].efectos?.Count ?? 0).ToString();

            _table.itemsSource = _items;


            _table.selectionChanged += selection =>
            {
                if (Authoring == null) return;

                if (_table.selectedItem is PlaceableDefinition pd_object)
                {
                    Debug.Log($"Seleccionaste: {pd_object.id}");
                    Authoring.activePlaceable = pd_object;
                }
            };

            root.Add(_table);
        }

        private void RegisterDrop(VisualElement drop)
        {
            drop.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                // Aceptar solo si TODO lo arrastrado es una Habilidad
                bool ok = DragAndDrop.objectReferences.Length > 0 &&
                          Array.TrueForAll(DragAndDrop.objectReferences, o => o is PlaceableDefinition);
                DragAndDrop.visualMode = ok ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            });

            drop.RegisterCallback<DragPerformEvent>(_ =>
            {
                DragAndDrop.AcceptDrag();
                foreach (var o in DragAndDrop.objectReferences)
                    if (o is PlaceableDefinition _pd && !_items.Contains(_pd))
                        _items.Add(_pd);
                _table.RefreshItems();
            });
        }
    





*/