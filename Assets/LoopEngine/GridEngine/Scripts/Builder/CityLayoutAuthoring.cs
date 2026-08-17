using System.Collections.Generic;
using UnityEngine;
using LoopEngine.GridEngine.Placement;

namespace LoopEngine.GridEngine.Builder
{
    /// <summary>
    /// Contexto de autoría para pintar ciudades en el editor. Solo guarda datos:
    /// a qué CityLayout escribimos, con qué GridSettings (para la matemática de la grilla),
    /// la pieza activa y la rotación actual.
    ///
    /// La INTERACCIÓN (dibujar, pintar, borrar) vive en el CustomEditor dentro de una carpeta
    /// Editor/, que Unity excluye de la build. Este componente es inocuo en runtime.
    /// </summary>
    [AddComponentMenu("LoopEngine/Grid/City Layout Authoring")]
    public class CityLayoutAuthoring : MonoBehaviour
    {
        [Tooltip("Configuración de la grilla sobre la que se autoría (misma que usará el host en runtime).")]
        public GridSettings settings;

        [Tooltip("Asset donde se guardan las colocaciones que pintes.")]
        public CityLayout layout;

        [Tooltip("Pieza activa que se colocará al pintar.")]
        public PlaceableDefinition activePlaceable;

        //[Tooltip("Piezas disponibles para pintar; se eligen con botones en el inspector.")]
        //public List<PlaceableDefinition> palette = new();

        [Tooltip("Rotación actual de la pieza activa (se cambia con R en la Scene).")]
        public GridRotation currentRotation = GridRotation.Deg0;

        [Tooltip("Muestra los prefabs reales en modo edición (vista previa, no se guarda en la escena).")]
        public bool previewEnabled = true;

        [Header("Colores de depuración")]
        public Color gridColor = new Color(0f, 1f, 1f, 0.35f);
        public Color authoredColor = new Color(0.3f, 1f, 0.4f, 0.25f);
    }
}