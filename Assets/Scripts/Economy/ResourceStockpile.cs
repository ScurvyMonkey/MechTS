using System;
using System.Collections.Generic;

namespace MechTS.Economy
{
    /// <summary>
    /// Tracks one faction's current resource amounts and lifetime total earned. The
    /// lifetime total is used for the Battle Round's simultaneous-defeat tie-break.
    /// </summary>
    public class ResourceStockpile
    {
        private readonly Dictionary<ResourceType, int> _current = new Dictionary<ResourceType, int>();
        private readonly Dictionary<ResourceType, int> _totalEarned = new Dictionary<ResourceType, int>();

        /// <summary>
        /// Creates a stockpile seeded with the given starting amounts.
        /// </summary>
        /// <param name="startingOre">Starting Ore amount.</param>
        /// <param name="startingBiomass">Starting Biomass amount.</param>
        /// <param name="startingGold">Starting Gold amount.</param>
        public ResourceStockpile(int startingOre, int startingBiomass, int startingGold)
        {
            _current[ResourceType.Ore] = startingOre;
            _current[ResourceType.Biomass] = startingBiomass;
            _current[ResourceType.Gold] = startingGold;

            _totalEarned[ResourceType.Ore] = startingOre;
            _totalEarned[ResourceType.Biomass] = startingBiomass;
            _totalEarned[ResourceType.Gold] = startingGold;
        }

        /// <summary>
        /// The current amount of the given resource.
        /// </summary>
        /// <param name="type">The resource type to query.</param>
        public int GetAmount(ResourceType type) => _current.TryGetValue(type, out var amount) ? amount : 0;

        /// <summary>
        /// Adds harvested resources to the stockpile and to the lifetime-earned total.
        /// </summary>
        /// <param name="type">The resource type deposited.</param>
        /// <param name="amount">The amount deposited.</param>
        public void Deposit(ResourceType type, int amount)
        {
            _current[type] = GetAmount(type) + amount;
            _totalEarned[type] = (_totalEarned.TryGetValue(type, out var earned) ? earned : 0) + amount;
        }

        /// <summary>
        /// Records additional lifetime-earned resources without adding them to the current
        /// spendable amount (issue #52) — used when part of a deposit is diverted to the
        /// locked pool instead of this stockpile, so the Battle Round's lifetime-earned
        /// tie-break (<see cref="GetTotalEarned"/>) still reflects the true total earned,
        /// not just what landed here.
        /// </summary>
        /// <param name="type">The resource type earned.</param>
        /// <param name="amount">The additional amount to credit toward the lifetime total.</param>
        public void RecordAdditionalEarned(ResourceType type, int amount)
        {
            _totalEarned[type] = (_totalEarned.TryGetValue(type, out var earned) ? earned : 0) + amount;
        }

        /// <summary>
        /// Returns whether the stockpile currently holds enough of every listed cost to afford it.
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
        /// Deducts the given cost from the stockpile. Caller must have already confirmed affordability.
        /// </summary>
        /// <param name="cost">The resource costs to deduct.</param>
        public void Spend(IReadOnlyDictionary<ResourceType, int> cost)
        {
            foreach (var entry in cost)
            {
                _current[entry.Key] = GetAmount(entry.Key) - entry.Value;
            }
        }

        /// <summary>
        /// Returns whether every resource's current amount is zero.
        /// </summary>
        public bool IsEmpty()
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                if (GetAmount(type) > 0) return false;
            }
            return true;
        }

        /// <summary>
        /// The lifetime total of all resources ever earned by this faction, summed across
        /// types. Used for the Battle Round's simultaneous-defeat tie-break.
        /// </summary>
        public int GetTotalEarned()
        {
            int sum = 0;
            foreach (var value in _totalEarned.Values) sum += value;
            return sum;
        }
    }
}
