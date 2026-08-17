using UnityEditor;
using UnityEngine;

namespace LoopEngine.CameraEngine.CameraController.EditorScript
{
    /// <summary>
    /// Lógica compartida de dibujo/edición de un CameraBounds en la vista de
    /// escena. Se usa desde DOS editores distintos (ver CameraBoundsEditors.cs):
    /// al seleccionar el asset CameraBounds, o al seleccionar el CameraRig que
    /// lo referencia. Así se puede editar el límite desde cualquiera de los dos.
    /// </summary>
    public static class CameraBoundsHandles
    {
        private static readonly Color Outline = new Color(0.20f, 0.85f, 1f, 1f);
        private static readonly Color Fill = new Color(0.20f, 0.85f, 1f, 0.06f);
        private static readonly Color InnerLine = new Color(1f, 0.75f, 0.2f, 0.9f);
        private static readonly Color CornerCol = new Color(1f, 0.95f, 0.3f, 1f);
        private static readonly Color CenterCol = new Color(0.4f, 1f, 0.5f, 1f);

        /// <summary>
        /// Dibuja el rectángulo y los handles. Aplica los cambios al asset con
        /// soporte de Undo. Llamar desde OnSceneGUI.
        /// </summary>
        public static void DrawEditable(CameraBounds b)
        {
            if (b == null) return;

            float y = b.drawHeight;
            float minX = b.MinX, maxX = b.MaxX;
            float minZ = b.MinZ, maxZ = b.MaxZ;

            Vector3 bl = new Vector3(minX, y, minZ);
            Vector3 br = new Vector3(maxX, y, minZ);
            Vector3 tr = new Vector3(maxX, y, maxZ);
            Vector3 tl = new Vector3(minX, y, maxZ);

            // Área permitida (relleno + borde).
            Handles.DrawSolidRectangleWithOutline(new[] { bl, br, tr, tl }, Fill, Outline);

            // Previsualización del padding (rectángulo interno).
            if (b.padding > 0f)
            {
                float p = b.padding;
                Vector3 i0 = new Vector3(minX + p, y, minZ + p);
                Vector3 i1 = new Vector3(maxX - p, y, minZ + p);
                Vector3 i2 = new Vector3(maxX - p, y, maxZ - p);
                Vector3 i3 = new Vector3(minX + p, y, maxZ - p);
                Handles.color = InnerLine;
                Handles.DrawAAPolyLine(2f, i0, i1, i2, i3, i0);
            }

            // --- Handles de esquina: cada uno mueve sus dos bordes ---
            Handles.color = CornerCol;

            EditorGUI.BeginChangeCheck();
            Vector3 nbl = Corner(bl);
            if (EditorGUI.EndChangeCheck()) { ApplyEdges(b, nbl.x, maxX, nbl.z, maxZ); return; }

            EditorGUI.BeginChangeCheck();
            Vector3 nbr = Corner(br);
            if (EditorGUI.EndChangeCheck()) { ApplyEdges(b, minX, nbr.x, nbr.z, maxZ); return; }

            EditorGUI.BeginChangeCheck();
            Vector3 ntr = Corner(tr);
            if (EditorGUI.EndChangeCheck()) { ApplyEdges(b, minX, ntr.x, minZ, ntr.z); return; }

            EditorGUI.BeginChangeCheck();
            Vector3 ntl = Corner(tl);
            if (EditorGUI.EndChangeCheck()) { ApplyEdges(b, ntl.x, maxX, minZ, ntl.z); return; }

            // --- Handle central: mueve todo el rectángulo ---
            Vector3 c = new Vector3(b.center.x, y, b.center.y);
            Handles.color = CenterCol;
            EditorGUI.BeginChangeCheck();
            Vector3 nc = Handles.FreeMoveHandle(
                c, HandleUtility.GetHandleSize(c) * 0.09f, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(b, "Move Camera Bounds");
                Vector3 d = nc - c;
                b.center += new Vector2(d.x, d.z);
                EditorUtility.SetDirty(b);
                return;
            }

            Handles.color = Color.white;
            Handles.Label(c, $"Camera Bounds\n{b.size.x:0} x {b.size.y:0}");
        }

        private static Vector3 Corner(Vector3 p)
        {
            return Handles.FreeMoveHandle(
                p, HandleUtility.GetHandleSize(p) * 0.06f, Vector3.zero, Handles.DotHandleCap);
        }

        private static void ApplyEdges(CameraBounds b, float minX, float maxX, float minZ, float maxZ)
        {
            Undo.RecordObject(b, "Resize Camera Bounds");
            b.center = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            b.size = new Vector2(Mathf.Abs(maxX - minX), Mathf.Abs(maxZ - minZ));
            EditorUtility.SetDirty(b);
        }
    }
}