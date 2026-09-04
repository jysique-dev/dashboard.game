using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Draws the grid at runtime, in the Game view and in a build.
    ///
    /// Uses three child GameObjects with MeshFilter/MeshRenderer instead of
    /// Graphics.RenderMesh. The immediate-mode path proved hard to diagnose when a
    /// draw silently failed to reach the pipeline; real renderers are inspectable:
    /// select the child in the Hierarchy and the Inspector shows the mesh, its
    /// bounds and its material. Children carry no colliders and are not saved.
    /// </summary>
    [AddComponentMenu("IsoGrid/Grid Renderer")]
    [ExecuteAlways]
    public class GridRenderer : MonoBehaviour
    {
        [Header("Material")]
        [Tooltip("Leave empty to auto-create one from the IsoGrid/Unlit Vertex Color shader.")]
        [SerializeField] private Material material;

        [Header("Lines")]
        [SerializeField] private bool drawLines = true;
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.6f);

        [Header("Highlight")]
        [SerializeField] private Color highlightColor = new Color(0.2f, 0.9f, 1f, 0.8f);

        [Header("Placement")]
        [Tooltip("Lifts the grid along the plane normal. Raise it if the ground z-fights with the lines.")]
        [SerializeField] private float heightOffset = 0.01f;

        [Header("Rendering")]
        [Tooltip("Layer for the generated child objects. Must be inside the camera's Culling Mask.")]
        [SerializeField] private int layer;

        private readonly GridMeshBuilder lineBuilder = new GridMeshBuilder();
        private readonly GridMeshBuilder cellBuilder = new GridMeshBuilder();
        private readonly GridMeshBuilder highlightBuilder = new GridMeshBuilder();

        private readonly List<GridCoord> highlighted = new List<GridCoord>();

        private MeshFilter lineFilter;
        private MeshFilter cellFilter;
        private MeshFilter highlightFilter;

        private MeshRenderer lineRenderer;
        private MeshRenderer cellRenderer;
        private MeshRenderer highlightRenderer;

        // One material per layer: 3D geometry is ordered by renderQueue, not sortingOrder.
        private readonly Material[] runtimeMaterials = new Material[3];

        private IGridLayout layout;
        private GridBounds bounds;
        private IGridCellColorSource colorSource;

        private bool linesDirty = true;
        private bool cellsDirty = true;
        private bool highlightDirty = true;

        /// <summary>
        /// Binds the renderer to a layout, a region and an optional per-cell colour source.
        /// Call again whenever any of the three is replaced.
        /// </summary>
        public void Bind(IGridLayout layout, GridBounds bounds, IGridCellColorSource colorSource = null)
        {
            this.layout = layout;
            this.bounds = bounds;
            this.colorSource = colorSource;
            SetAllDirty();
        }

        /// <summary>Queues a full rebuild. Cheap to call; the rebuild happens once, before the next draw.</summary>
        public void SetAllDirty()
        {
            linesDirty = true;
            cellsDirty = true;
            highlightDirty = true;
        }

        /// <summary>Queues a rebuild of the filled cells only. Hook this to GridMap.CellChanged.</summary>
        public void SetCellsDirty() => cellsDirty = true;

        // --- Highlight ------------------------------------------------------

        public void ClearHighlight()
        {
            if (highlighted.Count == 0) return;
            highlighted.Clear();
            highlightDirty = true;
        }

        public void SetHighlight(GridCoord coord)
        {
            if (highlighted.Count == 1 && highlighted[0] == coord) return;
            highlighted.Clear();
            highlighted.Add(coord);
            highlightDirty = true;
        }

        public void SetHighlight(IReadOnlyList<GridCoord> coords, int count = -1)
        {
            highlighted.Clear();
            if (coords != null)
            {
                int limit = count < 0 ? coords.Count : Mathf.Min(count, coords.Count);
                for (int i = 0; i < limit; i++)
                    highlighted.Add(coords[i]);
            }
            highlightDirty = true;
        }

        // --- Lifecycle ------------------------------------------------------

        private void OnValidate() => SetAllDirty();

        private void OnEnable() => SetAllDirty();

        private void OnDisable() => ReleaseResources();

        private void OnDestroy() => ReleaseResources();

        private void LateUpdate()
        {
            if (layout == null || bounds.IsEmpty) return;

            EnsureChildren();
            RebuildIfNeeded();

            lineRenderer.enabled = drawLines && HasGeometry(lineFilter);
            cellRenderer.enabled = HasGeometry(cellFilter);
            highlightRenderer.enabled = HasGeometry(highlightFilter);
        }

        private static bool HasGeometry(MeshFilter filter)
            => filter != null && filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0;

        // --- Child objects --------------------------------------------------

        private void EnsureChildren()
        {
            if (lineFilter == null)
                CreateChild("IsoGrid Lines", 0, out lineFilter, out lineRenderer);
            if (cellFilter == null)
                CreateChild("IsoGrid Cells", 1, out cellFilter, out cellRenderer);
            if (highlightFilter == null)
                CreateChild("IsoGrid Highlight", 2, out highlightFilter, out highlightRenderer);
        }

        private void CreateChild(string name, int order, out MeshFilter filter, out MeshRenderer meshRenderer)
        {
            GameObject go = new GameObject(name)
            {
                // Not hidden: keeping it visible in the Hierarchy makes the mesh,
                // its bounds and its material inspectable when something looks wrong.
                hideFlags = HideFlags.DontSave,
                layer = Mathf.Clamp(layer, 0, 31)
            };

            go.transform.SetParent(transform, false);

            filter = go.AddComponent<MeshFilter>();
            meshRenderer = go.AddComponent<MeshRenderer>();

            meshRenderer.sharedMaterial = ResolveMaterial(order);
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            filter.sharedMesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
        }

        private void ReleaseResources()
        {
            DestroyChild(ref lineFilter, ref lineRenderer);
            DestroyChild(ref cellFilter, ref cellRenderer);
            DestroyChild(ref highlightFilter, ref highlightRenderer);

            for (int i = 0; i < runtimeMaterials.Length; i++)
            {
                DestroySafe(runtimeMaterials[i]);
                runtimeMaterials[i] = null;
            }
        }

        private void DestroyChild(ref MeshFilter filter, ref MeshRenderer meshRenderer)
        {
            if (filter != null)
            {
                DestroySafe(filter.sharedMesh);
                DestroySafe(filter.gameObject);
            }
            filter = null;
            meshRenderer = null;
        }

        private static void DestroySafe(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        // --- Mesh rebuilds --------------------------------------------------

        private void RebuildIfNeeded()
        {
            if (linesDirty)
            {
                lineBuilder.BuildLines(layout, bounds, lineFilter.sharedMesh, lineColor, heightOffset);
                linesDirty = false;
            }

            if (cellsDirty)
            {
                if (colorSource != null)
                    cellBuilder.BuildCells(layout, bounds, colorSource, cellFilter.sharedMesh, heightOffset * 2f);
                else
                    cellFilter.sharedMesh.Clear();
                cellsDirty = false;
            }

            if (highlightDirty)
            {
                highlightBuilder.BuildCells(layout, highlighted, highlightColor, highlightFilter.sharedMesh, heightOffset * 3f);
                highlightDirty = false;
            }
        }

        /// <summary>
        /// Returns a material for the given layer. Each layer gets its own instance with
        /// a bumped renderQueue so lines, cells and highlight draw in a fixed order.
        /// sortingOrder is not used here: it only affects 2D renderers.
        /// </summary>
        private Material ResolveMaterial(int order)
        {
            if (order < 0 || order >= runtimeMaterials.Length) order = 0;
            if (runtimeMaterials[order] != null) return runtimeMaterials[order];

            Material source = material;
            bool sourceIsTemporary = false;

            if (source == null)
            {
                Shader shader = Shader.Find("IsoGrid/Unlit Vertex Color");
                if (shader == null)
                {
                    Debug.LogError(
                        "[GridRenderer] Shader 'IsoGrid/Unlit Vertex Color' not found. " +
                        "Keep IsoGridUnlit.shader inside the project, or assign a material manually.", this);
                    return null;
                }

                source = new Material(shader);
                sourceIsTemporary = true;
            }

            Material instance = new Material(source)
            {
                name = $"IsoGrid Runtime {order}",
                hideFlags = HideFlags.DontSave
            };

            int baseQueue = source.renderQueue >= 0 ? source.renderQueue : (int)RenderQueue.Transparent;
            instance.renderQueue = baseQueue + order;

            if (sourceIsTemporary) DestroySafe(source);

            runtimeMaterials[order] = instance;
            return instance;
        }
    }
}