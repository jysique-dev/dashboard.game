using System;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Host de escena que crea y posee una Grid&lt;TCell&gt; a partir de un GridSettings.
    ///
    /// Es abstracto y genérico a propósito: Unity NO permite añadir directamente un
    /// MonoBehaviour genérico desde el inspector, pero sí una subclase CONCRETA y no
    /// genérica de una base genérica (p. ej. SampleGridHost : GridBehaviour&lt;int&gt;).
    ///
    /// Expone la grilla como IGridProvider (vista de solo lectura, sin TCell) para que
    /// otros sistemas no dependan del tipo de celda, y como TypedGrid para quien sí lo necesite.
    /// </summary>
    public abstract class GridBehaviour<TCell> : MonoBehaviour, IGridProvider
    {
        [SerializeField] protected GridSettings settings;

        /// <summary>La grilla tipada. Disponible tras Awake (o tras llamar BuildGrid).</summary>
        public Grid<TCell> TypedGrid { get; private set; }

        /// <summary>Vista de solo lectura para consumidores genéricos.</summary>
        public IReadOnlyGrid Grid => TypedGrid;

        public event Action<IReadOnlyGrid> OnGridReady;

        public bool IsReady => TypedGrid != null;

        protected virtual void Awake() => BuildGrid();

        /// <summary>(Re)construye la grilla desde settings. Reutilizable por herramientas de editor.</summary>
        public void BuildGrid()
        {
            if (settings == null)
            {
                Debug.LogError($"[{GetType().Name}] Falta asignar GridSettings.", this);
                return;
            }

            TypedGrid = settings.CreateGrid<TCell>(CreateCell);
            OnGridReady?.Invoke(TypedGrid);
        }

        /// <summary>
        /// Fábrica del contenido inicial de cada celda. Las subclases la sobrescriben para
        /// decidir el estado de arranque. Por defecto deja default(TCell).
        /// </summary>
        protected virtual TCell CreateCell(Grid<TCell> grid, Vector2Int coord) => default;
    }
}