using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Etiquetas en escena para la previsualizacion de variantes: nombre y rotacion sobre
    /// cada celda, y la notacion del socket sobre cada cara.
    ///
    /// Los gizmos de color los pinta el propio componente (se ven sin seleccionarlo);
    /// aqui van solo los Handles, que requieren seleccion.
    /// </summary>
    [CustomEditor(typeof(WfcVariantPreview))]
    public class WfcVariantPreviewEditor : UnityEditor.Editor
    {
        private static GUIStyle labelStyle;
        private static GUIStyle faceStyle;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var preview = (WfcVariantPreview)target;

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Rehornear"))
            {
                preview.Rebuild();
                SceneView.RepaintAll();
            }

            var baked = preview.Baked;
            if (baked == null)
            {
                EditorGUILayout.HelpBox("Asigna un Module Set para previsualizar.", MessageType.Info);
                return;
            }

            int dropped = 0;
            foreach (var entry in preview.Report)
            {
                if (!entry.Kept) dropped++;
            }

            EditorGUILayout.LabelField(
                $"{baked.Count} variantes horneadas · {preview.ShownVariants.Count} mostradas · " +
                $"{dropped} rotacion(es) descartada(s) por duplicada",
                EditorStyles.miniBoldLabel);

            if (dropped > 0)
            {
                EditorGUILayout.HelpBox(
                    "Una rotacion descartada significa que el modulo es simetrico respecto a ese giro: " +
                    "sus 6 sockets quedan identicos. Es correcto y ahorra entradas en la matriz de vecindad. " +
                    "Si esperabas 4 variantes distintas, revisa si los sockets deberian ser asimetricos.",
                    MessageType.Info);
            }

            foreach (var issue in preview.Issues)
            {
                if (issue.Severity == WfcIssueSeverity.Info) continue;
                EditorGUILayout.HelpBox(
                    issue.Message, WfcModuleDefinitionEditor.ToMessageType(issue.Severity));
            }
        }

        private void OnSceneGUI()
        {
            var preview = (WfcVariantPreview)target;
            var baked = preview.Baked;
            if (baked == null) return;

            EnsureStyles();

            float cell = preview.CellSize;

            for (int slot = 0; slot < preview.ShownVariants.Count; slot++)
            {
                int variantIndex = preview.ShownVariants[slot];
                var center = preview.GetSlotPosition(slot);
                var view = baked.GetView(variantIndex);

                Handles.color = Color.white;
                Handles.Label(center + Vector3.up * (cell * 0.85f), view.DisplayName, labelStyle);

                for (int i = 0; i < WfcDirections.Count; i++)
                {
                    var direction = (WfcDirection)i;
                    var offset = WfcDirections.Offset(direction);
                    var normal = new Vector3(offset.X, offset.Y, offset.Z);

                    var descriptor = baked.GetSocket(variantIndex, direction);
                    var faceCenter = center + normal * (cell * 0.62f);

                    faceStyle.normal.textColor = baked.GetSocketColor(variantIndex, direction);
                    Handles.Label(faceCenter, descriptor.Notation(), faceStyle);
                }
            }
        }

        private static void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                labelStyle.normal.textColor = Color.white;
            }

            if (faceStyle == null)
            {
                faceStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }
        }
    }
}