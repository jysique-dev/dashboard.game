using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Dibujo compartido de miniaturas y chapas de socket.
    ///
    /// La miniatura del prefab no puede mostrar la rotacion de la variante: AssetPreview
    /// devuelve una imagen fija del asset. La rotacion se indica con una chapa "r2" en la
    /// esquina; para ver la geometria girada de verdad esta la escena de la sesion 3.
    /// </summary>
    internal static class WfcPreviewUtility
    {
        /// <summary>true si hay algun preview cargandose y conviene repintar la ventana.</summary>
        public static bool DrawVariantThumbnail(Rect rect, WfcBakedSet baked, int variantIndex)
        {
            var view = baked.GetView(variantIndex);
            bool loading = false;

            if (view.Prefab == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.25f, 0.6f));
                EditorGUI.LabelField(rect, "AIRE", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                var preview = AssetPreview.GetAssetPreview(view.Prefab);

                if (preview != null)
                {
                    GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
                    EditorGUI.LabelField(rect, "...", EditorStyles.centeredGreyMiniLabel);
                    loading = AssetPreview.IsLoadingAssetPreview(view.Prefab.GetInstanceID());
                }
            }

            if (view.RotationSteps != 0)
            {
                var badge = new Rect(rect.x + 2f, rect.y + 2f, 20f, 14f);
                EditorGUI.DrawRect(badge, new Color(0f, 0f, 0f, 0.65f));
                EditorGUI.LabelField(badge, $" r{view.RotationSteps}", EditorStyles.miniLabel);
            }

            return loading;
        }

        /// <summary>Chapa de color con la notacion del socket de una cara.</summary>
        public static void DrawSocketChip(
            Rect rect, WfcBakedSet baked, int variantIndex, WfcDirection direction, bool showFaceName)
        {
            var color = baked.GetSocketColor(variantIndex, direction);
            var descriptor = baked.GetSocket(variantIndex, direction);

            EditorGUI.DrawRect(rect, color * new Color(1f, 1f, 1f, 0.35f));

            var strip = new Rect(rect.x, rect.y, 3f, rect.height);
            EditorGUI.DrawRect(strip, color);

            string label = showFaceName
                ? $" {WfcDirections.ShortName(direction)}  {descriptor.Notation()}"
                : $" {descriptor.Notation()}";

            EditorGUI.LabelField(rect, label, EditorStyles.miniLabel);
        }

        public static void DrawOutline(Rect rect, Color color, float thickness = 2f)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }
    }
}