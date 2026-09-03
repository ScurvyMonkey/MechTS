using System.Collections.Generic;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// One faction's per-resource-type locked pool (issue #52): a live-adjustable allocation
    /// percentage of incoming resources, an accumulated amount, and a capacity bounded by that
    /// faction's built Silos. Deliberately unspendable — nothing in this class removes an
    /// already-banked amount; consuming the pool is the not-yet-built "Injection Round" feature.
    /// </summary>
    public class LockedResourcePool
    {
        private readonly Dictionary<ResourceType, int> _amount = new Dictionary<ResourceType, int>();
        private readonly Dictionary<ResourceType, int> _capacity = new Dictionary<ResourceType, int>();
        private readonly Dictionary<ResourceType, int> _allocationPercent = new Dictionary<ResourceType, int>();

        /// <summary>The amount currently banked for the given resource type.</summary>
        public int GetAmount(ResourceType type) => _amount.TryGetValue(type, out var amount) ? amount : 0;

        /// <summary>The total capacity for the given resource type, summed from every owned Silo.</summary>
        public int GetCapacity(ResourceType type) => _capacity.TryGetValue(type, out var capacity) ? capacity : 0;

        /// <summary>The player's current allocation percentage (0-100) for the given resource type.</summary>
        public int GetAllocationPercent(ResourceType type) => _allocationPercent.TryGetValue(type, out var percent) ? percent : 0;

        /// <summary>
        /// Sets the allocation percentage for the given resource type, clamped to [0, 100].
        /// Only affects future deposits — never reshuffles amounts already banked.
        /// </summary>
        /// <param name="type">The resource type to adjust.</param>
        /// <param name="percent">The new allocation percentage.</param>
        public void SetAllocationPercent(ResourceType type, int percent)
        {
            _allocationPercent[type] = Mathf.Clamp(percent, 0, 100);
        }

        /// <summary>
        /// Adds to this resource type's total capacity, from a newly-functional Silo.
        /// </summary>
        /// <param name="type">The resource type the Silo stores.</param>
        /// <param name="amount">The capacity to add.</param>
        public void AddCapacity(ResourceType type, int amount)
        {
            _capacity[type] = GetCapacity(type) + amount;
        }

        /// <summary>
        /// Removes from this resource type's total capacity, when a Silo is deregistered.
        /// Never truncates an already-banked amount that now exceeds the reduced capacity —
        /// it simply blocks further deposits of that type until capacity is rebuilt.
        /// </summary>
        /// <param name="type">The resource type the Silo stored.</param>
        /// <param name="amount">The capacity to remove.</param>
        public void RemoveCapacity(ResourceType type, int amount)
        {
            _capacity[type] = GetCapacity(type) - amount;
        }

        /// <summary>
        /// Attempts to bank up to <paramref name="amount"/> of the given resource type,
        /// capped by remaining capacity. Returns how much was actually banked, so the caller
        /// can route the remainder elsewhere — no resources are ever lost to a full pool.
        /// </summary>
        /// <param name="type">The resource type being deposited.</param>
        /// <param name="amount">The amount attempting to be banked.</param>
        public int Deposit(ResourceType type, int amount)
        {
            if (amount <= 0) return 0;

            int room = Mathf.Max(0, GetCapacity(type) - GetAmount(type));
            int deposited = Mathf.Min(amount, room);
            if (deposited > 0) _amount[type] = GetAmount(type) + deposited;
            return deposited;
        }

        /// <summary>
        /// Returns whether the pool currently holds enough of every listed cost to afford it
        /// (issue #53 — the Injection Round). Mirrors <see cref="ResourceStockpile.CanAfford"/>'s
        /// exact shape.
        /// </summary>
        /// <param name="cost">The resource costs to check against.</param>
        public bool CanAfford(IReadOnlyDictionary<ResourceType, int> cost)
        {
            foreach (var entry in cost)
            {
                if (GetAmount(entry.Key) < entry.Value) return false;
            }
            return true;
        }

        /// <summary>
        /// Deducts the given cost from the pool (issue #53). Caller must have already
        /// confirmed affordability via <see cref="CanAfford"/>. Freeing up banked amount this
        /// way also frees up room for future deposits, since <see cref="Deposit"/>'s room
        /// calculation is always <c>capacity - current amount</c>.
        /// </summary>
        /// <param name="cost">The resource costs to deduct.</param>
        public void Spend(IReadOnlyDictionary<ResourceType, int> cost)
        {
            foreach (var entry in cost)
            {
                _amount[entry.Key] = GetAmount(entry.Key) - entry.Value;
            }
        }
    }
}
