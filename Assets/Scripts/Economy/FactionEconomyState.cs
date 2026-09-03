using System.Collections.Generic;
using MechTS.Units;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// One faction's live economy state during a mission: its resource stockpile,
    /// power capacity/usage, and currently standing buildings.
    /// </summary>
    public class FactionEconomyState
    {
        /// <summary>The faction this state belongs to.</summary>
        public Faction Faction { get; }

        /// <summary>This faction's current and lifetime-earned resources.</summary>
        public ResourceStockpile Stockpile { get; }

        /// <summary>Buildings this faction currently has standing.</summary>
        public List<BuildingInstance> Buildings { get; } = new List<BuildingInstance>();

        /// <summary>Total power capacity from all standing power-generating buildings.</summary>
        public int PowerCapacity { get; set; }

        /// <summary>Total power currently consumed by standing buildings.</summary>
        public int PowerUsed { get; set; }

        /// <summary>
        /// This faction's locked resource pool (issue #52) — a fresh instance per
        /// <see cref="FactionEconomyState"/>, so it resets to empty/0% automatically every
        /// time <see cref="EconomyManager"/> constructs a new state on a fresh Economy Round
        /// entry, matching every other per-attempt economy state.
        /// </summary>
        public LockedResourcePool LockedPool { get; } = new LockedResourcePool();

        /// <summary>
        /// Creates a new faction economy state seeded with the mission's starting resources.
        /// </summary>
        /// <param name="faction">The faction this state tracks.</param>
        /// <param name="startingOre">Starting Ore amount.</param>
        /// <param name="startingBiomass">Starting Biomass amount.</param>
        /// <param name="startingGold">Starting Gold amount.</param>
        public FactionEconomyState(Faction faction, int startingOre, int startingBiomass, int startingGold)
        {
            Faction = faction;
            Stockpile = new ResourceStockpile(startingOre, startingBiomass, startingGold);
        }

        /// <summary>
        /// Returns whether this faction has any production-capable building still standing.
        /// </summary>
        public bool HasProductionBuilding()
        {
            foreach (var building in Buildings)
            {
                if (building.CanProduceUnits) return true;
            }
            return false;
        }

        /// <summary>
        /// Deposits harvested resources into this faction's stockpile, first diverting the
        /// faction's current allocation percentage (issue #52) into its locked pool — capped
        /// at remaining Silo capacity, with any amount that would overflow a full/zero-capacity
        /// pool falling back to the stockpile instead. The full original amount always counts
        /// toward the stockpile's lifetime-earned total (<see cref="ResourceStockpile.GetTotalEarned"/>,
        /// the Battle Round tie-break) regardless of the split, via
        /// <see cref="ResourceStockpile.RecordAdditionalEarned"/> for the pooled portion
        /// (moved from <see cref="EconomyManager.Deposit"/> by issue #61).
        /// </summary>
        /// <param name="type">The resource type deposited.</param>
        /// <param name="amount">The amount deposited.</param>
        public void Deposit(ResourceType type, int amount)
        {
            int allocationPercent = LockedPool.GetAllocationPercent(type);
            int allocatedAmount = Mathf.RoundToInt(amount * (allocationPercent / 100f));
            int pooled = LockedPool.Deposit(type, allocatedAmount);
            int toStockpile = amount - pooled;

            Stockpile.Deposit(type, toStockpile);
            if (pooled > 0) Stockpile.RecordAdditionalEarned(type, pooled);
        }
    }
}
