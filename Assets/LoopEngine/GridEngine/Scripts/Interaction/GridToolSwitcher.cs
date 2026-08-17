using UnityEngine;

namespace LoopEngine.GridEngine.Interaction
{
    /// <summary>
    /// Activa UNA sola "herramienta" a la vez (colocación, selección, movimiento...).
    /// Cada herramienta se suscribe a los eventos del GridInteractor en OnEnable y se
    /// desuscribe en OnDisable, así que activar/desactivar el componente ya conmuta quién
    /// escucha el clic. Esto resuelve el conflicto de que varios consumidores usan PrimaryClicked.
    /// </summary>
    [AddComponentMenu("LoopEngine/Grid/Grid Tool Switcher")]
    public class GridToolSwitcher : MonoBehaviour
    {
        [Tooltip("Herramientas mutuamente excluyentes (MonoBehaviours que reaccionan al interactor).")]
        [SerializeField] private MonoBehaviour[] tools;
        [SerializeField] private int defaultIndex = 0;

        private void Start() => Activate(defaultIndex);

        /// <summary>Activa la herramienta en 'index' y desactiva las demás.</summary>
        public void Activate(int index)
        {
            if (tools == null) return;
            for (int i = 0; i < tools.Length; i++)
                if (tools[i] != null) tools[i].enabled = (i == index);
        }

        /// <summary>Activa una herramienta concreta (debe estar en la lista).</summary>
        public void Activate(MonoBehaviour tool)
        {
            if (tools == null) return;
            for (int i = 0; i < tools.Length; i++)
                if (tools[i] != null) tools[i].enabled = (tools[i] == tool);
        }

        /// <summary>Desactiva todas (ningún modo activo).</summary>
        public void DeactivateAll() => Activate(-1);
    }
}