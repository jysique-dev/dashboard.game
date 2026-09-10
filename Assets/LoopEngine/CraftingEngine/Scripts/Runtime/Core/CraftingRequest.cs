namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// What the caller asks for: run this recipe, on this machine, this many times,
    /// pulling from here and pushing to there.
    /// </summary>
    public readonly struct CraftingRequest
    {
        /// <summary>Hard ceiling on a single request, so count * amount can never overflow.</summary>
        public const int MaxCount = 1_000_000;

        public readonly CraftingId Recipe;

        /// <summary><see cref="CraftingId.None"/> means hand crafting, with no machine.</summary>
        public readonly CraftingId Machine;

        /// <summary>How many times to run the recipe. Always at least 1.</summary>
        public readonly int Count;

        /// <summary>Where inputs are taken from.</summary>
        public readonly IItemContainer Input;

        /// <summary>Where outputs are delivered. May be the same container as the input.</summary>
        public readonly IItemContainer Output;

        public CraftingRequest(
            CraftingId recipe,
            CraftingId machine,
            IItemContainer input,
            IItemContainer output,
            int count = 1)
        {
            Recipe = recipe;
            Machine = machine;
            Input = input;
            Output = output;

            if (count < 1)
                count = 1;
            else if (count > MaxCount)
                count = MaxCount;

            Count = count;
        }

        /// <summary>Hand crafting shorthand: no machine involved.</summary>
        public static CraftingRequest ByHand(
            CraftingId recipe,
            IItemContainer input,
            IItemContainer output,
            int count = 1)
            => new CraftingRequest(recipe, CraftingId.None, input, output, count);

        public bool IsHandCrafted => !Machine.IsValid;

        /// <summary>Structural sanity only. Says nothing about whether the ids resolve.</summary>
        public bool IsWellFormed => Recipe.IsValid && Input != null && Output != null;
    }

    /// <summary>
    /// A validated request. Everything is resolved and the numbers are final:
    /// session 6 turns this straight into a running job without re-checking anything.
    /// </summary>
    public readonly struct CraftingPlan
    {
        public readonly RecipeDefinition Recipe;

        /// <summary>Null for hand crafting.</summary>
        public readonly MachineDefinition Machine;

        /// <summary>Repetitions requested.</summary>
        public readonly int Count;

        /// <summary>
        /// Seconds for ONE repetition on this machine, after the machine's base speed.
        /// Per-instance modifiers (session 8) are applied on top, at job start.
        /// </summary>
        public readonly float SecondsPerCraft;

        public readonly IItemContainer Input;

        public readonly IItemContainer Output;

        public CraftingPlan(
            RecipeDefinition recipe,
            MachineDefinition machine,
            int count,
            float secondsPerCraft,
            IItemContainer input,
            IItemContainer output)
        {
            Recipe = recipe;
            Machine = machine;
            Count = count < 1 ? 1 : count;
            SecondsPerCraft = secondsPerCraft < 0f ? 0f : secondsPerCraft;
            Input = input;
            Output = output;
        }

        public bool IsValid => Recipe != null && Input != null && Output != null;

        /// <summary>Total work time for the whole batch at the current speed.</summary>
        public float TotalSeconds => SecondsPerCraft * Count;
    }
}