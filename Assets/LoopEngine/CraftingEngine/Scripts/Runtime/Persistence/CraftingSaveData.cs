using System;

namespace LoopEngine.CraftingEngine
{
    /// <summary>One job slot, flattened for saving.</summary>
    [Serializable]
    public sealed class JobSaveData
    {
        public string recipeId;
        public int remaining;
        public int completed;
        public float elapsed;
        public int state;
    }

    /// <summary>One pending order, flattened for saving.</summary>
    [Serializable]
    public sealed class QueuedSaveData
    {
        public string recipeId;
        public int count;
    }

    /// <summary>One machine instance, flattened for saving.</summary>
    [Serializable]
    public sealed class MachineSaveData
    {
        public string machineId;
        public float manualSpeedBonus = 1f;
        public int stallPolicy;
        public string[] modifierIds = Array.Empty<string>();
        public JobSaveData[] slots = Array.Empty<JobSaveData>();
        public QueuedSaveData[] queue = Array.Empty<QueuedSaveData>();
    }

    /// <summary>
    /// The whole runtime state of a <see cref="CraftingSystem"/>.
    /// </summary>
    /// <remarks>
    /// Plain classes with public fields, wrapped in a root object: <c>JsonUtility</c> cannot
    /// serialize a bare array at the top level, and it ignores properties and readonly fields.
    /// Nothing here references a UnityEngine.Object; definitions are stored by id so a save
    /// survives assets being moved or reimported.
    /// </remarks>
    [Serializable]
    public sealed class CraftingSaveData
    {
        /// <summary>Bump this when the shape changes so old saves can be migrated.</summary>
        public int version = 1;

        public MachineSaveData[] machines = Array.Empty<MachineSaveData>();
    }
}