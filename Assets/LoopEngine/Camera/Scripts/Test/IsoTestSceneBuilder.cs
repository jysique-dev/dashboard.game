using LoopEngine.IsoCamera.Test;
using UnityEngine;

namespace LoopEngine.IsoCamera.Test
{
    /// <summary>
    /// Genera por código una escena de prueba visual: tablero de ajedrez de baldosas 1x1,
    /// postes verticales, un cubo unitario de referencia y ejes de mundo.
    /// No requiere ningún asset importado. Solo para testing.
    ///
    /// Uso: GameObject vacío -> añadir este componente -> Play (o "Build Now" en el menú contextual).
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Iso Camera/Test/Iso Test Scene Builder")]
    public sealed class IsoTestSceneBuilder : MonoBehaviour
    {
        private const string ContainerName = "__IsoTestScene";

        [Header("Tablero")]
        [SerializeField, Min(2)] private int tiles = 16;
        [SerializeField, Min(0.1f)] private float tileSize = 1f;
        [SerializeField] private Color colorA = new Color(0.82f, 0.82f, 0.86f);
        [SerializeField] private Color colorB = new Color(0.42f, 0.44f, 0.52f);

        [Header("Referencias")]
        [Tooltip("Cubo unitario centrado: en isométrico verdadero sus 3 caras visibles se ven iguales.")]
        [SerializeField] private bool spawnUnitCube = true;
        [Tooltip("Postes verticales en las esquinas para comprobar que las verticales quedan rectas.")]
        [SerializeField] private bool spawnPoles = true;
        [SerializeField, Min(1)] private int poleHeight = 4;
        [SerializeField] private bool spawnAxes = true;

        [Header("Ciclo de vida")]
        [SerializeField] private bool buildOnEnable = true;

        private Transform container;

        private void OnEnable() { if (buildOnEnable) Build(); }

        [ContextMenu("Build Now")]
        public void Build()
        {
            Clear();
            container = new GameObject(ContainerName).transform;
            container.SetParent(transform, false);

            Material matA = CreateMaterial(colorA);
            Material matB = CreateMaterial(colorB);

            float half = tiles * tileSize * 0.5f;

            for (int x = 0; x < tiles; x++)
            {
                for (int z = 0; z < tiles; z++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Tile_{x}_{z}";
                    tile.transform.SetParent(container, false);
                    tile.transform.localPosition = new Vector3(
                        -half + (x + 0.5f) * tileSize, -0.05f, -half + (z + 0.5f) * tileSize);
                    tile.transform.localScale = new Vector3(tileSize * 0.98f, 0.1f, tileSize * 0.98f);
                    SetMaterial(tile, ((x + z) % 2 == 0) ? matA : matB);
                }
            }

            if (spawnUnitCube)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "UnitCube";
                cube.transform.SetParent(container, false);
                cube.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                SetMaterial(cube, CreateMaterial(new Color(0.95f, 0.75f, 0.2f)));
            }

            if (spawnPoles)
            {
                Vector3[] corners =
                {
                    new Vector3(-half + tileSize * 0.5f, 0f, -half + tileSize * 0.5f),
                    new Vector3( half - tileSize * 0.5f, 0f, -half + tileSize * 0.5f),
                    new Vector3(-half + tileSize * 0.5f, 0f,  half - tileSize * 0.5f),
                    new Vector3( half - tileSize * 0.5f, 0f,  half - tileSize * 0.5f)
                };
                Material poleMat = CreateMaterial(new Color(0.9f, 0.35f, 0.35f));
                for (int i = 0; i < corners.Length; i++)
                {
                    var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pole.name = $"Pole_{i}";
                    pole.transform.SetParent(container, false);
                    pole.transform.localPosition = corners[i] + Vector3.up * (poleHeight * 0.5f);
                    pole.transform.localScale = new Vector3(0.25f, poleHeight, 0.25f);
                    SetMaterial(pole, poleMat);
                }
            }

            if (spawnAxes)
            {
                SpawnAxis("Axis_X", Vector3.right, Color.red, half);
                SpawnAxis("Axis_Z", Vector3.forward, Color.blue, half);
                SpawnAxis("Axis_Y", Vector3.up, Color.green, half * 0.4f);
            }
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            Transform existing = transform.Find(ContainerName);
            if (existing == null) return;
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
            container = null;
        }

        private void SpawnAxis(string axisName, Vector3 dir, Color color, float length)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = axisName;
            go.transform.SetParent(container, false);
            go.transform.localPosition = dir * (length * 0.5f) + Vector3.up * 0.06f;
            go.transform.localScale = dir * length + (Vector3.one - Abs(dir)) * 0.12f;
            SetMaterial(go, CreateMaterial(color));
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // Delegado a IsoDebugMaterials: detecta Built-in / URP / HDRP y elige el shader válido.
        private static Material CreateMaterial(Color color) => IsoDebugMaterials.Create(color);

        private static void SetMaterial(GameObject go, Material m)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = m;
            var c = go.GetComponent<Collider>();
            if (c != null) DestroySafe(c);
        }

        private static void DestroySafe(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}