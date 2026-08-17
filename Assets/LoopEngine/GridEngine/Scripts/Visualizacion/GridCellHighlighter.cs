using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoopEngine.GridEngine.Visualization
{
    /// <summary>
    /// Resalta una o varias celdas en runtime creando quads ajustados a la grilla.
    ///
    /// Usa un pool de quads y un MaterialPropertyBlock para cambiar el color SIN crear
    /// instancias de material por celda (renderer.material sí crearía una instancia nueva
    /// cada vez). El MPB se reutiliza en todas las llamadas.
    ///
    /// Este componente solo sabe DIBUJAR resaltados. La Sesión 3 (input) decidirá QUÉ celda
    /// resaltar según el mouse; la Sesión 4 (colocación) resaltará huellas de edificios.
    /// </summary>
    public class GridCellHighlighter : MonoBehaviour
    {
        [Tooltip("Material de los quads. Usa un shader Unlit/Transparent de tu pipeline " +
                 "(URP: 'Universal Render Pipeline/Unlit'). Si tu cámara puede ver el quad por " +
                 "debajo, pon 'Render Face = Both' en el material.")]
        [SerializeField] private Material highlightMaterial;

        [Tooltip("Separación sobre el plano para evitar z-fighting con el suelo.")]
        [SerializeField] private float surfaceOffset = 0.01f;

        [SerializeField] private ColorEngine.PaletteColorReference defaultColor;

        private IReadOnlyGrid _grid;
        private readonly List<MeshRenderer> _pool = new();
        private MaterialPropertyBlock _mpb;
        private Mesh _quad;

        // IDs de propiedades de color: _BaseColor (URP/HDRP, nombre reservado del color principal)
        // y _Color (Built-in). Fijar ambos hace el componente compatible con las dos pipelines.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _quad = BuildQuadMesh();

            if (highlightMaterial == null)
                Debug.LogWarning("[GridCellHighlighter] Sin material asignado: los quads se verán en magenta.", this);

            // Toma la grilla del IGridProvider del mismo GameObject o de un padre, si existe.
            var provider = GetComponentInParent<IGridProvider>();
            if (provider != null)
            {
                if (provider.Grid != null) _grid = provider.Grid;
                provider.OnGridReady += g => _grid = g;
            }
        }

        private void OnDestroy()
        {
            if (_quad != null) Destroy(_quad);
        }

        /// <summary>Inyección manual de la grilla si no se usa IGridProvider.</summary>
        public void SetGrid(IReadOnlyGrid grid) => _grid = grid;

        public void HighlightCell(Vector2Int cell) => Highlight(new[] { cell }, defaultColor.Value);

        public void HighlightCell(Vector2Int cell, Color color) => Highlight(new[] { cell }, color);

        /// <summary>Resalta un conjunto de celdas con un color. Reutiliza el pool y oculta lo sobrante.</summary>
        public void Highlight(IEnumerable<Vector2Int> cells, Color color)
        {
            if (_grid == null) return;

            int used = 0;
            foreach (Vector2Int cell in cells)
            {
                if (!_grid.IsInside(cell)) continue;
                MeshRenderer r = GetOrCreate(used++);
                PlaceOnCell(r.transform, cell);
                ApplyColor(r, color);
                if (!r.gameObject.activeSelf) r.gameObject.SetActive(true);
            }

            // Desactiva los quads del pool que no se usaron esta vez.
            for (int i = used; i < _pool.Count; i++)
                if (_pool[i].gameObject.activeSelf)
                    _pool[i].gameObject.SetActive(false);
        }

        public void Clear()
        {
            foreach (MeshRenderer r in _pool)
                if (r.gameObject.activeSelf)
                    r.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------

        private MeshRenderer GetOrCreate(int index)
        {
            if (index < _pool.Count) return _pool[index];

            var go = new GameObject($"Highlight_{index}");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = highlightMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _pool.Add(mr);
            return mr;
        }

        private void PlaceOnCell(Transform t, Vector2Int cell)
        {
            Vector3 normal = _grid.Plane == GridPlane.XZ ? Vector3.up : Vector3.back;
            t.position = _grid.GetCellCenterWorld(cell) + normal * surfaceOffset;
            // El quad se crea en el plano XY mirando +Z. En XZ lo tumbamos para que mire +Y (arriba).
            t.rotation = _grid.Plane == GridPlane.XZ
                ? Quaternion.Euler(-90f, 0f, 0f)
                : Quaternion.identity;
            t.localScale = Vector3.one * _grid.CellSize;
        }

        private void ApplyColor(MeshRenderer r, Color color)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetColor(ColorId, color);
            r.SetPropertyBlock(_mpb);
        }

        private static Mesh BuildQuadMesh()
        {
            var mesh = new Mesh { name = "GridHighlightQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}