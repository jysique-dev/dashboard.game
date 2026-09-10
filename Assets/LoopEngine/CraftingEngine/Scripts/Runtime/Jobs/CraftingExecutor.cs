using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Crafting that resolves in one call, with no job, no slot and no waiting.
    /// This is the hand crafting path, and the shortcut for recipes whose duration is zero.
    /// </summary>
    public static class CraftingExecutor
    {
        /// <summary>
        /// Validates, consumes and produces in a single call.
        /// Either the whole batch happens or nothing does: inputs are only taken once the
        /// output has been confirmed to fit.
        /// </summary>
        /// <param name="overflow">
        /// Optional collector for output that did not fit after all. Should stay empty in
        /// practice, since space is checked first; it catches containers whose reported
        /// capacity does not match what they actually accept.
        /// </param>
        public static CraftingStatus CraftNow(
            in CraftingRequest request,
            CraftingContext context,
            List<ItemStack> overflow = null)
        {
            CraftingStatus status = CraftingValidator.Validate(in request, context, out CraftingPlan plan);
            if (status != CraftingStatus.Success)
                return status;

            return Execute(in plan, overflow);
        }

        /// <summary>
        /// Runs an already validated plan. Skips validation, so only call it with a plan that
        /// came from <see cref="CraftingValidator.Validate"/> in the same frame.
        /// </summary>
        public static CraftingStatus Execute(in CraftingPlan plan, List<ItemStack> overflow = null)
        {
            if (!plan.IsValid)
                return CraftingStatus.InvalidRequest;

            ItemStack[] inputs = plan.Recipe.Inputs;
            ItemStack[] outputs = plan.Recipe.Outputs;

            CraftingStatus status = ItemTransfer.ConsumeAll(inputs, plan.Count, plan.Input);
            if (status != CraftingStatus.Success)
                return status;

            status = ItemTransfer.ProduceAll(outputs, plan.Count, plan.Output, overflow);

            // No rollback here on purpose. By this point some outputs may already be inside
            // the container, so refunding the inputs as well would duplicate value. This only
            // happens with a container whose reported capacity disagrees with what it accepts,
            // since CraftingValidator already confirmed the space. Pass an overflow list to
            // recover whatever did not fit.
            return status;
        }
    }
}