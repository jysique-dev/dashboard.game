using UnityEngine;

namespace LoopEngine.IsoCamera.Test
{
    /// <summary>
    /// Genera muros con collider para probar la oclusión. El builder de la escena base
    /// destruye los colliders de las baldosas a propósito (no queremos que el suelo
    /// bloquee nada), así que los ocluyentes se crean aquí y sí los conservan.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Iso Camera/Test/Iso Occlusion Test Props")]
    public sealed class IsoOcclusionTestProps : MonoBehaviour
    {
        private const string ContainerName = "__IsoOcclusionProps";

        [SerializeField, Min(1)] private int wallCount = 5;
        [SerializeField] private Vector3 wallSize = new Vector3(3f, 4f, 0.6f);
        [SerializeField, Min(1f)] private float spread = 12f;
        [SerializeField] private Color wallColor = new Color(0.35f, 0.55f, 0.85f);
        [SerializeField] private bool buildOnEnable = true;

        private void OnEnable() { if (buildOnEnable) Build(); }

        [ContextMenu("Build Now")]
        public void Build()
        {
            Clear();
            var container = new GameObject(ContainerName).transform;
            container.SetParent(transform, false);

            Material mat = IsoDebugMaterials.Create(wallColor);

            for (int i = 0; i < wallCount; i++)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{i}";
                wall.transform.SetParent(container, false);

                float angle = i / (float)wallCount * Mathf.PI * 2f;
                wall.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * spread * 0.5f,
                    wallSize.y * 0.5f,
                    Mathf.Sin(angle) * spread * 0.5f);
                wall.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                wall.transform.localScale = wallSize;

                // El collider se conserva: es lo que detecta el SphereCast.
                var r = wall.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            Transform existing = transform.Find(ContainerName);
            if (existing == null) return;
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }
}