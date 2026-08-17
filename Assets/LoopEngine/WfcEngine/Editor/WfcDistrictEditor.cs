using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Prueba visual de la sesion 10: colocar edificios haciendo click sobre el plano del
    /// distrito y ver como cada uno se adapta a las fachadas de los que ya estaban.
    ///
    /// Usa el feed manual, no GridEngine, a proposito: si la costura falla, se sabe que el
    /// problema esta en el WFC y no en la integracion.
    /// </summary>
    [CustomEditor(typeof(WfcDistrict))]
    public class WfcDistrictEditor : UnityEditor.Editor
    {
        private bool placing = true;
        private bool eraseMode;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var district = (WfcDistrict)target;
            var feed = district.GetComponent<WfcManualPlacementFeed>();

            EditorGUILayout.Space(8);

            if (feed == null)
            {
                EditorGUILayout.HelpBox(
                    "Anade un Manual Placement Feed para colocar desde el editor, " +
                    "o un Grid Placement Adapter para que lo haga GridEngine.",
                    MessageType.Info);
            }
            else
            {
                district.EnsureSubscribed();

                if (!district.HasFeed)
                {
                    EditorGUILayout.HelpBox(
                        "El distrito no esta escuchando al feed. Desactiva y reactiva el " +
                        "componente, o comprueba que ambos estan en el mismo GameObject.",
                        MessageType.Error);
                }

                DrawPalette(feed);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(district.DescribeSeams(), EditorStyles.miniBoldLabel);

            CheckCellSize(district);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Re-resolver todo")) district.ResolveAll();
                if (GUILayout.Button("Vaciar barrio")) district.Clear();
            }
        }

        private void DrawPalette(WfcManualPlacementFeed feed)
        {
            placing = EditorGUILayout.Toggle("Colocar en escena", placing);
            eraseMode = EditorGUILayout.Toggle("Borrar al hacer click", eraseMode);

            if (feed.Palette.Count == 0)
            {
                EditorGUILayout.HelpBox("La paleta del feed esta vacia.", MessageType.Warning);
                return;
            }

            var labels = new string[feed.Palette.Count];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = feed.Palette[i] != null ? feed.Palette[i].name : "(vacio)";
            }

            feed.Selected = EditorGUILayout.Popup("Edificio", feed.Selected, labels);
        }

        /// <summary>
        /// El tamano de celda del distrito y el de los sets tienen que coincidir. Si no,
        /// los edificios encajan logicamente pero quedan separados o solapados en pantalla,
        /// que es un sintoma confuso porque la costura si esta bien resuelta.
        /// </summary>
        private void CheckCellSize(WfcDistrict district)
        {
            var feed = district.GetComponent<WfcManualPlacementFeed>();
            if (feed == null) return;

            foreach (var building in feed.Palette)
            {
                if (building == null || building.ModuleSet == null) continue;

                if (!Mathf.Approximately(building.CellSize, district.CellSize))
                {
                    EditorGUILayout.HelpBox(
                        $"'{building.name}' usa celdas de {building.CellSize} y el distrito de " +
                        $"{district.CellSize}. Los volumenes no se alinearan en pantalla.",
                        MessageType.Warning);
                }
            }
        }

        private void OnSceneGUI()
        {
            if (!placing) return;

            var district = (WfcDistrict)target;
            var feed = district.GetComponent<WfcManualPlacementFeed>();
            if (feed == null) return;

            // Por si el feed se anadio despues que el distrito: anadir un componente no
            // vuelve a disparar el OnEnable de sus hermanos.
            district.EnsureSubscribed();

            var plane = new Plane(Vector3.up, district.transform.position);
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            if (!plane.Raycast(ray, out float distance)) return;

            var point = ray.GetPoint(distance);
            var cell = district.WorldToCell(point);
            cell.y = 0;

            var center = district.CellToWorld(cell);

            Handles.color = eraseMode
                ? new Color(0.95f, 0.35f, 0.3f, 0.9f)
                : new Color(0.35f, 0.85f, 0.5f, 0.9f);

            Handles.DrawWireCube(center, Vector3.one * district.CellSize);

            var definition = feed.Current;
            if (definition != null && !eraseMode)
            {
                var size = new Vector3(
                    definition.Size.x, definition.Size.y, definition.Size.z) * district.CellSize;

                var footprintCenter = center + new Vector3(
                    (definition.Size.x - 1) * 0.5f,
                    (definition.Size.y - 1) * 0.5f,
                    (definition.Size.z - 1) * 0.5f) * district.CellSize;

                Handles.color = new Color(0.35f, 0.85f, 0.5f, 0.35f);
                Handles.DrawWireCube(footprintCenter, size);

                Handles.Label(center + Vector3.up * district.CellSize, definition.name);
            }

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                !Event.current.alt)
            {
                Undo.RegisterFullObjectHierarchyUndo(district.gameObject, "Place WFC building");

                if (eraseMode) feed.RemoveAt(cell);
                else feed.PlaceAt(cell);

                Event.current.Use();
                SceneView.RepaintAll();
                Repaint();
            }

            // Sin esto, el click seleccionaria objetos de la escena en vez de colocar.
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
    }
}