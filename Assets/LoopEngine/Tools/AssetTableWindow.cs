using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoopEngine.AssetTool
{
    public sealed class AssetColumn<T>
    {
        public string Key;
        public string Title;
        public float Width = 100f;
        public Func<T, string> Text;
    }
    public abstract class AssetTableWindow<T> : EditorWindow where T : UnityEngine.Object
    {
        // La subclase concreta es la dueña de la lista (ver nota al final)
        protected abstract List<T> Items { get; }
        protected abstract IReadOnlyList<AssetColumn<T>> Columns { get; }

        protected virtual string DropHint => $"Arrastra aquí assets de tipo {typeof(T).Name}";
        protected virtual void OnItemSelected(T item) { }

        private MultiColumnListView _table;

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 6; root.style.paddingRight = 6; root.style.paddingTop = 6;

            root.Add(DropZone());
            root.Add(BuildTable());

            Items.RemoveAll(x => !x);   // limpia assets borrados
            _table.RefreshItems();
        }

        private VisualElement DropZone()
        {
            var drop = new VisualElement
            {
                style = { height = 44, marginBottom = 6,
                          alignItems = Align.Center, justifyContent = Justify.Center,
                          backgroundColor = new StyleColor(new Color(0, 0, 0, 0.12f)) }
            };
            drop.Add(new Label(DropHint));

            drop.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                bool ok = DragAndDrop.objectReferences.Length > 0 &&
                          Array.TrueForAll(DragAndDrop.objectReferences, o => o is T);
                DragAndDrop.visualMode = ok ? DragAndDropVisualMode.Copy
                                            : DragAndDropVisualMode.Rejected;
            });

            drop.RegisterCallback<DragPerformEvent>(_ =>
            {
                DragAndDrop.AcceptDrag();
                foreach (var o in DragAndDrop.objectReferences)
                    if (o is T item && !Items.Contains(item))
                        Items.Add(item);
                _table.RefreshItems();
            });

            return drop;
        }

        private VisualElement BuildTable()
        {
            _table = new MultiColumnListView { reorderable = true, style = { flexGrow = 1 } };

            foreach (var col in Columns)
            {
                var def = col; // captura local
                _table.columns.Add(new Column { name = def.Key, title = def.Title, width = def.Width });
                _table.columns[def.Key].makeCell = () => new Label();
                _table.columns[def.Key].bindCell = (el, i) =>
                    ((Label)el).text = Items[i] ? def.Text(Items[i]) : "<missing>";
            }

            _table.itemsSource = Items;

            _table.selectionChanged += selection =>
            {
                foreach (var obj in selection)
                    if (obj is T item) { OnItemSelected(item); break; }
            };

            return _table;
        }
    }
}