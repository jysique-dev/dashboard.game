using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Prueba visual de la sesion 8: pintar restricciones sobre el volumen en la escena.
    ///
    /// Se pinta una capa Y a la vez. Pintar en 3D con clicks planos es ambiguo, y elegir la
    /// capa explicitamente evita el error clasico de creer que has marcado el suelo cuando
    /// en realidad marcaste la celda de arriba.
    /// </summary>
    [CustomEditor(typeof(WfcConstraintPainter))]
    public class WfcConstraintPainterEditor : UnityEditor.Editor
    {
        private int layer;
        private WfcPaintMode brushMode = WfcPaintMode.AllowOnly;
        private WfcModuleDefinition brushModule;
        private int brushRotation = -1;
        private bool eraseMode;
        private bool paintingEnabled = true;

        private static readonly Color AllowColor = new Color(0.3f, 0.9f, 0.5f, 0.85f);
        private static readonly Color BanColor = new Color(0.95f, 0.35f, 0.3f, 0.85f);
        private static readonly Color EmptyColor = new Color(1f, 1f, 1f, 0.18f);

        public override void OnInspectorGUI()
        {
            var painter = (WfcConstraintPainter)target;
            var runner = painter.GetComponent<WfcRunner>();

            DrawBoundaries();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Pincel", EditorStyles.boldLabel);

            paintingEnabled = EditorGUILayout.Toggle("Pintar en escena", paintingEnabled);

            int maxLayer = runner != null && runner.IsPrepared ? runner.Solver.Topology.SizeY - 1 : 8;
            layer = EditorGUILayout.IntSlider("Capa Y", Mathf.Clamp(layer, 0, maxLayer), 0, maxLayer);

            brushMode = (WfcPaintMode)EditorGUILayout.EnumPopup("Modo", brushMode);
            brushModule = (WfcModuleDefinition)EditorGUILayout.ObjectField(
                "Modulo", brushModule, typeof(WfcModuleDefinition), false);

            brushRotation = EditorGUILayout.IntPopup(
                "Rotacion", brushRotation,
                new[] { "cualquiera", "rot 0", "rot 1", "rot 2", "rot 3" },
                new[] { -1, 0, 1, 2, 3 });

            eraseMode = EditorGUILayout.Toggle("Borrar al hacer click", eraseMode);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Limpiar capa"))
                {
                    Undo.RecordObject(painter, "Clear WFC layer");
                    painter.ClearLayer(layer);
                    EditorUtility.SetDirty(painter);
                }

                if (GUILayout.Button("Limpiar todo"))
                {
                    Undo.RecordObject(painter, "Clear WFC constraints");
                    painter.ClearAll();
                    EditorUtility.SetDirty(painter);
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"{painter.Painted.Count} celda(s) pintada(s)", EditorStyles.miniLabel);

            if (runner != null && !string.IsNullOrEmpty(runner.ConstraintMessage))
            {
                EditorGUILayout.HelpBox(
                    "Restricciones imposibles: " + runner.ConstraintMessage, MessageType.Error);
            }

            EditorGUILayout.Space(6);
            if (runner != null && GUILayout.Button("Preparar y ejecutar con estas restricciones"))
            {
                runner.Prepare();
                runner.RunToCompletion();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(8);
            DrawDefaultInspector();
        }

        private void DrawBoundaries()
        {
            EditorGUILayout.LabelField("Bordes del volumen", EditorStyles.boldLabel);

            var boundariesProp = serializedObject.FindProperty("boundaries");
            serializedObject.Update();

            for (int i = 0; i < boundariesProp.arraySize; i++)
            {
                var rule = boundariesProp.GetArrayElementAtIndex(i);
                var directionProp = rule.FindPropertyRelative("direction");
                var modeProp = rule.FindPropertyRelative("mode");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            WfcDirections.ShortName((WfcDirection)directionProp.enumValueIndex),
                            EditorStyles.miniBoldLabel, GUILayout.Width(30f));

                        EditorGUILayout.PropertyField(modeProp, GUIContent.none);
                    }

                    switch ((WfcBoundaryMode)modeProp.enumValueIndex)
                    {
                        case WfcBoundaryMode.RequireSocket:
                            EditorGUILayout.PropertyField(rule.FindPropertyRelative("outsideSocket"));
                            break;

                        case WfcBoundaryMode.AllowOnlyModules:
                            EditorGUILayout.PropertyField(rule.FindPropertyRelative("modules"), true);
                            break;
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            if (!paintingEnabled) return;

            var painter = (WfcConstraintPainter)target;
            var runner = painter.GetComponent<WfcRunner>();

            if (runner == null || runner.ModuleSet == null) return;

            var topology = runner.IsPrepared ? runner.Solver.Topology : null;
            if (topology == null)
            {
                Handles.Label(painter.transform.position, "Pulsa Preparar en el Runner para pintar.");
                return;
            }

            var space = runner.Space;
            float cell = runner.ModuleSet.CellSize;
            int y = Mathf.Clamp(layer, 0, topology.SizeY - 1);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int z = 0; z < topology.SizeZ; z++)
            {
                for (int x = 0; x < topology.SizeX; x++)
                {
                    var coords = new Vector3Int(x, y, z);
                    var position = space.CellToWorld(x, y, z);
                    var entry = painter.Find(coords);

                    Handles.color = entry == null
                        ? EmptyColor
                        : (entry.mode == WfcPaintMode.AllowOnly ? AllowColor : BanColor);

                    if (Handles.Button(
                            position, Quaternion.identity, cell * 0.35f, cell * 0.45f, Handles.CubeHandleCap))
                    {
                        Undo.RecordObject(painter, "Paint WFC constraint");

                        if (eraseMode) painter.Erase(coords);
                        else painter.PaintCell(coords, brushMode, brushModule, brushRotation);

                        EditorUtility.SetDirty(painter);
                    }

                    if (entry != null && entry.modules.Count > 0)
                    {
                        Handles.color = Color.white;
                        Handles.Label(position + Vector3.up * cell * 0.4f, entry.modules[0].name);
                    }
                }
            }

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10f, 10f, 260f, 60f), GUI.skin.box);
            GUILayout.Label($"Capa Y = {y}   ·   {(eraseMode ? "borrar" : brushMode.ToString())}");
            GUILayout.Label(brushModule != null ? brushModule.name : "sin modulo de pincel");
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}