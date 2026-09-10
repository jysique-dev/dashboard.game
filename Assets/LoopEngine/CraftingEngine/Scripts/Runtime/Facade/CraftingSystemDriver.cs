using UnityEngine;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// The one MonoBehaviour in the system. Builds the context from a database asset and
    /// calls <see cref="CraftingSystem.Tick"/> once per frame.
    /// </summary>
    /// <remarks>
    /// Optional. If your game already has a central update loop, skip this component,
    /// build a <see cref="CraftingSystem"/> yourself and tick it from there.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("LoopEngine/Crafting/Crafting System Driver")]
    public sealed class CraftingSystemDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Definitions to load. Required.")]
        private CraftingDatabase _database;

        [SerializeField, Tooltip("Build the system in Awake. Turn off to build it yourself later.")]
        private bool _buildOnAwake = true;

        [SerializeField, Tooltip("Largest delta a single frame may contribute, in seconds.")]
        private float _maxFrameDelta = 0.25f;

        [SerializeField, Tooltip("Log a warning when the database contains broken or unreachable content.")]
        private bool _logContentProblems = true;

        /// <summary>Null until the system is built.</summary>
        public CraftingSystem System { get; private set; }

        /// <summary>What was rejected or flagged during the last build. Null until built.</summary>
        public CraftingBuildReport Report { get; private set; }

        public bool IsBuilt => System != null;

        private void Awake()
        {
            if (_buildOnAwake)
                Build();
        }

        private void Update()
        {
            System?.Tick();
        }

        private void OnDestroy()
        {
            System = null;
            Report = null;
        }

        /// <summary>
        /// Builds the system from the assigned database. Safe to call again to hot reload
        /// content: every existing machine instance is discarded with it.
        /// </summary>
        public CraftingSystem Build()
        {
            if (_database == null)
            {
                Debug.LogError("[LoopEngine.Crafting] No database assigned; the system was not built.", this);
                return null;
            }

            Report = new CraftingBuildReport();
            CraftingContext context = _database.BuildContext(new UnityCraftingClock(_maxFrameDelta), Report);
            System = new CraftingSystem(context);

            if (_logContentProblems && !Report.IsClean)
            {
                Debug.LogWarning(
                    "[LoopEngine.Crafting] Database built with " + Report.ProblemCount + " problem(s): "
                    + Report.RejectedCount + " rejected, "
                    + Report.BrokenReferences.Count + " with broken references, "
                    + Report.UnreachableRecipes.Count + " unreachable.",
                    this);
            }

            return System;
        }
    }
}