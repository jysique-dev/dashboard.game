using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Moves items in and out of containers with all-or-nothing semantics.
    /// Every method here is allocation free: rollback reuses the same stack array it was
    /// given, walking backwards, so no temporary buffer is needed.
    /// </summary>
    public static class ItemTransfer
    {
        /// <summary>
        /// Checks whether the container holds every stack, scaled by <paramref name="count"/>.
        /// Read only.
        /// </summary>
        public static CraftingStatus CheckAvailable(ItemStack[] stacks, int count, IItemContainer container)
        {
            if (container == null)
                return CraftingStatus.InvalidRequest;

            if (stacks == null || stacks.Length == 0)
                return CraftingStatus.Success;

            for (int i = 0; i < stacks.Length; i++)
            {
                ItemStack stack = stacks[i];
                if (stack.IsEmpty)
                    continue;

                if (container.GetAmount(stack.Item) < stack.Amount * count)
                    return CraftingStatus.MissingInputs;
            }

            return CraftingStatus.Success;
        }

        /// <summary>
        /// Checks whether every stack could be inserted, scaled by <paramref name="count"/>.
        /// Read only.
        /// </summary>
        /// <remarks>
        /// Each stack is tested independently. A container with a shared capacity pool can
        /// report that two outputs each fit while both together do not. If your container
        /// works that way, make <see cref="IItemContainer.GetInsertableAmount"/> conservative
        /// or check the batch yourself before calling.
        /// </remarks>
        public static CraftingStatus CheckSpace(ItemStack[] stacks, int count, IItemContainer container)
        {
            if (container == null)
                return CraftingStatus.InvalidRequest;

            if (stacks == null || stacks.Length == 0)
                return CraftingStatus.Success;

            for (int i = 0; i < stacks.Length; i++)
            {
                ItemStack stack = stacks[i];
                if (stack.IsEmpty)
                    continue;

                ItemStack scaled = stack.Scale(count);
                if (container.GetInsertableAmount(in scaled) < scaled.Amount)
                    return CraftingStatus.OutputBlocked;
            }

            return CraftingStatus.Success;
        }

        /// <summary>
        /// Removes every stack, scaled by <paramref name="count"/>, or removes nothing at all.
        /// If the container removes less than requested part way through, everything already
        /// taken is put back before returning.
        /// </summary>
        public static CraftingStatus ConsumeAll(ItemStack[] stacks, int count, IItemContainer container)
        {
            if (container == null || count < 1)
                return CraftingStatus.InvalidRequest;

            if (stacks == null || stacks.Length == 0)
                return CraftingStatus.Success;

            for (int i = 0; i < stacks.Length; i++)
            {
                ItemStack stack = stacks[i];
                if (stack.IsEmpty)
                    continue;

                ItemStack scaled = stack.Scale(count);
                int removed = container.Remove(in scaled);

                if (removed >= scaled.Amount)
                    continue;

                // Partial removal: give back what this step took, then unwind the earlier ones.
                if (removed > 0)
                {
                    ItemStack partial = scaled.WithAmount(removed);
                    container.Insert(in partial);
                }

                Rollback(stacks, count, container, i);
                return CraftingStatus.MissingInputs;
            }

            return CraftingStatus.Success;
        }

        /// <summary>
        /// Inserts every stack, scaled by <paramref name="count"/>.
        /// Unlike consumption this does not roll back: by the time outputs are produced the
        /// inputs are already gone, and destroying the result would be worse than reporting it.
        /// Anything that did not fit is appended to <paramref name="overflow"/> when provided.
        /// </summary>
        /// <returns>
        /// <see cref="CraftingStatus.Success"/> when everything fit,
        /// <see cref="CraftingStatus.OutputBlocked"/> when some of it did not.
        /// </returns>
        public static CraftingStatus ProduceAll(
            ItemStack[] stacks,
            int count,
            IItemContainer container,
            List<ItemStack> overflow = null)
        {
            if (container == null || count < 1)
                return CraftingStatus.InvalidRequest;

            if (stacks == null || stacks.Length == 0)
                return CraftingStatus.Success;

            bool complete = true;

            for (int i = 0; i < stacks.Length; i++)
            {
                ItemStack stack = stacks[i];
                if (stack.IsEmpty)
                    continue;

                ItemStack scaled = stack.Scale(count);
                int inserted = container.Insert(in scaled);

                if (inserted >= scaled.Amount)
                    continue;

                complete = false;
                overflow?.Add(scaled.Subtract(inserted));
            }

            return complete ? CraftingStatus.Success : CraftingStatus.OutputBlocked;
        }

        /// <summary>
        /// Puts back the first <paramref name="upToIndex"/> stacks, scaled by
        /// <paramref name="count"/>. Used for rollback and for cancelling a running job.
        /// </summary>
        public static void Rollback(ItemStack[] stacks, int count, IItemContainer container, int upToIndex)
        {
            if (stacks == null || container == null)
                return;

            if (upToIndex > stacks.Length)
                upToIndex = stacks.Length;

            for (int i = 0; i < upToIndex; i++)
            {
                ItemStack stack = stacks[i];
                if (stack.IsEmpty)
                    continue;

                ItemStack scaled = stack.Scale(count);
                container.Insert(in scaled);
            }
        }

        /// <summary>
        /// How many times the recipe's inputs could be paid for out of this container.
        /// Returns <see cref="int.MaxValue"/> for a recipe with no inputs.
        /// </summary>
        public static int GetAffordableCount(ItemStack[] stacks, IItemContainer container)
        {
            if (container == null)
                return 0;

            if (stacks == null || stacks.Length == 0)
                return int.MaxValue;

            int best = int.MaxValue;

            for (int i = 0; i < stacks.Length; i++)
            {
                ItemStack stack = stacks[i];
                if (stack.IsEmpty)
                    continue;

                int available = container.GetAmount(stack.Item);
                int possible = available / stack.Amount;

                if (possible < best)
                    best = possible;

                if (best == 0)
                    return 0;
            }

            return best;
        }
    }
}