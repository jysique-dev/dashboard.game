using UnityEngine;
using LoopEngine.GridEngine.Placement;

namespace LoopEngine.GridEngine.Builder
{
    /// <summary>
    /// Materializa un CityLayout (datos) sobre la grilla en runtime, usando el MISMO
    /// GridPlacementSystem que la colocación del usuario. Por eso una ciudad autoría en
    /// el editor y una construida a mano por el jugador comparten exactamente la misma lógica.
    /// </summary>
    [AddComponentMenu("LoopEngine/Grid/City Layout Loader")]
    public class CityLayoutLoader : MonoBehaviour
    {
        [SerializeField] private GridPlacementSystem placement;
        [SerializeField] private CityLayout layout;
        [SerializeField] private bool loadOnStart = true;

        private void Awake()
        {
            if (placement == null) placement = GetComponentInParent<GridPlacementSystem>();
        }

        private void Start()
        {
            if (loadOnStart) Load();
        }

        /// <summary>Coloca todas las entradas del layout. Devuelve cuántas se colocaron con éxito.</summary>
        public int Load()
        {
            if (placement == null || layout == null) return 0;

            int placed = 0;
            foreach (CityLayout.Entry e in layout.entries)
            {
                if (e.placeable == null) continue;
                if (placement.TryPlace(e.placeable, e.anchor, e.rotation) != null)
                    placed++;
                else
                    Debug.LogWarning($"[CityLayoutLoader] No se pudo colocar '{e.placeable.id}' en {e.anchor}.", this);
            }
            return placed;
        }
    }
}