namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Turns a <see cref="CraftingRequest"/> into a <see cref="CraftingPlan"/>, or explains
    /// exactly why it cannot. Nothing is consumed here: validation is read only, so the UI
    /// can call it every frame to grey out a button without side effects.
    /// </summary>
    public static class CraftingValidator
    {
        /// <summary>
        /// Full check: ids resolve, machine is capable, inputs are present, outputs fit.
        /// </summary>
        public static CraftingStatus Validate(
            in CraftingRequest request,
            CraftingContext context,
            out CraftingPlan plan)
        {
            plan = default;

            if (context == null)
                return CraftingStatus.NotInitialized;

            if (!request.IsWellFormed)
                return CraftingStatus.InvalidRequest;

            CraftingStatus status = ResolvePair(
                in request,
                context,
                out RecipeDefinition recipe,
                out MachineDefinition machine);

            if (status != CraftingStatus.Success)
                return status;

            status = ItemTransfer.CheckAvailable(recipe.Inputs, request.Count, request.Input);
            if (status != CraftingStatus.Success)
                return status;

            status = ItemTransfer.CheckSpace(recipe.Outputs, request.Count, request.Output);
            if (status != CraftingStatus.Success)
                return status;

            float seconds = machine != null
                ? machine.GetCraftSeconds(recipe)
                : recipe.CraftSeconds;

            plan = new CraftingPlan(
                recipe,
                machine,
                request.Count,
                seconds,
                request.Input,
                request.Output);

            return CraftingStatus.Success;
        }

        /// <summary>
        /// Capability check only: do the ids resolve and is this machine allowed to run this
        /// recipe. Ignores inventory entirely. Use it to build a machine's recipe list, where
        /// "you lack the ingredients" is a different message from "this does not belong here".
        /// </summary>
        public static CraftingStatus ValidateCapability(
            in CraftingId recipeId,
            in CraftingId machineId,
            CraftingContext context)
        {
            if (context == null)
                return CraftingStatus.NotInitialized;

            var probe = new CraftingRequest(recipeId, machineId, null, null, 1);
            return ResolvePair(in probe, context, out _, out _);
        }

        /// <summary>
        /// Largest count that would pass <see cref="Validate"/> right now, capped by
        /// <see cref="CraftingRequest.MaxCount"/>. Returns 0 when nothing can be crafted.
        /// </summary>
        public static int GetMaxCraftableCount(
            in CraftingId recipeId,
            in CraftingId machineId,
            CraftingContext context,
            IItemContainer input)
        {
            if (context == null || input == null)
                return 0;

            var probe = new CraftingRequest(recipeId, machineId, input, input, 1);
            if (ResolvePair(in probe, context, out RecipeDefinition recipe, out _) != CraftingStatus.Success)
                return 0;

            int affordable = ItemTransfer.GetAffordableCount(recipe.Inputs, input);
            return affordable > CraftingRequest.MaxCount ? CraftingRequest.MaxCount : affordable;
        }

        /// <summary>
        /// Resolves recipe and machine and confirms the pairing is legal.
        /// <paramref name="machine"/> is null for hand crafting.
        /// </summary>
        private static CraftingStatus ResolvePair(
            in CraftingRequest request,
            CraftingContext context,
            out RecipeDefinition recipe,
            out MachineDefinition machine)
        {
            machine = null;

            CraftingStatus status = context.ResolveRecipe(in request.Recipe, out recipe);
            if (status != CraftingStatus.Success)
                return status;

            if (request.IsHandCrafted)
            {
                // No machine supplied: only a recipe that needs no category can run.
                return recipe.IsHandCrafted
                    ? CraftingStatus.Success
                    : CraftingStatus.IncompatibleMachine;
            }

            status = context.ResolveMachine(in request.Machine, out machine);
            if (status != CraftingStatus.Success)
                return status;

            if (!machine.CanRun(recipe))
            {
                machine = null;
                return CraftingStatus.IncompatibleMachine;
            }

            return CraftingStatus.Success;
        }
    }
}