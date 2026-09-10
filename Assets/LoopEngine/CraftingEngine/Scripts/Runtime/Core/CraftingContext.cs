using System;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Everything an operation needs to resolve itself: the four registries and the clock.
    /// Built once during initialisation and passed by reference from then on, so no part
    /// of the system needs a singleton or a static lookup.
    /// </summary>
    /// <remarks>
    /// The context deliberately does NOT hold item containers. Containers are per machine,
    /// per player or per request, and baking them in here would make the context stateful.
    /// </remarks>
    public sealed class CraftingContext
    {
        public CraftingContext(
            ItemManager items,
            RecipeManager recipes,
            MachineManager machines,
            MachineCategoryManager categories,
            ModifierManager modifiers = null,
            ICraftingClock clock = null)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            Machines = machines ?? throw new ArgumentNullException(nameof(machines));
            Categories = categories ?? throw new ArgumentNullException(nameof(categories));

            // Optional: a game with no upgrades gets an empty registry instead of a null check
            // at every call site.
            Modifiers = modifiers ?? new ModifierManager(0);
            Clock = clock ?? new UnityCraftingClock();
        }

        public ItemManager Items { get; }

        public RecipeManager Recipes { get; }

        public MachineManager Machines { get; }

        public MachineCategoryManager Categories { get; }

        /// <summary>Upgrade modifiers. Empty when the game has none.</summary>
        public ModifierManager Modifiers { get; }

        /// <summary>Time source driving every running job.</summary>
        public ICraftingClock Clock { get; }

        /// <summary>True when there is at least one item and one recipe to work with.</summary>
        public bool HasContent => Items.Count > 0 && Recipes.Count > 0;

        public CraftingStatus ResolveRecipe(in CraftingId id, out RecipeDefinition recipe)
            => Recipes.Resolve(in id, out recipe);

        public CraftingStatus ResolveMachine(in CraftingId id, out MachineDefinition machine)
            => Machines.Resolve(in id, out machine);

        public CraftingStatus ResolveItem(in CraftingId id, out ItemDefinition item)
            => Items.Resolve(in id, out item);

        /// <summary>
        /// Runs every cross-registry content check in one pass: recipes pointing at
        /// unregistered items, and recipes no machine can run.
        /// Call once after building the context; not meant for per-frame use.
        /// </summary>
        /// <returns>Total number of problems found.</returns>
        public int ValidateContent(
            System.Collections.Generic.List<RecipeDefinition> brokenReferences = null,
            System.Collections.Generic.List<RecipeDefinition> unreachable = null)
        {
            int problems = Recipes.ValidateAgainstItems(Items, brokenReferences);
            problems += Machines.FindUnreachableRecipes(Recipes, unreachable);
            return problems;
        }
    }
}