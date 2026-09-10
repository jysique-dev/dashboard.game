using UnityEngine;

namespace LoopEngine.CraftingEngine.UI
    {
        /// <summary>
        /// Turns crafting data into things a human can read. The crafting system stores hashed
        /// ids and optional sprites and nothing else, so every label, icon and number format
        /// the UI shows comes from here.
        /// </summary>
        /// <remarks>
        /// Implemented by the host project. Nothing in the crafting system knows this interface
        /// exists, and nothing in the UI resolves a name or an icon by any other route.
        /// </remarks>
        public interface ICraftingDisplayProvider
        {
            /// <summary>Definition behind an item id, or null when the id is unknown here.</summary>
            ItemDefinition ResolveItem(in CraftingId item);

            /// <summary>Readable name for an item id. Never null; falls back to a placeholder.</summary>
            string GetItemName(in CraftingId item);

            /// <summary>Icon for an item id, or null when there is none.</summary>
            Sprite GetItemIcon(in CraftingId item);

            /// <summary>Readable name of a recipe. Never null.</summary>
            string GetRecipeName(RecipeDefinition recipe);

            /// <summary>
            /// Icon representing a recipe. Recipes carry no sprite of their own, so this is
            /// normally the icon of the first output.
            /// </summary>
            Sprite GetRecipeIcon(RecipeDefinition recipe);

            /// <summary>Readable name of a machine type. Never null.</summary>
            string GetMachineName(MachineDefinition machine);

            /// <summary>Icon of a machine type, or null.</summary>
            Sprite GetMachineIcon(MachineDefinition machine);

            /// <summary>Readable name of an upgrade. Never null.</summary>
            string GetModifierName(ModifierDefinition modifier);

            /// <summary>
            /// Player-facing explanation of a status code, used for "waiting for iron" style
            /// messages instead of showing an idle machine with no reason.
            /// </summary>
            string GetStatusText(CraftingStatus status);

            /// <summary>Formats a stack amount, e.g. "x12" or "1.2k".</summary>
            string FormatAmount(int amount);

            /// <summary>Formats a duration in seconds, e.g. "4.5s" or "2:05".</summary>
            string FormatSeconds(float seconds);
        }
    }