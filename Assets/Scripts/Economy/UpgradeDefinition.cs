using System.Collections.Generic;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Base for every researchable tech-tree upgrade. New upgrade *types* are added by
    /// writing a new subclass (see <see cref="StatUpgradeDefinition"/>/
    /// <see cref="UnlockUpgradeDefinition"/>) — <see cref="TechManager"/> and
    /// <see cref="UI.TechPanel"/> never need to change to support one.
    /// </summary>
    public abstract class UpgradeDefinition : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        public int tier;
        public UpgradeDefinition[] prerequisites;

        [Header("Cost")]
        public int oreCost;
        public int biomassCost;
        public int goldCost;

        /// <summary>
        /// Whether this upgrade can be reverted and replaced with an alternative via the
        /// Injection panel (issue #54) once researched — e.g. a weapon-type choice a player
        /// might need to pivot away from mid-battle. Tier-progression upgrades (Gun I → Gun
        /// II) should leave this false; once researched, they stay permanent.
        /// </summary>
        [Header("Injection Round")]
        public bool isSwappable;

        /// <summary>
        /// Returns this upgrade's resource cost as a lookup keyed by resource type, matching
        /// the existing cost pattern already used by <see cref="UnitProductionDefinition"/>/
        /// <see cref="BuildingDefinition"/>.
        /// </summary>
        public Dictionary<ResourceType, int> GetCost()
        {
            return new Dictionary<ResourceType, int>
            {
                { ResourceType.Ore, oreCost },
                { ResourceType.Biomass, biomassCost },
                { ResourceType.Gold, goldCost }
            };
        }

        /// <summary>
        /// Applies this upgrade's effect for the researching faction. Called once by
        /// <see cref="TechManager.TryResearch"/> immediately after the cost is spent and the
        /// upgrade is marked researched.
        /// </summary>
        /// <param name="faction">The faction that researched this upgrade.</param>
        /// <param name="techManager">The tech manager to register the effect with.</param>
        public abstract void Apply(Units.Faction faction, TechManager techManager);

        /// <summary>
        /// Undoes this upgrade's effect for the given faction (issue #54) — the exact inverse
        /// of <see cref="Apply"/>. Called once by <see cref="TechManager.TryRevert"/>
        /// immediately after the upgrade is unmarked researched. Only ever called for an
        /// upgrade with <see cref="isSwappable"/> true; reverting costs nothing — the original
        /// research spend is a pure sunk cost.
        /// </summary>
        /// <param name="faction">The faction reverting this upgrade.</param>
        /// <param name="techManager">The tech manager to remove the effect from.</param>
        public abstract void Revert(Units.Faction faction, TechManager techManager);
    }
}
