using System.Collections.Generic;
using UnityEngine;
using LoopEngine.GridEngine;
using LoopEngine.GridEngine.Placement;
using LoopEngine.GridEngine.Builder;

namespace LoopEngine.GridEngine.Builder.EditorTools
{
    /// <summary>
    /// Materializa los prefabs del CityLayout en modo edición como VISTA PREVIA.
    /// Los objetos se crean con HideFlags.HideAndDontSave: no se guardan en la escena ni en la
    /// build, pero por eso hay que destruirlos manualmente (DestroyImmediate) o se filtran.
    /// Usa exactamente la misma matemática de posición/rotación que el runtime (GridFootprint),
    /// así la vista previa coincide con lo que se colocará en Play.
    /// </summary>
    public class CityLayoutPreview
    {
        private const string RootName = "[CityLayoutPreview]";
        private readonly List<Vector2Int> _cellBuffer = new();
        private GameObject _root;

        /// <summary>Reconstruye por completo la vista previa a partir del layout.</summary>
        public void Rebuild(CityLayoutAuthoring authoring, IReadOnlyGrid grid)
        {
            Clear(authoring);
            if (authoring == null || authoring.layout == null) return;

            _root = new GameObject(RootName) { hideFlags = HideFlags.HideAndDontSave };
            _root.transform.SetParent(authoring.transform, false);

            foreach (CityLayout.Entry e in authoring.layout.entries)
            {
                if (e.placeable == null || e.placeable.visualPrefab == null) continue;

                GridFootprint.ComputeCells(e.placeable, e.anchor, e.rotation, _cellBuffer);
                Vector3 pos = GridFootprint.CenterWorld(grid, _cellBuffer);
                Quaternion rot = GridFootprint.VisualRotation(grid, e.rotation);

                var go = Object.Instantiate(e.placeable.visualPrefab, pos, rot, _root.transform);
                MarkRecursively(go, HideFlags.HideAndDontSave);
            }
        }

        /// <summary>Destruye la vista previa actual y cualquier root huérfano (p.ej. tras recompilar).</summary>
        public void Clear(CityLayoutAuthoring authoring)
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }

            if (authoring == null) return;

            // Limpia roots huérfanos por nombre (sobreviven a recompilaciones por ser DontSave).
            Transform orphan = authoring.transform.Find(RootName);
            while (orphan != null)
            {
                Object.DestroyImmediate(orphan.gameObject);
                orphan = authoring.transform.Find(RootName);
            }
        }

        private static void MarkRecursively(GameObject go, HideFlags flags)
        {
            go.hideFlags = flags;
            foreach (Transform child in go.transform)
                MarkRecursively(child.gameObject, flags);
        }
    }
}