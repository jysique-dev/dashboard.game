using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Prueba visual de la sesion 3: instancia en escena todas las variantes horneadas
    /// de un set, una fila por modulo y una columna por rotacion, con los sockets pintados
    /// en cada cara.
    ///
    /// Si una rotacion esta bien horneada, el color de la cara acompana al giro de la
    /// geometria. Si la formula estuviera invertida, los colores se quedarian quietos
    /// mientras el prefab gira: es un error que se ve de inmediato.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("LoopEngine/WFC/Variant Preview")]
    public class WfcVariantPreview : MonoBehaviour
    {
        [SerializeField] private WfcModuleSet set;

        [Tooltip("Opcional: previsualizar solo este modulo del set.")]
        [SerializeField] private WfcModuleDefinition onlyModule;

        [Tooltip("Separacion entre celdas, en multiplos del cellSize del set.")]
        [SerializeField, Min(1f)] private float spacing = 1.6f;

        [SerializeField] private bool dropDuplicateRotations = true;

        [Header("Gizmos")]
        [SerializeField] private bool drawCellBounds = true;
        [SerializeField] private bool drawSocketFaces = true;
        [SerializeField, Range(0.05f, 0.45f)] private float faceSize = 0.3f;

        private readonly List<WfcValidationIssue> issues = new List<WfcValidationIssue>();
        private readonly List<WfcBakeReportEntry> report = new List<WfcBakeReportEntry>();

        private WfcBakedSet baked;
        private readonly List<int> shownVariants = new List<int>();

        public WfcModuleSet Set => set;
        public WfcBakedSet Baked => baked;
        public IReadOnlyList<WfcValidationIssue> Issues => issues;
        public IReadOnlyList<WfcBakeReportEntry> Report => report;
        public IReadOnlyList<int> ShownVariants => shownVariants;

        public float CellSize => baked != null ? baked.CellSize : 1f;

        private void OnEnable() => Rebuild();

        public void Rebuild()
        {
            ClearChildren();

            issues.Clear();
            report.Clear();
            shownVariants.Clear();
            baked = null;

            if (set == null) return;

            baked = WfcModuleBaker.Bake(set, dropDuplicateRotations, issues, report);
            if (baked == null) return;

            float step = CellSize * spacing;

            // Una fila por modulo de autoria, una columna por rotacion conservada.
            int row = 0;
            int lastSource = -1;
            int column = 0;

            for (int variantIndex = 0; variantIndex < baked.Count; variantIndex++)
            {
                var view = baked.GetView(variantIndex);

                if (onlyModule != null && view.Module != onlyModule) continue;

                int source = baked.GetVariant(variantIndex).SourceIndex;
                if (source != lastSource)
                {
                    if (lastSource >= 0) row++;
                    lastSource = source;
                    column = 0;
                }

                var position = transform.position + new Vector3(column * step, 0f, -row * step);
                Spawn(view, position);

                shownVariants.Add(variantIndex);
                column++;
            }
        }

        /// <summary>Posicion de mundo de la variante n-esima de las mostradas.</summary>
        public Vector3 GetSlotPosition(int slot)
        {
            float step = CellSize * spacing;

            int row = 0;
            int column = 0;
            int lastSource = -1;

            for (int i = 0; i < shownVariants.Count; i++)
            {
                int source = baked.GetVariant(shownVariants[i]).SourceIndex;
                if (source != lastSource)
                {
                    if (lastSource >= 0) row++;
                    lastSource = source;
                    column = 0;
                }

                if (i == slot) return transform.position + new Vector3(column * step, 0f, -row * step);
                column++;
            }

            return transform.position;
        }

        private void Spawn(WfcBakedVariant view, Vector3 position)
        {
            if (view.IsAir) return;

            var instance = Instantiate(view.Prefab, position, view.Rotation, transform);
            instance.name = view.DisplayName;

            // Las instancias son desechables: no se guardan en la escena ni se seleccionan.
            instance.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;

                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private void OnDrawGizmos()
        {
            if (baked == null) return;

            float cell = CellSize;

            for (int slot = 0; slot < shownVariants.Count; slot++)
            {
                int variantIndex = shownVariants[slot];
                var center = GetSlotPosition(slot);

                if (drawCellBounds)
                {
                    Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
                    Gizmos.DrawWireCube(center, Vector3.one * cell);
                }

                if (!drawSocketFaces) continue;

                for (int i = 0; i < WfcDirections.Count; i++)
                {
                    var direction = (WfcDirection)i;
                    var offset = WfcDirections.Offset(direction);
                    var normal = new Vector3(offset.X, offset.Y, offset.Z);

                    Gizmos.color = baked.GetSocketColor(variantIndex, direction);

                    var faceCenter = center + normal * (cell * 0.5f);
                    var size = Vector3.one * (cell * faceSize);

                    // Aplasta el cubo contra el eje de la cara para que parezca una placa.
                    size.x *= Mathf.Approximately(Mathf.Abs(normal.x), 1f) ? 0.12f : 1f;
                    size.y *= Mathf.Approximately(Mathf.Abs(normal.y), 1f) ? 0.12f : 1f;
                    size.z *= Mathf.Approximately(Mathf.Abs(normal.z), 1f) ? 0.12f : 1f;

                    Gizmos.DrawCube(faceCenter, size);
                }
            }
        }

        private void OnValidate()
        {
            if (spacing < 1f) spacing = 1f;

#if UNITY_EDITOR
            // Rebuild destruye hijos, y eso no se puede hacer dentro de OnValidate.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (!isActiveAndEnabled) return;
                Rebuild();
            };
#endif
        }
    }
}