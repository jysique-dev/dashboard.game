using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Whole-system save and load, built on the per-machine capture in
    /// <see cref="MachineInstance.CaptureState"/>.
    /// </summary>
    /// <remarks>
    /// Machines are matched by position, so the caller must recreate them in the same order
    /// before restoring. That is the honest limitation of a system that does not own machine
    /// identity: your game knows which furnace is which, this system does not. If your
    /// machines have persistent world ids, save each <see cref="MachineSaveData"/> next to its
    /// own entity instead of using <see cref="RestoreAll"/>.
    /// </remarks>
    public static class CraftingPersistence
    {
        /// <summary>Captures every machine registered with the system, in its current order.</summary>
        public static CraftingSaveData CaptureAll(CraftingSystem system)
        {
            var data = new CraftingSaveData();

            if (system == null)
                return data;

            IReadOnlyList<MachineInstance> machines = system.Machines;
            data.machines = new MachineSaveData[machines.Count];

            for (int i = 0; i < machines.Count; i++)
                data.machines[i] = machines[i].CaptureState();

            return data;
        }

        /// <summary>
        /// Restores by position. Recreate the machines first, in the same order they were
        /// captured; entries whose machine id does not match the instance at that position are
        /// skipped rather than applied to the wrong machine.
        /// </summary>
        /// <returns>How many machines were restored.</returns>
        public static int RestoreAll(
            CraftingSystem system,
            CraftingSaveData data,
            List<string> unresolved = null)
        {
            if (system == null || data?.machines == null)
                return 0;

            IReadOnlyList<MachineInstance> machines = system.Machines;
            int count = data.machines.Length < machines.Count ? data.machines.Length : machines.Count;
            int restored = 0;

            for (int i = 0; i < count; i++)
            {
                MachineSaveData saved = data.machines[i];
                if (saved == null)
                    continue;

                MachineInstance machine = machines[i];

                // Guard against a save written before the scene layout changed.
                if (!string.Equals(saved.machineId, machine.Definition.RawId, System.StringComparison.Ordinal))
                {
                    unresolved?.Add(saved.machineId);
                    continue;
                }

                if (machine.RestoreState(saved, unresolved) == CraftingStatus.Success)
                    restored++;
            }

            return restored;
        }
    }
}