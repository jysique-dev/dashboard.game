using UnityEngine;

namespace LoopEngine.GridMovement.Samples
{
    /// <summary>
    /// The two things this system was built to do, in code, with nothing else involved.
    ///
    /// A sample and not part of the runtime: it lives in its own namespace and folder so it
    /// can be deleted from a project without leaving a hole, the same way the grid engine
    /// keeps its test utilities out of Runtime.
    /// </summary>
    [AddComponentMenu("Grid Movement/Samples/Grid Movement Demo")]
    public class GridMovementDemo : MonoBehaviour
    {
        [SerializeField] private GridMovementSystem movement;

        [Header("Objective 1: spawn at a starting cell")]
        [SerializeField] private GameObject agentPrefab;
        [SerializeField] private GridCoord spawnCoord = new GridCoord(0, 0);
        [SerializeField] private bool spawnOnStart = true;

        [Header("Objective 2: travel to a given cell")]
        [SerializeField] private GridCoord destinationCoord = new GridCoord(8, 5);
        [SerializeField] private bool moveOnStart = false;

        private GridMover agent;

        private void Start()
        {
            if (movement == null)
            {
                Debug.LogError($"[{nameof(GridMovementDemo)}] No GridMovementSystem assigned.", this);
                return;
            }

            if (spawnOnStart) SpawnAgent();
            if (moveOnStart) MoveAgent();
        }

        /// <summary>Objective 1. Two lines, and the agent exists on a cell, bound and ready.</summary>
        [ContextMenu("Spawn Agent")]
        public void SpawnAgent()
        {
            agent = movement.Spawn(agentPrefab, spawnCoord, transform);
            if (agent == null) return;

            agent.PathCompleted += coord => Debug.Log($"Arrived at {coord}.");
            agent.PathBlocked += coord => Debug.Log($"Blocked by {coord}.");
        }

        /// <summary>
        /// Objective 2. Weights and walkability are honoured by the search the system was
        /// configured with; nothing here has to know which one that is.
        /// </summary>
        [ContextMenu("Move Agent")]
        public void MoveAgent()
        {
            if (agent == null)
            {
                Debug.LogWarning($"[{nameof(GridMovementDemo)}] Spawn an agent first.", this);
                return;
            }

            if (!movement.MoveTo(agent, destinationCoord))
                Debug.Log($"No path from {agent.CurrentCoord} to {destinationCoord}.");
        }
        private void Update()
        {
            if (movement == null) return;
            if (agent == null) return;
            if (!moveOnStart && Input.GetKeyDown(KeyCode.Z))
            {
                MoveAgent();
            }
        }
    }
}